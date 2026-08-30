using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using PromptVcs.Core;
using PromptVcs.McpServer.Hubs;

namespace PromptVcs.McpServer.Services;

/// <summary>
/// Server-side IClaudeCodeInvoker: the MCP server has no local Claude Code
/// access (it's meant to run remotely), so instead of shelling out it
/// dispatches the prompt to whichever Runner is connected over the
/// RunnerHub and awaits the matching ReportResult call. From Qa/PublishRules'
/// point of view this looks exactly like the Runner's real ClaudeCodeInvoker
/// — same interface, same result shape, including failure results (no
/// runner connected, timeout) rather than exceptions, so it flows through
/// their existing failure handling unchanged.
/// </summary>
public class RunnerDispatchInvoker : IClaudeCodeInvoker
{
    private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(5);

    private readonly IHubContext<RunnerHub> _hubContext;
    private readonly RunnerRegistry _registry;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ClaudeCodeResult>> _pending = new();

    public RunnerDispatchInvoker(IHubContext<RunnerHub> hubContext, RunnerRegistry registry)
    {
        _hubContext = hubContext;
        _registry = registry;
    }

    public async Task<ClaudeCodeResult> InvokeAsync(string prompt, CancellationToken ct = default)
    {
        var connectionId = _registry.CurrentConnectionId;
        if (connectionId == null)
        {
            return new ClaudeCodeResult(false, "", false, "No runner connected.");
        }

        var jobId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<ClaudeCodeResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[jobId] = tcs;

        using var timeoutCts = new CancellationTokenSource(JobTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        await using var registration = linkedCts.Token.Register(() => tcs.TrySetResult(
            new ClaudeCodeResult(false, "", false, "Runner did not respond in time.")));

        try
        {
            await _hubContext.Clients.Client(connectionId).SendAsync("RunJob", jobId, prompt, ct);
            return await tcs.Task;
        }
        finally
        {
            _pending.TryRemove(jobId, out _);
        }
    }

    /// <summary>Called by RunnerHub when the runner reports a job's result.</summary>
    public void CompleteJob(string jobId, ClaudeCodeResult result)
    {
        if (_pending.TryGetValue(jobId, out var tcs))
        {
            tcs.TrySetResult(result);
        }
    }
}
