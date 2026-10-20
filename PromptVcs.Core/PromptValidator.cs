using System.Diagnostics;

namespace PromptVcs.Core;

public class PromptValidator : IPromptValidator
{
    private const int MaxPromptLength = 20_000;

    public QaCheckResult Validate(string content)
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
}
