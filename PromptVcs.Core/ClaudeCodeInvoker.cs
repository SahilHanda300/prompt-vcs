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

    public async Task<ClaudeCodeResult> InvokeAsync(string prompt, IReadOnlyList<Attachment>? attachments = null, CancellationToken ct = default)
    {
        if (Environment.GetEnvironmentVariable("PROMPTVCS_MOCK_CLAUDE") == "1")
        {
            return MockInvoke(prompt);
        }

        string? attachmentDir = null;
        if (attachments is { Count: > 0 })
        {
            // Claude Code's headless mode has no way to receive inline image
            // bytes on stdin — it reads images the same way it reads any
            // other file, via a local path its Read tool can open. The
            // Runner is the only component with a real filesystem under
            // Claude Code, so attachments are decoded to a scratch temp
            // directory here and the prompt is told where to find them.
            attachmentDir = Path.Combine(Path.GetTempPath(), "promptvcs-attachments", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(attachmentDir);
            var paths = new List<string>();
            foreach (var attachment in attachments)
            {
                var safeName = string.Join("_", attachment.FileName.Split(Path.GetInvalidFileNameChars()));
                var path = Path.Combine(attachmentDir, $"{paths.Count}_{safeName}");
                await File.WriteAllBytesAsync(path, Convert.FromBase64String(attachment.Base64Data), ct);
                paths.Add(path);
            }
            prompt = string.Join("\n",
                $"You have been given {paths.Count} reference image attachment(s) at the following local file path(s). Use your Read tool to view each one before you begin, and incorporate it as the prompt implies (e.g. use an attached logo or reference image in the generated design):",
                string.Join("\n", paths.Select(p => $"- {p}")),
                "",
                prompt);
        }

        try
        {
            return await InvokeProcessAsync(prompt, attachmentDir, ct);
        }
        finally
        {
            if (attachmentDir != null)
            {
                try { Directory.Delete(attachmentDir, recursive: true); } catch { /* best effort cleanup */ }
            }
        }
    }

    private async Task<ClaudeCodeResult> InvokeProcessAsync(string prompt, string? attachmentDir, CancellationToken ct)
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

        // PublishRules' combined first-run screen+generate prompt: JSON line, then a
        // literal "===HTML===" marker, then raw (non-JSON-escaped) HTML.
        if (prompt.Contains("===HTML===", StringComparison.Ordinal))
        {
            var combined = "{\"safe\": true, \"safetyReason\": \"No concerning content detected.\", \"feasible\": true, \"feasibilitySummary\": \"Mock feasibility check looks buildable.\"}\n===HTML===\n" + html;
            return new ClaudeCodeResult(true, combined, false, null);
        }
        // Qa's own combined safety+feasibility-only prompt (no HTML expected).
        if (prompt.Contains("respond with JSON", StringComparison.OrdinalIgnoreCase) && prompt.Contains("\"safe\"", StringComparison.OrdinalIgnoreCase))
        {
            return new ClaudeCodeResult(true, "{\"safe\": true, \"safetyReason\": \"No concerning content detected.\", \"feasible\": true, \"feasibilitySummary\": \"Mock feasibility check looks buildable.\"}", false, null);
        }
        return new ClaudeCodeResult(true, html, false, null);
    }
}
