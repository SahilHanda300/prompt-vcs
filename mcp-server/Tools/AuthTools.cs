using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using PromptVcs.McpServer.Services;

namespace PromptVcs.McpServer.Tools;

/// <summary>
/// Login/logout/register — the CLI's identity now comes from MongoDB, not a
/// shared token. Same {"status":"ok"|"error",...} envelope as PromptTools.
/// </summary>
[McpServerToolType]
public class AuthTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly MongoAuthService _auth;

    public AuthTools(MongoAuthService auth)
    {
        _auth = auth;
    }

    [McpServerTool(Name = "login"), Description("Logs in with a username and password, returning a session token.")]
    public async Task<string> Login(
        [Description("Username")] string username,
        [Description("Password")] string password)
    {
        if (!_auth.IsConfigured) return Error("Server has no MongoDB connection configured (PROMPTVCS_MONGO_URI unset).");

        var (success, message, sessionToken, isAdmin) = await _auth.LoginAsync(username, password);
        return success ? Ok(new { sessionToken, isAdmin }) : Error(message!);
    }

    [McpServerTool(Name = "logout"), Description("Invalidates a session token.")]
    public async Task<string> Logout([Description("Session token")] string? sessionToken)
    {
        if (!_auth.IsConfigured) return Error("Server has no MongoDB connection configured (PROMPTVCS_MONGO_URI unset).");

        await _auth.LogoutAsync(sessionToken);
        return Ok(new { loggedOut = true });
    }

    [McpServerTool(Name = "register"), Description(
        "Creates a new user — open to anyone. The very first user ever registered " +
        "becomes an admin automatically; everyone after that registers as a normal user.")]
    public async Task<string> Register(
        [Description("Username")] string username,
        [Description("Password")] string password)
    {
        if (!_auth.IsConfigured) return Error("Server has no MongoDB connection configured (PROMPTVCS_MONGO_URI unset).");

        var (success, message, sessionToken, isAdmin) = await _auth.RegisterAsync(username, password);
        return success ? Ok(new { sessionToken, isAdmin }) : Error(message!);
    }

    private static string Ok(object data) => JsonSerializer.Serialize(new { status = "ok", data }, JsonOptions);
    private static string Error(string message) => JsonSerializer.Serialize(new { status = "error", message }, JsonOptions);
}
