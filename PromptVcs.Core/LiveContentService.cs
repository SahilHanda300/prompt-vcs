using System.Collections.Concurrent;

namespace PromptVcs.Core;

/// <summary>
/// Real implementation of ILiveContentService. A literal "call Claude Code
/// on every single page view" would mean every visitor (and every reload,
/// and every open tab) spends one Claude Pro usage unit and several seconds
/// of latency — so this still goes through the Runner on each visit, but a
/// short in-memory per-prompt cache collapses concurrent/rapid visits onto
/// one call instead of one each. PROMPTVCS_LIVE_REFRESH_SECONDS tunes how
/// "live" that is; the default favors not exhausting the developer's own
/// Claude Pro usage over sub-minute freshness.
///
/// One SemaphoreSlim per prompt id, same pattern as ServerStore's
/// single-writer lock elsewhere in this project — double-checks the cache
/// after acquiring the lock so N concurrent visitors during a cache miss
/// trigger exactly one Claude Code call, not N.
/// </summary>
public class LiveContentService : ILiveContentService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(
        int.TryParse(Environment.GetEnvironmentVariable("PROMPTVCS_LIVE_REFRESH_SECONDS"), out var seconds) ? seconds : 60);

    private readonly IClaudeCodeInvoker _invoker;
    private readonly IGenerationPromptBuilder _promptBuilder;
    private readonly IArtifactSanitizer _sanitizer;

    private readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresAt, string Html)> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public LiveContentService(IClaudeCodeInvoker invoker, IGenerationPromptBuilder promptBuilder, IArtifactSanitizer sanitizer)
    {
        _invoker = invoker;
        _promptBuilder = promptBuilder;
        _sanitizer = sanitizer;
    }

    public async Task<string?> GetFreshHtmlAsync(PromptRecord record, string originalContent, string cachedHtml, CancellationToken ct = default)
    {
        if (TryGetFresh(record.Id, out var fresh))
        {
            return fresh;
        }

        var gate = _locks.GetOrAdd(record.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            // Re-check: another visitor may have just finished the refresh
            // this one was waiting on.
            if (TryGetFresh(record.Id, out fresh))
            {
                return fresh;
            }

            var prompt = _promptBuilder.BuildLiveRefreshPrompt(record.Name, originalContent, cachedHtml);
            var result = await _invoker.InvokeAsync(prompt, ct: ct);
            if (!result.Ok)
            {
                return null;
            }

            var sanitized = _sanitizer.Sanitize(result.Text);
            if (!sanitized.Ok)
            {
                return null;
            }

            _cache[record.Id] = (DateTimeOffset.UtcNow.Add(Ttl), sanitized.Html);
            return sanitized.Html;
        }
        finally
        {
            gate.Release();
        }
    }

    private bool TryGetFresh(string id, out string html)
    {
        if (_cache.TryGetValue(id, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            html = entry.Html;
            return true;
        }
        html = string.Empty;
        return false;
    }
}
