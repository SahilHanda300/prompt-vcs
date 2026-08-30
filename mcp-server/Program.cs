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

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<PromptTools>();

var app = builder.Build();

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

app.MapGet("/", () => "PromptVCS MCP server is running. MCP endpoint: /mcp, runner hub: /runnerhub, artifacts: /site/<promptId>/");

// Shared-secret auth on the CLI-facing MCP endpoint — anyone who can reach
// this server otherwise has full read/write access to every prompt, so this
// matters once the server is exposed publicly, not just for the Runner hub.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp"))
    {
        var expectedToken = Environment.GetEnvironmentVariable("PROMPTVCS_API_TOKEN");
        if (!string.IsNullOrEmpty(expectedToken))
        {
            var providedToken = context.Request.Headers["X-PromptVCS-Token"].ToString();
            if (providedToken != expectedToken)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }
        }
    }
    await next();
});

app.MapMcp("/mcp");
app.MapHub<RunnerHub>("/runnerhub");

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
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROMPTVCS_RUNNER_TOKEN")))
    {
        WriteWarning("  Warning: PROMPTVCS_RUNNER_TOKEN is not set — any runner can connect.");
    }
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROMPTVCS_API_TOKEN")))
    {
        WriteWarning("  Warning: PROMPTVCS_API_TOKEN is not set — anyone who can reach this server has full access to the store.");
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
