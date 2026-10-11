using Microsoft.AspNetCore.SignalR.Client;
using PromptVcs.Core;

var hubUrl = Environment.GetEnvironmentVariable("PROMPTVCS_RUNNER_HUB_URL") ?? "http://localhost:5279/runnerhub";
var token = Environment.GetEnvironmentVariable("PROMPTVCS_RUNNER_TOKEN");
var invoker = new ClaudeCodeInvoker();

var urlWithToken = string.IsNullOrEmpty(token) ? hubUrl : $"{hubUrl}?token={Uri.EscapeDataString(token)}";

// ServerTimeout/KeepAliveInterval raised well past SignalR's own 30s
// defaults, to match RunnerDispatchInvoker's 5-minute application-level job
// timeout — a real generation call (especially opus, or anything with
// attachments) can legitimately run longer than 30s, and the default
// would have the transport itself drop the connection mid-job before our
// own timeout ever gets a chance to. KeepAliveInterval stays well under
// ServerTimeout, as SignalR requires.
var connection = new HubConnectionBuilder()
    .WithUrl(urlWithToken)
    .WithAutomaticReconnect()
    .WithServerTimeout(TimeSpan.FromMinutes(6))
    .WithKeepAliveInterval(TimeSpan.FromSeconds(15))
    .Build();

connection.On<string, string, List<Attachment>, string?>("RunJob", (jobId, prompt, attachments, model) =>
{
    // Fired off, not awaited, deliberately: SignalR's client-side message
    // loop processes `.On` handlers inline, so awaiting a long Claude Code
    // call directly here would block the connection from sending its own
    // keep-alive pings for the whole duration — a real, measured cause of
    // "Connection closed with an error" on real generation calls (first
    // seen reliably once calls got slower after switching to opus). Firing
    // the work to the background and returning immediately keeps the
    // connection's own loop free regardless of how long the job takes.
    _ = RunJobAsync(jobId, prompt, attachments, model);
    return Task.CompletedTask;
});

async Task RunJobAsync(string jobId, string prompt, List<Attachment> attachments, string? model)
{
    WriteLine($"Running job {jobId}{(attachments.Count > 0 ? $" ({attachments.Count} attachment(s))" : "")}{(model != null ? $" [model={model}]" : "")}...", ConsoleColor.Cyan);
    var result = await invoker.InvokeAsync(prompt, attachments, model);
    try
    {
        await connection.InvokeAsync("ReportResult", jobId, result.Ok, result.Text, result.UsageLimitHit, result.Detail);
        WriteLine($"Job {jobId} reported: {(result.Ok ? "ok" : "failed")}.", result.Ok ? ConsoleColor.Green : ConsoleColor.Red);
    }
    catch (Exception ex)
    {
        WriteLine($"Failed to report result for job {jobId}: {ex.Message}", ConsoleColor.Red);
    }
}

connection.Reconnecting += _ =>
{
    WriteLine("Connection lost, reconnecting...", ConsoleColor.Yellow);
    return Task.CompletedTask;
};
connection.Reconnected += _ =>
{
    WriteLine("Reconnected.", ConsoleColor.Green);
    return Task.CompletedTask;
};
connection.Closed += async error =>
{
    WriteLine($"Connection closed{(error != null ? $": {error.Message}" : "")}. Retrying in 5s...", ConsoleColor.Red);
    await Task.Delay(5000);
    try
    {
        await connection.StartAsync();
    }
    catch (Exception ex)
    {
        WriteLine($"Reconnect attempt failed: {ex.Message}", ConsoleColor.Red);
    }
};

try
{
    await connection.StartAsync();
}
catch (Exception ex)
{
    WriteLine($"Could not connect to {hubUrl}: {ex.Message}", ConsoleColor.Red);
    return 1;
}

WriteLine("PromptVCS Runner connected!", ConsoleColor.Green);
Console.WriteLine($"  Hub: {hubUrl}");
Console.WriteLine("  Waiting for jobs. Press Ctrl+C to stop.");
if (Environment.GetEnvironmentVariable("PROMPTVCS_MOCK_CLAUDE") == "1")
{
    WriteLine("  PROMPTVCS_MOCK_CLAUDE=1 — jobs will be answered with canned output, not real Claude Code calls.", ConsoleColor.Yellow);
}

var exitSignal = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    exitSignal.TrySetResult();
};
await exitSignal.Task;
await connection.DisposeAsync();
return 0;

static void WriteLine(string text, ConsoleColor color)
{
    var previous = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ForegroundColor = previous;
}
