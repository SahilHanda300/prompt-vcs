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

// ClientTimeoutInterval/KeepAliveInterval raised past SignalR's 30s
// defaults to match the Runner's own HubConnectionBuilder settings and
// RunnerDispatchInvoker's 5-minute application-level job timeout — both
// sides of a SignalR connection enforce their own timeout independently,
// so raising only the client's would still leave the server free to drop
// the connection first. See PromptVcs.Runner/Program.cs for the matching
// client-side config and why this was needed (a long opus/attachment
// generation call was reliably triggering "Connection closed with an
// error" once calls started regularly running past ~30s).
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(6);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

builder.Services.AddSingleton<MongoDatabaseProvider>();
builder.Services.AddSingleton<IServerStore, ServerStore>();
builder.Services.AddSingleton<IDataProtectionKeyStore, MongoDataProtectionKeyStore>();
builder.Services.AddSingleton<RunnerRegistry>();
builder.Services.AddSingleton<RunnerDispatchInvoker>();
builder.Services.AddSingleton<IClaudeCodeInvoker>(sp => sp.GetRequiredService<RunnerDispatchInvoker>());

// Core pipeline — each stage depends only on the abstractions it needs
// (IClaudeCodeInvoker, IPromptValidator, IGenerationPromptBuilder,
// IArtifactSanitizer, IArtifactWriter), not on each other's concrete types.
builder.Services.AddSingleton<IPromptValidator, PromptValidator>();
builder.Services.AddSingleton<IGenerationPromptBuilder, GenerationPromptBuilder>();
builder.Services.AddSingleton<IArtifactSanitizer, ArtifactSanitizer>();
builder.Services.AddSingleton<IArtifactWriter>(_ => new ArtifactWriter(Path.Combine(builder.Environment.ContentRootPath, "site")));
builder.Services.AddSingleton<IQaService, Qa>();
builder.Services.AddSingleton<IPublishService, PublishRules>();
builder.Services.AddSingleton<IPipelineService, Pipeline>();
builder.Services.AddSingleton<IPromptService, PromptService>();

// Auth — AuthService composes these four single-purpose collaborators
// instead of doing hashing + password-policy + user storage + session
// storage all in one class.
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IPasswordPolicy, PasswordPolicy>();
builder.Services.AddSingleton<IUserRepository, MongoUserRepository>();
builder.Services.AddSingleton<ISessionRepository, MongoSessionRepository>();
builder.Services.AddSingleton<IAuthService, AuthService>();

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

// Server-to-server only (PromptVcs.Web's Data Protection keyring, see
// IDataProtectionKeyStore) — gated by a shared secret, not a user session,
// since there's no logged-in user at the point Web needs to read/write its
// own keyring. Not a security boundary against a determined attacker who
// also has the secret, same trust model as PROMPTVCS_RUNNER_TOKEN.
bool InternalRequestAuthorized(HttpContext ctx)
{
    var expected = Environment.GetEnvironmentVariable("PROMPTVCS_INTERNAL_SECRET");
    if (string.IsNullOrEmpty(expected)) return false;
    return ctx.Request.Headers["X-Internal-Secret"] == expected;
}

app.MapGet("/internal/dataprotection-keys", async (HttpContext ctx, IDataProtectionKeyStore store) =>
{
    if (!InternalRequestAuthorized(ctx)) return Results.Unauthorized();
    return Results.Ok(await store.GetAllAsync());
});
app.MapPost("/internal/dataprotection-keys", async (HttpContext ctx, IDataProtectionKeyStore store, DataProtectionKeyUpload body) =>
{
    if (!InternalRequestAuthorized(ctx)) return Results.Unauthorized();
    await store.StoreAsync(body.Id, body.Xml);
    return Results.Ok();
});

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
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROMPTVCS_INTERNAL_SECRET")))
    {
        WriteWarning("  Warning: PROMPTVCS_INTERNAL_SECRET is not set — PromptVcs.Web can't persist its Data Protection keyring, so it will stay ephemeral.");
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

record DataProtectionKeyUpload(string Id, string Xml);
