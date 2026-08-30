namespace PromptVcs.Cli;

// Small local records mirroring the MCP server's JSON responses. Deliberately
// separate from PromptVcs.Core's model types — the CLI is a standalone thin
// client now and has no project reference to Core (which carries
// server/runner-only logic: Pipeline, Qa, PublishRules, ClaudeCodeInvoker).

public record QaCheckResultDto(bool Passed, string? Detail, long? DurationMs);
public record QaChecksDto(QaCheckResultDto Validation, QaCheckResultDto ContentSafety, QaCheckResultDto TrialGeneration);
public record QaCheckpointDto(int Version, DateTimeOffset Timestamp, bool Passed, QaChecksDto Checks);
public record BuildDto(int BuildVersion, int PromptVersion, string? ArtifactUrl, DateTimeOffset Timestamp, string Status, string? Detail);
public record PipelineResultDto(string Stage, string PromptId, int Version, QaCheckpointDto Checkpoint, BuildDto? Build, string Message);

public record LatestBuildDto(int BuildVersion, string Status, string? ArtifactUrl);
public record PromptListItemDto(string Name, int? Dev, int? Qa, int? Prod, LatestBuildDto? LatestBuild);

public record PromptVersionDto(int Version, string Content, DateTimeOffset CreatedAt);
public record EnvironmentsDto(int? Dev, int? Qa, int? Prod);
public record PromptRecordDto(string Id, string Name, List<PromptVersionDto> History, EnvironmentsDto Environments, List<QaCheckpointDto> QaCheckpoints, List<BuildDto> Builds);
public record ShowResultDto(PromptRecordDto Record, int Version, string Content);

public record DiffLineDto(string Type, string Text);

public record InitResultDto(bool AlreadyInitialized);
