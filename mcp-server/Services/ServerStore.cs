using System.Text.Json;
using MongoDB.Driver;
using PromptVcs.Core;

namespace PromptVcs.McpServer.Services;

/// <summary>
/// Singleton wrapper giving the MCP server's tool handlers safe concurrent
/// access to the one prompt store this process owns. Backed by a single
/// MongoDB document — not a JSON file on disk (that's gone, see CLAUDE.md),
/// and deliberately not one document per prompt either: the whole Store
/// graph is serialized with the same System.Text.Json shape
/// PromptService/Pipeline/Qa already operate on in memory, and that blob is
/// stored as one field on one document. This preserves the exact
/// concurrency model the file-based version had (single in-process lock,
/// one blob, no per-process partitioning) rather than introducing a new
/// one — see "Multi-instance scaling" in CLAUDE.md for the limitation that
/// still carries forward unchanged.
/// </summary>
public class ServerStore
{
    private const string DocumentId = "singleton";

    private readonly IMongoCollection<StoreDocument>? _collection;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Store? _cache;

    public ServerStore(MongoDatabaseProvider provider)
    {
        _collection = provider.Database?.GetCollection<StoreDocument>("app_store");
    }

    /// <summary>Wipes every prompt, checkpoint, and build record back to empty.</summary>
    public async Task ResetAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _cache = new Store();
            await SaveAsync(_cache);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T> ReadAsync<T>(Func<Store, T> action)
    {
        await _lock.WaitAsync();
        try
        {
            return action(await LoadAsync());
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<T> MutateAsync<T>(Func<Store, Task<T>> action)
    {
        await _lock.WaitAsync();
        try
        {
            var store = await LoadAsync();
            try
            {
                return await action(store);
            }
            finally
            {
                // Always persist, even on failure — a partially-advanced
                // pipeline (e.g. a recorded QA checkpoint before a later
                // step throws) should still be saved, matching the old
                // file-based store's "always save in finally" behavior.
                await SaveAsync(store);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Store> LoadAsync()
    {
        if (_cache != null) return _cache;

        var doc = await RequireCollection().Find(d => d.Id == DocumentId).FirstOrDefaultAsync();
        _cache = doc != null ? JsonSerializer.Deserialize<Store>(doc.Json) ?? new Store() : new Store();
        return _cache;
    }

    private async Task SaveAsync(Store store)
    {
        var doc = new StoreDocument { Id = DocumentId, Json = JsonSerializer.Serialize(store) };
        await RequireCollection().ReplaceOneAsync(d => d.Id == DocumentId, doc, new ReplaceOptions { IsUpsert = true });
    }

    private IMongoCollection<StoreDocument> RequireCollection() =>
        _collection ?? throw new InvalidOperationException("PROMPTVCS_MONGO_URI is not configured on the server.");
}
