namespace PromptVcs.Core;

/// <summary>
/// Abstraction over "run this prompt through Claude Code and get a result back."
/// <see cref="ClaudeCodeInvoker"/> is the real, local-subprocess implementation used
/// by the Runner (the only process that ever has Claude Pro access). The MCP server
/// runs remotely and has no local Claude Code access, so it implements this interface
/// with a dispatcher that forwards the call to whichever Runner is connected — from
/// <see cref="Qa"/> and <see cref="PublishRules"/>'s point of view, both look identical.
/// </summary>
public interface IClaudeCodeInvoker
{
    Task<ClaudeCodeResult> InvokeAsync(string prompt, CancellationToken ct = default);
}
