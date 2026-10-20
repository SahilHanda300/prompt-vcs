namespace PromptVcs.Core;

/// <summary>
/// Runs the QA stage's non-first-run checks: local validation plus one
/// combined content-safety/feasibility Claude Code call. See
/// <see cref="Qa"/> for why first-run submissions use a different path
/// (PublishRules.ScreenAndPublishFirstRunAsync combines screening with
/// generation itself, so it doesn't call this).
/// </summary>
public interface IQaService
{
    Task<QaCheckpoint> RunAsync(int version, string content);
}
