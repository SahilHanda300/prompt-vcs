namespace PromptVcs.Core;

public record PublishOutcome(bool Ok, int BuildVersion, string? ArtifactRelativePath, string? Detail);

/// <summary>
/// Owns the first-run vs. diff-aware-update decision — the core of what the
/// publish stage is responsible for: it doesn't just pass the prompt
/// through to the generator, it decides how to build/update the site.
/// </summary>
public interface IPublishService
{
    /// Update path only (Pipeline calls this when a prior successful build
    /// exists) — one Claude Code call, diff-aware.
    Task<PublishOutcome> PublishAsync(PromptRecord record, int promptVersion, string content, IReadOnlyList<Attachment>? attachments = null);

    /// First-run path only — combines safety/feasibility screening and the
    /// actual generation, see PublishRules for the full mechanism.
    Task<(QaCheckResult Safety, QaCheckResult Feasibility, PublishOutcome Outcome)> ScreenAndPublishFirstRunAsync(
        PromptRecord record, int promptVersion, string content, IReadOnlyList<Attachment>? attachments = null);
}
