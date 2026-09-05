using Microsoft.Extensions.FileProviders;
using PromptVcs.Core;
using PromptVcs.McpServer.Hubs;
using PromptVcs.McpServer.Services;
using PromptVcs.McpServer.Tools;

const string DefaultPort = "5279";
// Render (and most PaaS hosts) inject PORT and expect the app to bind it;
// PROMPTVCS_MCP_PORT stays as a manual override for local/other-host use.
var port = Environment.GetEnvironmentVariable("PORT")
    ?? Environment.GetEnvironmentVariable("PROMPTVCS_MCP_PORT")
    ?? DefaultPort;

var builder = WebApplication.CreateBuilder(args);
// 0.0.0.0, not localhost — localhost only accepts loopback connections, so
// a container's own healthcheck/reverse proxy (or anyone outside the
// container) couldn't reach the app at all if it bound to localhost.
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddSignalR();

builder.Services.AddSingleton<MongoDatabaseProvider>();
builder.Services.AddSingleton<ServerStore>();
builder.Services.AddSingleton<RunnerRegistry>();
builder.Services.AddSingleton<RunnerDispatchInvoker>();
builder.Services.AddSingleton<IClaudeCodeInvoker>(sp => sp.GetRequiredService<RunnerDispatchInvoker>());
builder.Services.AddSingleton<Qa>();
builder.Services.AddSingleton(sp => new PublishRules(
    sp.GetRequiredService<IClaudeCodeInvoker>(),
    Path.Combine(builder.Environment.ContentRootPath, "site")));
builder.Services.AddSingleton<Pipeline>();
builder.Services.AddSingleton<PromptService>();
builder.Services.AddSingleton<MongoAuthService>();

var cliDllPath = Environment.GetEnvironmentVariable("PROMPTVCS_CLI_PATH") ?? "/app/cli/prompt-vcs.dll";
builder.Services.AddSingleton(_ => new TerminalSessionManager(cliDllPath, int.Parse(port)));

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<PromptTools>()
    .WithTools<AuthTools>();

var app = builder.Build();

app.UseWebSockets();
app.UseStaticFiles(); // default wwwroot — serves wwwroot/terminal.html at /terminal.html

var siteDir = Path.Combine(app.Environment.ContentRootPath, "site");
Directory.CreateDirectory(siteDir);

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = new PhysicalFileProvider(siteDir),
    RequestPath = "/site",
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(siteDir),
    RequestPath = "/site",
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.ContentType = "text/html; charset=utf-8";
        }
    },
});

app.MapGet("/", () => "PromptVCS MCP server is running. MCP endpoint: /mcp, runner hub: /runnerhub, artifacts: /site/<promptId>/, terminal: /terminal");
app.MapGet("/terminal", () => Results.Redirect("/terminal.html"));

// CLI-facing auth on /mcp is now per-user, via MongoDB-backed login (see
// AuthTools/MongoAuthService) rather than a single shared token: every tool
// except login/register validates a sessionToken argument itself. There is
// deliberately no blanket gate in front of /mcp anymore — login/register
// have to be reachable before a caller has any session to present.
app.MapMcp("/mcp");
app.MapHub<RunnerHub>("/runnerhub");

app.Map("/terminal-ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    // No token gate here anymore — the spawned CLI's own register/login
    // (MongoDB-backed) is the real gate now, same as /mcp. Anyone can open a
    // terminal session, but they can't do anything beyond register/login
    // without real credentials.
    var terminalSessions = context.RequestServices.GetRequiredService<TerminalSessionManager>();
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await terminalSessions.RunSessionAsync(socket, context.RequestAborted);
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    var previous = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("PromptVCS MCP Server Live!");
    Console.ForegroundColor = previous;
    var publicUrl = Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL") ?? $"http://localhost:{port}";
    Console.WriteLine($"  MCP endpoint: {publicUrl}/mcp");
    Console.WriteLine($"  Runner hub:   {publicUrl}/runnerhub");
    Console.WriteLine($"  Artifacts:    {publicUrl}/site/<promptId>/");
    Console.WriteLine($"  Terminal:     {publicUrl}/terminal");
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROMPTVCS_RUNNER_TOKEN")))
    {
        WriteWarning("  Warning: PROMPTVCS_RUNNER_TOKEN is not set — any runner can connect.");
    }
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROMPTVCS_MONGO_URI")))
    {
        WriteWarning("  Warning: PROMPTVCS_MONGO_URI is not set — MongoDB now backs both auth and the prompt store itself, so every command will fail.");
    }
});

static void WriteWarning(string text)
{
    var previous = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(text);
    Console.ForegroundColor = previous;
}

app.Run();
