namespace PromptVcs.Core;

/// <summary>
/// Submits a new prompt version and drives it through the fully automatic
/// dev -> qa -> prod pipeline in one call — the only entry point that
/// advances the pipeline; there is no manual promote step.
/// </summary>
public interface IPipelineService
{
    Task<PipelineResult> SubmitAsync(PromptRecord record, string content, List<Attachment>? attachments = null);
}
