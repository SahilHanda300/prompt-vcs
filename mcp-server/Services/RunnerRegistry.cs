namespace PromptVcs.McpServer.Services;

/// <summary>
/// Tracks the single currently-connected runner's SignalR connection id.
/// Single-runner/single-user for now — see CLAUDE.md's open "multi-user
/// story" question for what happens when there's more than one.
/// </summary>
public class RunnerRegistry
{
    public string? CurrentConnectionId { get; set; }
}
