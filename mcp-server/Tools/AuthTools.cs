using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using ModelContextProtocol.Server;
using PromptVcs.Core;
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
    private readonly ServerStore _store;

    public AuthTools(MongoAuthService auth, ServerStore store)
    {
        _auth = auth;
        _store = store;
    }

    /// Every prompt owned by oldUsername now belongs to newUsername instead
    /// — without this, a rename (self-service or admin-driven) would
    /// silently orphan that user's own prompts (Owner would still hold a
    /// username that no longer exists). No-op if oldUsername is null (no
    /// rename happened) or equals newUsername.
    private Task CascadeOwnerRenameAsync(string? oldUsername, string? newUsername)
    {
        if (oldUsername == null || newUsername == null || oldUsername == newUsername) return Task.CompletedTask;
        return _store.MutateAsync<object?>(store =>
        {
            PromptService.RenameOwner(store, oldUsername, newUsername);
            return Task.FromResult<object?>(null);
        });
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

    [McpServerTool(Name = "whoAmI"), Description(
        "Returns the current username and role for a session token, re-checked live against " +
        "the database rather than anything cached by the caller. Used to catch a role/username " +
        "change (e.g. an admin demoting this user) that happened after the caller's own copy " +
        "of that information was issued.")]
    public async Task<string> WhoAmI([Description("Session token")] string? sessionToken)
    {
        if (!_auth.IsConfigured) return Error("Server has no MongoDB connection configured (PROMPTVCS_MONGO_URI unset).");

        var (valid, username, isAdmin) = await _auth.ValidateSessionAsync(sessionToken);
        if (!valid) return Error("Not logged in. Run \"login\" first.");

        return Ok(new { username, isAdmin });
    }

    [McpServerTool(Name = "listUsers"), Description("Lists all registered users (username and role only). Admin only.")]
    public async Task<string> ListUsers([Description("Session token")] string? sessionToken)
    {
        if (!_auth.IsConfigured) return Error("Server has no MongoDB connection configured (PROMPTVCS_MONGO_URI unset).");

        var (valid, _, isAdmin) = await _auth.ValidateSessionAsync(sessionToken);
        if (!valid) return Error("Not logged in. Run \"login\" first.");
        if (!isAdmin) return Error("Admin privileges required for this command.");

        var users = await _auth.ListUsersAsync();
        return Ok(users.Select(u => new { username = u.Username, isAdmin = u.IsAdmin }));
    }

    [McpServerTool(Name = "changePassword"), Description("Changes the logged-in user's password. Requires the current password.")]
    public async Task<string> ChangePassword(
        [Description("Session token")] string? sessionToken,
        [Description("Current password")] string currentPassword,
        [Description("New password (minimum 8 characters)")] string newPassword)
    {
        if (!_auth.IsConfigured) return Error("Server has no MongoDB connection configured (PROMPTVCS_MONGO_URI unset).");

        var (success, message) = await _auth.ChangePasswordAsync(sessionToken, currentPassword, newPassword);
        return success ? Ok(new { changed = true }) : Error(message!);
    }

    [McpServerTool(Name = "changeUsername"), Description("Changes the logged-in user's username. Requires the current password.")]
    public async Task<string> ChangeUsername(
        [Description("Session token")] string? sessionToken,
        [Description("Current password")] string currentPassword,
        [Description("New username")] string newUsername)
    {
        if (!_auth.IsConfigured) return Error("Server has no MongoDB connection configured (PROMPTVCS_MONGO_URI unset).");

        var (_, oldUsername, _) = await _auth.ValidateSessionAsync(sessionToken);
        var (success, message, updatedUsername) = await _auth.ChangeUsernameAsync(sessionToken, currentPassword, newUsername);
        if (!success) return Error(message!);
        await CascadeOwnerRenameAsync(oldUsername, updatedUsername);
        return Ok(new { username = updatedUsername });
    }

    [McpServerTool(Name = "createUser"), Description(
        "Creates a new user account directly — admin only. Unlike \"register\", the caller " +
        "chooses the role and no session is created for the new account.")]
    public async Task<string> CreateUser(
        [Description("Session token")] string? sessionToken,
        [Description("Username for the new account")] string username,
        [Description("Password for the new account (minimum 8 characters)")] string password,
        [Description("Whether the new account is an admin")] bool isAdmin = false)
    {
        if (!_auth.IsConfigured) return Error("Server has no MongoDB connection configured (PROMPTVCS_MONGO_URI unset).");

        var (valid, _, callerIsAdmin) = await _auth.ValidateSessionAsync(sessionToken);
        if (!valid) return Error("Not logged in. Run \"login\" first.");
        if (!callerIsAdmin) return Error("Admin privileges required for this command.");

        var (success, message) = await _auth.AdminCreateUserAsync(username, password, isAdmin);
        return success ? Ok(new { created = true }) : Error(message!);
    }

    [McpServerTool(Name = "updateUser"), Description(
        "Updates another user's username, password, and/or admin role — admin only. Any field " +
        "left out is unchanged. Refuses to remove admin from the last remaining admin.")]
    public async Task<string> UpdateUser(
        [Description("Session token")] string? sessionToken,
        [Description("Username of the account to update")] string targetUsername,
        [Description("New username, or omit to leave unchanged")] string? newUsername = null,
        [Description("New password (minimum 8 characters), or omit to leave unchanged")] string? newPassword = null,
        [Description("New admin role, or omit to leave unchanged")] bool? isAdmin = null)
    {
        if (!_auth.IsConfigured) return Error("Server has no MongoDB connection configured (PROMPTVCS_MONGO_URI unset).");

        var (valid, _, callerIsAdmin) = await _auth.ValidateSessionAsync(sessionToken);
        if (!valid) return Error("Not logged in. Run \"login\" first.");
        if (!callerIsAdmin) return Error("Admin privileges required for this command.");

        var (success, message, renamedFrom, renamedTo) = await _auth.AdminUpdateUserAsync(targetUsername, newUsername, newPassword, isAdmin);
        if (!success) return Error(message!);
        await CascadeOwnerRenameAsync(renamedFrom, renamedTo);
        return Ok(new { username = renamedTo ?? targetUsername });
    }

    [McpServerTool(Name = "deleteUser"), Description(
        "Permanently deletes a user account and logs out all of their sessions — admin only. " +
        "Refuses to delete your own account or the last remaining admin. Does not delete that " +
        "user's prompts (use deletePrompt/admin tools for those separately).")]
    public async Task<string> DeleteUser(
        [Description("Session token")] string? sessionToken,
        [Description("Username of the account to delete")] string targetUsername)
    {
        if (!_auth.IsConfigured) return Error("Server has no MongoDB connection configured (PROMPTVCS_MONGO_URI unset).");

        var (valid, callerUsername, callerIsAdmin) = await _auth.ValidateSessionAsync(sessionToken);
        if (!valid) return Error("Not logged in. Run \"login\" first.");
        if (!callerIsAdmin) return Error("Admin privileges required for this command.");

        var (success, message) = await _auth.AdminDeleteUserAsync(targetUsername, callerUsername!);
        return success ? Ok(new { deleted = true }) : Error(message!);
    }

    private static string Ok(object data) => JsonSerializer.Serialize(new { status = "ok", data }, JsonOptions);
    private static string Error(string message) => JsonSerializer.Serialize(new { status = "error", message }, JsonOptions);
}
