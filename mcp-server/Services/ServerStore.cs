using PromptVcs.Core;

namespace PromptVcs.McpServer.Services;

/// <summary>
/// Singleton wrapper around PromptVcs.Core.PromptStore giving the MCP
/// server's tool handlers safe concurrent access to the one store file this
/// process owns (previously the CLI's local .promptvcs/store.json — now
/// server-side, since the CLI is a thin client).
/// </summary>
public class ServerStore
{
    private readonly string _dataDir;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Store? _cache;

    public ServerStore(IWebHostEnvironment env)
    {
        _dataDir = env.ContentRootPath;
    }

    /// <summary>Initializes the store if needed. Returns whether it was already initialized.</summary>
    public async Task<bool> InitAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var alreadyInitialized = PromptStore.IsInitialized(_dataDir);
            _cache = alreadyInitialized ? PromptStore.Load(_dataDir) : PromptStore.Init(_dataDir);
            return alreadyInitialized;
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
            return action(LoadOrInit());
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
            var store = LoadOrInit();
            try
            {
                return await action(store);
            }
            finally
            {
                // Always persist, even on failure — a partially-advanced
                // pipeline (e.g. a recorded QA checkpoint before a later
                // step throws) should still be saved, matching the old
                // CLI's "always save in finally" behavior.
                PromptStore.Save(store, _dataDir);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private Store LoadOrInit()
    {
        if (_cache != null) return _cache;
        _cache = PromptStore.IsInitialized(_dataDir) ? PromptStore.Load(_dataDir) : PromptStore.Init(_dataDir);
        return _cache;
    }
}
