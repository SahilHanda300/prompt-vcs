namespace PromptVcs.Core;

public record PromptVersion(int Version, string Content, DateTimeOffset CreatedAt);

public record QaCheckResult(bool Passed, string? Detail = null, long? DurationMs = null);

public record QaChecks(QaCheckResult Validation, QaCheckResult ContentSafety, QaCheckResult TrialGeneration);

public record QaCheckpoint(int Version, DateTimeOffset Timestamp, bool Passed, QaChecks Checks);

public enum BuildStatus
{
    Success,
    Failed,
    Skipped,
}

public record Build(int BuildVersion, int PromptVersion, string? ArtifactUrl, DateTimeOffset Timestamp, BuildStatus Status, string? Detail = null);

public class Environments
{
    public int? Dev { get; set; }
    public int? Qa { get; set; }
    public int? Prod { get; set; }
}

public class PromptRecord
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public List<PromptVersion> History { get; set; } = new();
    public Environments Environments { get; set; } = new();
    public List<QaCheckpoint> QaCheckpoints { get; set; } = new();
    public List<Build> Builds { get; set; } = new();
}

public class Store
{
    public Dictionary<string, PromptRecord> Prompts { get; set; } = new();
}

public enum PipelineStage
{
    QaFailed,
    PublishSkipped,
    Published,
    PublishFailed,
}

public record PipelineResult(PipelineStage Stage, string PromptId, int Version, QaCheckpoint Checkpoint, Build? Build, string Message);
