namespace PromptVcs.Web;

// Small local records mirroring the MCP server's JSON responses. Deliberately
// separate from PromptVcs.Core's model types — the web app is a standalone
// thin client, same pattern as PromptVcs.Cli, and has no project reference to
// Core.

public record QaCheckResultDto(bool Passed, string? Detail, long? DurationMs);
public record QaChecksDto(QaCheckResultDto Validation, QaCheckResultDto ContentSafety, QaCheckResultDto TrialGeneration);
public record QaCheckpointDto(int Version, DateTimeOffset Timestamp, bool Passed, QaChecksDto Checks);
public record BuildDto(int BuildVersion, int PromptVersion, string? ArtifactUrl, DateTimeOffset Timestamp, string Status, string? Detail);
public record PipelineResultDto(string Stage, string PromptId, int Version, QaCheckpointDto Checkpoint, BuildDto? Build, string Message);

public record LatestBuildDto(int BuildVersion, string Status, string? ArtifactUrl);
public record PromptListItemDto(string Name, string? Owner, int? Dev, int? Qa, int? Prod, LatestBuildDto? LatestBuild);

public record AttachmentDto(string FileName, string ContentType, string Base64Data);

public record PromptVersionDto(int Version, string Content, DateTimeOffset CreatedAt, List<AttachmentDto>? Attachments = null);
public record EnvironmentsDto(int? Dev, int? Qa, int? Prod);
public record PromptRecordDto(string Id, string Name, List<PromptVersionDto> History, EnvironmentsDto Environments, List<QaCheckpointDto> QaCheckpoints, List<BuildDto> Builds);
public record ShowResultDto(PromptRecordDto Record, int Version, string Content);

public record DiffLineDto(string Type, string Text);

public record AuthResultDto(string? SessionToken, bool IsAdmin);
public record LogoutResultDto(bool LoggedOut);
public record ChangePasswordResultDto(bool Changed);
public record ChangeUsernameResultDto(string Username);
public record UserSummaryDto(string Username, bool IsAdmin);
public record ResetResultDto(bool Reset);
public record CreateUserResultDto(bool Created);
public record UpdateUserResultDto(string Username);
public record DeleteUserResultDto(bool Deleted);
public record DeletePromptResultDto(bool Deleted);
public record WhoAmIResultDto(string Username, bool IsAdmin);
