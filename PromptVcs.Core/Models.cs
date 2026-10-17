using System.Text.Json.Serialization;

namespace PromptVcs.Core;

/// Images attached to a prompt submission, sent to Claude Code as reference
/// context for generation (e.g. a logo to incorporate) — stored as base64
/// alongside the rest of the store, same as everything else in this project.
/// JsonPropertyName is explicit here (rather than relying on a serializer's
/// naming policy) since this type crosses two independent JSON boundaries
/// with different default conventions: the MCP tool argument that creates
/// it, and the SignalR hub call that forwards it to the Runner.
public record Attachment(
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("base64Data")] string Base64Data);

public record PromptVersion(int Version, string Content, DateTimeOffset CreatedAt)
{
    public List<Attachment> Attachments { get; init; } = new();
}

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
    // Null for records created before prompt-ownership existed ("legacy"
    // prompts) — treated as visible/editable by any authenticated user,
    // same as the fully-open behavior this project had before. A new
    // prompt always gets an Owner at creation time. Admins bypass
    // ownership entirely regardless of this field — see PromptService.
    public string? Owner { get; set; }
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
