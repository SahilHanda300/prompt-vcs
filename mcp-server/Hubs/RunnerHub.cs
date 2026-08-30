using Microsoft.AspNetCore.SignalR;
using PromptVcs.Core;
using PromptVcs.McpServer.Services;

namespace PromptVcs.McpServer.Hubs;

/// <summary>
/// The Runner connects here and stays connected. The server pushes Claude
/// Code jobs ("RunJob") to the connected client; the Runner calls back
/// ReportResult with the outcome. Auth is a shared secret
/// (PROMPTVCS_RUNNER_TOKEN) checked on connect — an interim, single-user
/// answer; see CLAUDE.md's open "runner agent authentication" question for
/// what a real multi-user scheme would need.
/// </summary>
public class RunnerHub : Hub
{
    private readonly RunnerRegistry _registry;
    private readonly RunnerDispatchInvoker _dispatcher;

    public RunnerHub(RunnerRegistry registry, RunnerDispatchInvoker dispatcher)
    {
        _registry = registry;
        _dispatcher = dispatcher;
    }

    public override Task OnConnectedAsync()
    {
        var expectedToken = Environment.GetEnvironmentVariable("PROMPTVCS_RUNNER_TOKEN");
        var providedToken = Context.GetHttpContext()?.Request.Query["token"].ToString();

        if (!string.IsNullOrEmpty(expectedToken) && providedToken != expectedToken)
        {
            WriteLine($"Rejected runner connection ({Context.ConnectionId}): bad or missing token.", ConsoleColor.Red);
            Context.Abort();
            return Task.CompletedTask;
        }

        _registry.CurrentConnectionId = Context.ConnectionId;
        WriteLine($"Runner connected ({Context.ConnectionId}).", ConsoleColor.Green);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (_registry.CurrentConnectionId == Context.ConnectionId)
        {
            _registry.CurrentConnectionId = null;
            WriteLine("Runner disconnected.", ConsoleColor.Yellow);
        }
        return base.OnDisconnectedAsync(exception);
    }

    public void ReportResult(string jobId, bool ok, string text, bool usageLimitHit, string? detail)
    {
        _dispatcher.CompleteJob(jobId, new ClaudeCodeResult(ok, text, usageLimitHit, detail));
    }

    private static void WriteLine(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }
}
