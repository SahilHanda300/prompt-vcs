namespace PromptVcs.Core;

public record ArtifactWriteResult(string RelativePath);

/// <summary>
/// Abstraction over persisting a finalized (already-sanitized) artifact to
/// disk, and reading back a prompt's previous artifact for the diff-aware
/// update path. Single responsibility: file I/O only — knows nothing about
/// Claude Code, prompts, or sanitization rules.
/// </summary>
public interface IArtifactWriter
{
    /// Writes both a versioned copy (`/site/{id}/v{buildVersion}/index.html`)
    /// and overwrites the shared "latest" copy (`/site/{id}/index.html`).
    /// Returns the versioned copy's relative URL — build history links to
    /// that specific build, not whatever is currently live.
    Task<ArtifactWriteResult> WriteAsync(string promptId, int buildVersion, string html);

    /// Null if no previous artifact exists on disk yet (first run, or the
    /// container's disk was wiped since the last successful build).
    Task<string?> ReadLatestAsync(string promptId);
}
