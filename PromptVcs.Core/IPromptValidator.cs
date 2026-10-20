namespace PromptVcs.Core;

/// <summary>
/// Local, no-network validation of raw prompt text (empty/malformed/too
/// long) — the first of QA's four checks. Split out from <see cref="Qa"/>
/// so Pipeline can run it standalone on the first-run path (before
/// deciding whether to run the combined safety+feasibility Claude Code
/// call at all) without depending on the rest of Qa's orchestration.
/// </summary>
public interface IPromptValidator
{
    QaCheckResult Validate(string content);
}
