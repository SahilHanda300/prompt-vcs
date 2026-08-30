using System.Diagnostics;
using System.Text.Json;

namespace PromptVcs.Core;

public class Qa
{
    private const int MaxPromptLength = 20_000;

    private readonly IClaudeCodeInvoker _invoker;

    public Qa(IClaudeCodeInvoker invoker)
    {
        _invoker = invoker;
    }

    public async Task<QaCheckpoint> RunAsync(int version, string content)
    {
        var validation = RunValidation(content);

        // No point spending Claude Code calls on a prompt that already failed local validation.
        var contentSafety = validation.Passed
            ? await RunContentSafetyAsync(content)
            : new QaCheckResult(false, "Skipped: validation failed.");
        var trialGeneration = validation.Passed && contentSafety.Passed
            ? await RunTrialGenerationAsync(content)
            : new QaCheckResult(false, "Skipped: prior check failed.");

        var passed = validation.Passed && contentSafety.Passed && trialGeneration.Passed;

        return new QaCheckpoint(
            version,
            DateTimeOffset.UtcNow,
            passed,
            new QaChecks(validation, contentSafety, trialGeneration));
    }

    private static QaCheckResult RunValidation(string content)
    {
        var sw = Stopwatch.StartNew();
        var trimmed = content.Trim();
        if (trimmed.Length == 0)
        {
            return new QaCheckResult(false, "Prompt is empty or whitespace-only.", sw.ElapsedMilliseconds);
        }
        if (trimmed.Length > MaxPromptLength)
        {
            return new QaCheckResult(false, $"Prompt exceeds {MaxPromptLength} character limit (got {trimmed.Length}).", sw.ElapsedMilliseconds);
        }
        return new QaCheckResult(true, null, sw.ElapsedMilliseconds);
    }

    private async Task<QaCheckResult> RunContentSafetyAsync(string content)
    {
        var prompt = string.Join("\n",
            "You are a content safety screener for a tool that turns prompts into generated websites/apps.",
            "Respond with exactly SAFE or UNSAFE on the first line, followed by a one-line reason.",
            "Do not generate anything else.",
            "",
            "Prompt to screen:",
            content);

        var result = await _invoker.InvokeAsync(prompt);
        if (!result.Ok)
        {
            return new QaCheckResult(false, result.Detail ?? "Content safety check failed to run.");
        }
        var firstLine = result.Text.Trim().Split('\n')[0].Trim().ToUpperInvariant();
        return new QaCheckResult(firstLine.StartsWith("SAFE"), result.Text.Trim());
    }

    private async Task<QaCheckResult> RunTrialGenerationAsync(string content)
    {
        var prompt = string.Join("\n",
            "You are evaluating feasibility for a tool that turns prompts into a generated single-page site or app.",
            "Respond with JSON only, no other text, in the form {\"feasible\": boolean, \"summary\": string}.",
            "Do not generate the actual site or app — just assess whether the prompt is clear and buildable.",
            "",
            "Prompt to evaluate:",
            content);

        var result = await _invoker.InvokeAsync(prompt);
        if (!result.Ok)
        {
            return new QaCheckResult(false, result.Detail ?? "Trial generation failed to run.");
        }

        try
        {
            var start = result.Text.IndexOf('{');
            var end = result.Text.LastIndexOf('}');
            var jsonText = start >= 0 && end > start ? result.Text[start..(end + 1)] : result.Text;
            using var doc = JsonDocument.Parse(jsonText);
            var feasible = doc.RootElement.TryGetProperty("feasible", out var f) && f.GetBoolean();
            var summary = doc.RootElement.TryGetProperty("summary", out var s) ? s.GetString() : result.Text.Trim();
            return new QaCheckResult(feasible, summary);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return new QaCheckResult(false, $"Could not parse trial generation response: {result.Text.Trim()}");
        }
    }
}
