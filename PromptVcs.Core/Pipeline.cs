namespace PromptVcs.Core;

/// <summary>
/// Submits a new prompt version and drives it through the fully automatic
/// dev -> qa -> prod pipeline in one call. This is the only entry point that
/// advances the pipeline — there is no manual promote step.
///
/// Runs entirely server-side now: QA's Claude Code calls and the publish
/// step's generation call both go through the same IClaudeCodeInvoker, which
/// on the server is a dispatcher that forwards to whichever Runner is
/// connected (see PromptVcs.McpServer.Services.RunnerDispatchInvoker) rather
/// than shelling out locally.
/// </summary>
public class Pipeline
{
    private readonly Qa _qa;
    private readonly PublishRules _publishRules;

    public Pipeline(Qa qa, PublishRules publishRules)
    {
        _qa = qa;
        _publishRules = publishRules;
    }

    public async Task<PipelineResult> SubmitAsync(PromptRecord record, string content)
    {
        var version = record.History.Count > 0 ? record.History[^1].Version + 1 : 1;
        record.History.Add(new PromptVersion(version, content, DateTimeOffset.UtcNow));
        record.Environments.Dev = version;

        var checkpoint = await _qa.RunAsync(version, content);
        record.QaCheckpoints.Add(checkpoint);

        if (!checkpoint.Passed)
        {
            var failedCheck = !checkpoint.Checks.Validation.Passed
                ? $"validation: {checkpoint.Checks.Validation.Detail}"
                : !checkpoint.Checks.ContentSafety.Passed
                    ? $"content safety: {checkpoint.Checks.ContentSafety.Detail}"
                    : $"trial generation: {checkpoint.Checks.TrialGeneration.Detail}";
            return new PipelineResult(PipelineStage.QaFailed, record.Id, version, checkpoint, null, $"QA failed at {failedCheck}");
        }

        record.Environments.Qa = version;
        record.Environments.Prod = version;

        var outcome = await _publishRules.PublishAsync(record, version, content);

        var build = new Build(
            outcome.BuildVersion,
            version,
            outcome.ArtifactRelativePath,
            DateTimeOffset.UtcNow,
            outcome.Ok ? BuildStatus.Success : BuildStatus.Failed,
            outcome.Detail);
        record.Builds.Add(build);

        return new PipelineResult(
            outcome.Ok ? PipelineStage.Published : PipelineStage.PublishFailed,
            record.Id,
            version,
            checkpoint,
            build,
            outcome.Ok ? $"Published: {build.ArtifactUrl ?? "(no url returned)"}" : $"Publish failed: {build.Detail ?? "unknown error"}");
    }
}
