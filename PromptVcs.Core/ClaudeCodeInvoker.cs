using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace PromptVcs.Core;

public record ClaudeCodeResult(bool Ok, string Text, bool UsageLimitHit, string? Detail);

/// <summary>
/// Invokes Claude Code headlessly in print mode (`claude -p --output-format json`),
/// piping the prompt via stdin (avoids argv length/escaping limits). Authenticated
/// via the user's existing Claude Pro login — the sole generation path for this
/// project; no API key, no fallback provider. Shared by the CLI's QA checks
/// (content safety, trial generation) and the MCP server's generation calls.
///
/// PROMPTVCS_MOCK_CLAUDE=1 is a gated test seam (not a production fallback) so the
/// pipeline can be verified without a live subprocess call.
/// </summary>
public class ClaudeCodeInvoker : IClaudeCodeInvoker
{
    private static readonly string[] UsageLimitMarkers = { "usage limit", "rate limit", "quota", "exceeded your" };

    public async Task<ClaudeCodeResult> InvokeAsync(string prompt, IReadOnlyList<Attachment>? attachments = null, string? model = null, CancellationToken ct = default)
    {
        if (Environment.GetEnvironmentVariable("PROMPTVCS_MOCK_CLAUDE") == "1")
        {
            return MockInvoke(prompt);
        }

        string? attachmentDir = null;
        var placeholders = new Dictionary<string, string>();
        if (attachments is { Count: > 0 })
        {
            // Claude Code's headless mode has no way to receive inline image
            // bytes on stdin for vision input — it has to be a local file its
            // Read tool can open. The Runner is the only component with a
            // real filesystem under Claude Code, so attachments are decoded
            // to a scratch temp directory here, purely so Claude can *see*
            // what the image looks like.
            //
            // Embedding the image in the actual HTML output is a separate
            // problem, and handing Claude the raw base64 to retype verbatim
            // (an earlier version of this code did) turned out to be a real
            // bug in practice: for anything but a tiny test image, that's
            // tens of thousands of output characters Claude has to transcribe
            // exactly, which is slow (risked the Runner's 5-minute job
            // timeout on a real ~36KB logo) and error-prone (one mistyped
            // character silently breaks the image). Instead, Claude is told
            // to use a short unique placeholder token as the <img src>, and
            // this code swaps each placeholder for the real data: URI after
            // generation — Claude never has to see or reproduce the bytes,
            // and the substitution is always byte-exact by construction.
            attachmentDir = Path.Combine(Path.GetTempPath(), "promptvcs-attachments", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(attachmentDir);
            var lines = new List<string>
            {
                $"You have been given {attachments.Count} reference image attachment(s). For each one below: a local file path (view it with your Read tool to understand what it looks like, e.g. its colors/shape/style) and a placeholder token. If the prompt implies this image should actually appear in the generated page (e.g. a logo), use its placeholder token verbatim, unmodified, as the entire <img src=\"...\"> value — e.g. <img src=\"PLACEHOLDER_TOKEN_HERE\" alt=\"...\">. This is a deliberate exception to any \"no external images\" rule elsewhere in this prompt. Do NOT write out any base64 or data: URI yourself — the real image will be substituted in for the placeholder automatically afterward, so writing the actual bytes yourself would be both wasted effort and wrong if even one character differs from the original.",
            };
            for (var i = 0; i < attachments.Count; i++)
            {
                var attachment = attachments[i];
                var safeName = string.Join("_", attachment.FileName.Split(Path.GetInvalidFileNameChars()));
                var path = Path.Combine(attachmentDir, $"{i}_{safeName}");
                await File.WriteAllBytesAsync(path, Convert.FromBase64String(attachment.Base64Data), ct);
                var placeholder = $"PROMPTVCS_ATTACHMENT_{Guid.NewGuid():N}";
                placeholders[placeholder] = $"data:{attachment.ContentType};base64,{attachment.Base64Data}";
                lines.Add($"{i + 1}. \"{attachment.FileName}\" — file path: {path}, placeholder token: {placeholder}");
            }
            lines.Add("");
            lines.Add(prompt);
            prompt = string.Join("\n", lines);
        }

        try
        {
            var result = await InvokeProcessAsync(prompt, attachmentDir, model, ct);
            if (result.Ok && placeholders.Count > 0)
            {
                var text = result.Text;
                foreach (var (placeholder, dataUri) in placeholders)
                {
                    text = text.Replace(placeholder, dataUri, StringComparison.Ordinal);
                }
                result = result with { Text = text };
            }
            return result;
        }
        finally
        {
            if (attachmentDir != null)
            {
                try { Directory.Delete(attachmentDir, recursive: true); } catch { /* best effort cleanup */ }
            }
        }
    }

    private async Task<ClaudeCodeResult> InvokeProcessAsync(string prompt, string? attachmentDir, string? model, CancellationToken ct)
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Without these, .NET decodes the child process's stdout/stderr
            // using Console.OutputEncoding — on Windows (where the Runner
            // actually runs) that's the legacy OEM codepage, not UTF-8. The
            // claude CLI emits real UTF-8 (×, √, π, ⌫, etc.), so without this
            // those multi-byte sequences get misread as that codepage,
            // corrupting them into mojibake before this code ever sees them —
            // no amount of fixing the artifact-writing side (see
            // ArtifactSanitizer) can undo damage done at decode time here.
            StandardOutputEncoding = utf8NoBom,
            StandardErrorEncoding = utf8NoBom,
            StandardInputEncoding = utf8NoBom,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("json");
        if (model != null)
        {
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(model);
        }
        if (attachmentDir != null)
        {
            // Without this, Claude Code's own permission system denies
            // reading a file outside its working directory, even via a
            // plain path reference in the prompt text — confirmed against
            // a real (non-mock) call: the model replied "read permission
            // was denied" for the attachment path without this flag.
            psi.ArgumentList.Add("--add-dir");
            psi.ArgumentList.Add(attachmentDir);
        }

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new ClaudeCodeResult(false, "", false, $"Failed to invoke claude CLI: {ex.Message}");
        }

        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return new ClaudeCodeResult(false, "", false, "claude CLI timed out.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var combined = stdout + "\n" + stderr;
        var usageLimitHit = UsageLimitMarkers.Any(m => combined.Contains(m, StringComparison.OrdinalIgnoreCase));

        if (process.ExitCode != 0)
        {
            return new ClaudeCodeResult(
                false,
                "",
                usageLimitHit,
                usageLimitHit ? "Claude Pro usage limit reached" : $"claude exited with code {process.ExitCode}: {stderr.Trim()}");
        }

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var text = doc.RootElement.TryGetProperty("result", out var resultProp) ? resultProp.GetString() ?? "" : stdout;
            return new ClaudeCodeResult(true, text, false, null);
        }
        catch (JsonException)
        {
            return new ClaudeCodeResult(true, stdout.Trim(), false, null);
        }
    }

    private static ClaudeCodeResult MockInvoke(string prompt)
    {
        var html = "<!doctype html>\n<html>\n<head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Mock Artifact</title></head>\n<body><h1>Mock generated artifact</h1><p>Generated from a " + prompt.Length + "-character prompt.</p></body>\n</html>";

        // PublishRules.BuildFirstRunScreenPrompt: screening only, JSON, no HTML.
        if (prompt.Contains("Just the screening judgment", StringComparison.Ordinal))
        {
            var screen = "{\"safe\": true, \"safetyReason\": \"No concerning content detected.\", \"feasible\": true, " +
                "\"feasibilitySummary\": \"Mock feasibility check looks buildable.\", \"type\": \"content\", \"name\": \"Mock Site\"}";
            return new ClaudeCodeResult(true, screen, false, null);
        }
        // PublishRules.BuildSectionPrompt: one parallel section of a plan-based
        // first-run page — a fragment, not a full document.
        if (prompt.Contains("ONE section of a larger self-contained HTML page", StringComparison.Ordinal))
        {
            return new ClaudeCodeResult(true, $"<section class=\"mock-{Guid.NewGuid():N}\"><p>Mock section, generated from a {prompt.Length}-character prompt.</p></section>", false, null);
        }
        // Qa's own combined safety+feasibility-only prompt (no HTML expected).
        if (prompt.Contains("respond with JSON", StringComparison.OrdinalIgnoreCase) && prompt.Contains("\"safe\"", StringComparison.OrdinalIgnoreCase))
        {
            return new ClaudeCodeResult(true, "{\"safe\": true, \"safetyReason\": \"No concerning content detected.\", \"feasible\": true, \"feasibilitySummary\": \"Mock feasibility check looks buildable.\"}", false, null);
        }
        return new ClaudeCodeResult(true, html, false, null);
    }
}
