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
    /// <param name="model">
    /// Optional Claude Code model alias ("haiku", "sonnet", "opus", ...).
    /// Null uses Claude Code's own default. Measured: haiku is NOT faster
    /// for a large single-shot full-page generation (it tends to produce
    /// more, not less, output for that shape of task) but is roughly 2x
    /// faster for a small, tightly-scoped task like a planning call or one
    /// section of a page — callers should pick per call-shape, not globally.
    /// </param>
    Task<ClaudeCodeResult> InvokeAsync(string prompt, IReadOnlyList<Attachment>? attachments = null, string? model = null, CancellationToken ct = default);
}
