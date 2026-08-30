using Microsoft.AspNetCore.SignalR.Client;
using PromptVcs.Core;

var hubUrl = Environment.GetEnvironmentVariable("PROMPTVCS_RUNNER_HUB_URL") ?? "http://localhost:5279/runnerhub";
var token = Environment.GetEnvironmentVariable("PROMPTVCS_RUNNER_TOKEN");
var invoker = new ClaudeCodeInvoker();

var urlWithToken = string.IsNullOrEmpty(token) ? hubUrl : $"{hubUrl}?token={Uri.EscapeDataString(token)}";

var connection = new HubConnectionBuilder()
    .WithUrl(urlWithToken)
    .WithAutomaticReconnect()
    .Build();

connection.On<string, string>("RunJob", async (jobId, prompt) =>
{
    WriteLine($"Running job {jobId}...", ConsoleColor.Cyan);
    var result = await invoker.InvokeAsync(prompt);
    try
    {
        await connection.InvokeAsync("ReportResult", jobId, result.Ok, result.Text, result.UsageLimitHit, result.Detail);
        WriteLine($"Job {jobId} reported: {(result.Ok ? "ok" : "failed")}.", result.Ok ? ConsoleColor.Green : ConsoleColor.Red);
    }
    catch (Exception ex)
    {
        WriteLine($"Failed to report result for job {jobId}: {ex.Message}", ConsoleColor.Red);
    }
});

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
