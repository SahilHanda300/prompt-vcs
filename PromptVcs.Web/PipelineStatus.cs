namespace PromptVcs.Web;

/// <summary>
/// A plain-language, percentage-based summary of where a prompt's pipeline
/// run landed — for non-technical users who shouldn't need to understand
/// "dev/qa/prod" or read a raw QA checkpoint to know what happened.
/// </summary>
public record PipelineStatusInfo(string Message, int Percent, bool Failed, string? Detail = null);

public static class PipelineStatus
{
    /// Full detail available (Pages/Prompt.cshtml): uses the latest
    /// version's actual QA checkpoint to say exactly which stage it's at
    /// or which check it failed. Detail carries the actual reason (the
    /// check's own Detail text) so a failure is never a dead end — this
    /// matters especially for content-safety/feasibility failures, which
    /// can otherwise look like an unexplained rejection.
    public static PipelineStatusInfo ForRecord(PromptRecordDto record)
    {
        if (record.History.Count == 0)
        {
            return new PipelineStatusInfo("No versions yet.", 0, false);
        }

        var latestVersion = record.History.Max(h => h.Version);
        var checkpoint = record.QaCheckpoints.LastOrDefault(c => c.Version == latestVersion);
        var build = record.Builds.LastOrDefault(b => b.PromptVersion == latestVersion);

        if (checkpoint == null)
        {
            return new PipelineStatusInfo("Your prompt was submitted — checks are starting", 20, false);
        }
        if (!checkpoint.Checks.Validation.Passed)
        {
            return new PipelineStatusInfo("Your prompt didn't pass validation", 20, true, checkpoint.Checks.Validation.Detail);
        }
        if (!checkpoint.Checks.ContentSafety.Passed)
        {
            return new PipelineStatusInfo("Your prompt didn't pass the content safety check", 40, true, checkpoint.Checks.ContentSafety.Detail);
        }
        if (!checkpoint.Checks.TrialGeneration.Passed)
        {
            return new PipelineStatusInfo("Your prompt didn't pass the feasibility check", 60, true, checkpoint.Checks.TrialGeneration.Detail);
        }
        if (build == null)
        {
            return new PipelineStatusInfo("Your prompt passed QA — publishing now", 80, false);
        }
        if (build.Status == "success")
        {
            return new PipelineStatusInfo("Your prompt passed QA and was published", 100, false);
        }
        return new PipelineStatusInfo("Your prompt passed QA, but publishing failed", 80, true, build.Detail);
    }

    /// Coarser version for Pages/Dashboard.cshtml, which only has the
    /// summary fields the `list` tool returns (no per-check breakdown).
    public static PipelineStatusInfo ForListItem(PromptListItemDto item)
    {
        if (!item.Dev.HasValue)
        {
            return new PipelineStatusInfo("No versions yet.", 0, false);
        }
        if (!item.Prod.HasValue)
        {
            return new PipelineStatusInfo("Didn't pass QA", 40, true);
        }
        if (item.LatestBuild == null)
        {
            return new PipelineStatusInfo("Passed QA — publishing now", 80, false);
        }
        if (item.LatestBuild.Status == "success")
        {
            return new PipelineStatusInfo("Passed QA and was published", 100, false);
        }
        return new PipelineStatusInfo("Passed QA, but publishing failed", 80, true);
    }
}
