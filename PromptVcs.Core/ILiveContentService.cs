namespace PromptVcs.Core;

/// <summary>
/// Keeps every published site's root fresh by re-running it through Claude
/// Code on demand, instead of serving the one static snapshot taken at
/// generation time. See mcp-server/Program.cs for where this is called from
/// (a request-intercepting middleware in front of the static /site file
/// server) — a specific /site/{id}/v{n}/ build still serves its original
/// static snapshot, only the "latest" root alias goes through here.
/// </summary>
public interface ILiveContentService
{
    /// Returns freshly rendered HTML for <paramref name="record"/>, or null
    /// if a refresh was needed but failed (no Runner connected, the call
    /// timed out, the response didn't sanitize) — callers should fall back
    /// to serving <paramref name="cachedHtml"/> as-is in that case rather
    /// than erroring, same "clean failure" policy the rest of this project
    /// uses when the Runner is unavailable.
    Task<string?> GetFreshHtmlAsync(PromptRecord record, string originalContent, string cachedHtml, CancellationToken ct = default);
}
