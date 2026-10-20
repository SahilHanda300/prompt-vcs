using PromptVcs.Core;

namespace PromptVcs.McpServer.Services;

/// <summary>
/// Safe concurrent access to the one prompt store this process owns.
/// ReadAsync/MutateAsync take the caller's logic as a delegate so every
/// read/write goes through the same lock-load-(mutate)-save sequence,
/// without IPromptService or the MCP tools needing to know anything about
/// locking or persistence themselves.
/// </summary>
public interface IServerStore
{
    Task ResetAsync();
    Task<T> ReadAsync<T>(Func<Store, T> action);
    Task<T> MutateAsync<T>(Func<Store, Task<T>> action);
}
