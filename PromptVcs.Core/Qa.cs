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

        // No point spending a Claude Code call on a prompt that already
        // failed local validation. Content safety and feasibility are
        // otherwise independent assessments of the same prompt text, so
        // they're combined into one Claude Code call (RunSafetyAndFeasibilityAsync)
        // instead of two sequential ones — same two real checks, half the
        // subprocess/cache-creation overhead, since that overhead is paid
        // per call, not per assessment.
        var (contentSafety, trialGeneration) = validation.Passed
            ? await RunSafetyAndFeasibilityAsync(content)
            : (new QaCheckResult(false, "Skipped: validation failed."), new QaCheckResult(false, "Skipped: validation failed."));

        var passed = validation.Passed && contentSafety.Passed && trialGeneration.Passed;

        return new QaCheckpoint(
            version,
            DateTimeOffset.UtcNow,
            passed,
            new QaChecks(validation, contentSafety, trialGeneration));
    }

    /// Public so Pipeline can validate standalone on the first-run path,
    /// where safety/feasibility come from PublishRules.ScreenAndPublishFirstRunAsync
    /// instead of RunAsync's own combined call — calling RunAsync there would
    /// mean a redundant extra Claude Code call before the combined one.
    public static QaCheckResult RunValidation(string content)
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

    private async Task<(QaCheckResult ContentSafety, QaCheckResult TrialGeneration)> RunSafetyAndFeasibilityAsync(string content)
    {
        var prompt = string.Join("\n",
            "You are screening a prompt for a tool that turns prompts into generated single-page sites/apps.",
            "Its users are non-technical and often write short, vague prompts (e.g. \"a landing page for a coffee shop\") — that is expected and completely fine, not a reason to fail either check below. A vague prompt gives the generator creative freedom to fill in specifics.",
            "Assess two independent things about the prompt:",
            "1. safe — is it free of genuinely unsafe/harmful content? Being brief or vague is not unsafe.",
            "2. feasible — can something reasonable be built from it as a single self-contained HTML page? Mark true unless the prompt is empty/gibberish, or explicitly requires something a single page structurally cannot provide (e.g. a real multi-user backend, a database, server-side payments).",
            "Respond with JSON only, no other text, in the form {\"safe\": boolean, \"safetyReason\": string, \"feasible\": boolean, \"feasibilitySummary\": string}.",
            "Do not generate the actual site or app.",
            "",
            "Prompt to evaluate:",
            content);

        var result = await _invoker.InvokeAsync(prompt);
        if (!result.Ok)
        {
            var failure = new QaCheckResult(false, result.Detail ?? "Safety/feasibility check failed to run.");
            return (failure, failure);
        }

        try
        {
            var start = result.Text.IndexOf('{');
            var end = result.Text.LastIndexOf('}');
            var jsonText = start >= 0 && end > start ? result.Text[start..(end + 1)] : result.Text;
            using var doc = JsonDocument.Parse(jsonText);

            var safe = doc.RootElement.TryGetProperty("safe", out var safeProp) && safeProp.GetBoolean();
            var safetyReason = doc.RootElement.TryGetProperty("safetyReason", out var reasonProp) ? reasonProp.GetString() : result.Text.Trim();
            var feasible = doc.RootElement.TryGetProperty("feasible", out var feasibleProp) && feasibleProp.GetBoolean();
            var summary = doc.RootElement.TryGetProperty("feasibilitySummary", out var summaryProp) ? summaryProp.GetString() : result.Text.Trim();

            return (new QaCheckResult(safe, safetyReason), new QaCheckResult(feasible, summary));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            var failure = new QaCheckResult(false, $"Could not parse safety/feasibility response: {result.Text.Trim()}");
            return (failure, failure);
        }
    }
}
