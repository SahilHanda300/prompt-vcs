using System.Diagnostics;
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

    public async Task<ClaudeCodeResult> InvokeAsync(string prompt, CancellationToken ct = default)
    {
        if (Environment.GetEnvironmentVariable("PROMPTVCS_MOCK_CLAUDE") == "1")
        {
            return MockInvoke(prompt);
        }

        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--output-format");
        psi.ArgumentList.Add("json");

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
        if (prompt.Contains("respond with exactly SAFE or UNSAFE", StringComparison.OrdinalIgnoreCase))
        {
            return new ClaudeCodeResult(true, "SAFE\nNo concerning content detected.", false, null);
        }
        if (prompt.Contains("respond with JSON", StringComparison.OrdinalIgnoreCase))
        {
            return new ClaudeCodeResult(true, "{\"feasible\": true, \"summary\": \"Mock trial generation looks feasible.\"}", false, null);
        }
        var html = "<!doctype html>\n<html>\n<head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Mock Artifact</title></head>\n<body><h1>Mock generated artifact</h1><p>Generated from a " + prompt.Length + "-character prompt.</p></body>\n</html>";
        return new ClaudeCodeResult(true, html, false, null);
    }
}
