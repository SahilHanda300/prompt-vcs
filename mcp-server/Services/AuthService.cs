using System.Security.Cryptography;

namespace PromptVcs.McpServer.Services;

/// <summary>
/// Registration is open to anyone — the very first user ever registered
/// becomes admin automatically; everyone after that registers as a normal
/// (non-admin) user, no invitation or approval needed.
/// </summary>
public class AuthService : IAuthService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    private readonly IUserRepository _users;
    private readonly ISessionRepository _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly IPasswordPolicy _passwordPolicy;

    public AuthService(IUserRepository users, ISessionRepository sessions, IPasswordHasher hasher, IPasswordPolicy passwordPolicy)
    {
        _users = users;
        _sessions = sessions;
        _hasher = hasher;
        _passwordPolicy = passwordPolicy;
    }

    public bool IsConfigured => _users.IsConfigured;

    /// Admin-only (caller enforces — see AuthTools.ListUsers). Just
    /// username + role, never the password hash.
    public async Task<List<(string Username, bool IsAdmin)>> ListUsersAsync()
    {
        var users = await _users.ListAllAsync();
        return users.Select(u => (u.Username, u.IsAdmin)).ToList();
    }

    public async Task<(bool Success, string? Message, string? SessionToken, bool IsAdmin)> RegisterAsync(
        string username, string password)
    {
        var passwordError = _passwordPolicy.Validate(password);
        if (passwordError != null) return (false, passwordError, null, false);

        var isFirstUser = !await _users.AnyAsync();

        var existing = await _users.FindByUsernameAsync(username);
        if (existing != null)
        {
            return (false, $"Username \"{username}\" is already taken.", null, false);
        }

        var user = new UserDocument
        {
            Username = username,
            PasswordHash = _hasher.Hash(password),
            IsAdmin = isFirstUser,
        };
        await _users.InsertAsync(user);

        var sessionToken = await CreateSessionAsync(username, user.IsAdmin);
        return (true, null, sessionToken, user.IsAdmin);
    }

    public async Task<(bool Success, string? Message, string? SessionToken, bool IsAdmin)> LoginAsync(string username, string password)
    {
        var user = await _users.FindByUsernameAsync(username);
        if (user == null || !_hasher.Verify(password, user.PasswordHash))
        {
            return (false, "Invalid username or password.", null, false);
        }

        var sessionToken = await CreateSessionAsync(user.Username, user.IsAdmin);
        return (true, null, sessionToken, user.IsAdmin);
    }

    /// Requires re-entering the current password, same as any sensitive
    /// account change — a 30-day session cookie being valid isn't the same
    /// as the person at the keyboard having just proven who they are.
    public async Task<(bool Success, string? Message)> ChangePasswordAsync(
        string? sessionToken, string currentPassword, string newPassword)
    {
        var (valid, username, _) = await ValidateSessionAsync(sessionToken);
        if (!valid) return (false, "Not logged in. Run \"login\" first.");

        var user = await _users.FindByUsernameAsync(username!);
        if (user == null) return (false, "User not found.");
        if (!_hasher.Verify(currentPassword, user.PasswordHash)) return (false, "Current password is incorrect.");
        var passwordError = _passwordPolicy.Validate(newPassword);
        if (passwordError != null) return (false, passwordError);

        await _users.SetPasswordHashAsync(username!, _hasher.Hash(newPassword));
        return (true, null);
    }

    /// Same current-password reverification as ChangePasswordAsync. Keeps
    /// the caller's own session alive under the new username afterward
    /// rather than forcing an immediate re-login right after a successful
    /// rename — other devices' sessions, if any, go stale until they next
    /// log in, an accepted tradeoff for not needing a bulk session update
    /// here (see AdminUpdateUserAsync, which does need one).
    public async Task<(bool Success, string? Message, string? NewUsername)> ChangeUsernameAsync(
        string? sessionToken, string currentPassword, string newUsername)
    {
        var (valid, username, _) = await ValidateSessionAsync(sessionToken);
        if (!valid) return (false, "Not logged in. Run \"login\" first.", null);

        newUsername = newUsername.Trim();
        if (string.IsNullOrEmpty(newUsername)) return (false, "New username is required.", null);
        if (newUsername == username) return (false, "That's already your username.", null);

        var user = await _users.FindByUsernameAsync(username!);
        if (user == null) return (false, "User not found.", null);
        if (!_hasher.Verify(currentPassword, user.PasswordHash)) return (false, "Current password is incorrect.", null);

        var existing = await _users.FindByUsernameAsync(newUsername);
        if (existing != null) return (false, $"Username \"{newUsername}\" is already taken.", null);

        await _users.SetUsernameAsync(username!, newUsername);
        await _sessions.UpdateUsernameForTokenAsync(sessionToken!, newUsername);

        return (true, null, newUsername);
    }

    /// Admin-only (caller enforces). Unlike self-registration, creates no
    /// session for the new account and doesn't affect "is this the first
    /// user" — an admin can create another admin, or several normal users,
    /// with no bootstrap-ordering implications.
    public async Task<(bool Success, string? Message)> AdminCreateUserAsync(string username, string password, bool isAdmin)
    {
        username = username.Trim();
        if (string.IsNullOrEmpty(username)) return (false, "Username is required.");
        var passwordError = _passwordPolicy.Validate(password);
        if (passwordError != null) return (false, passwordError);

        var existing = await _users.FindByUsernameAsync(username);
        if (existing != null) return (false, $"Username \"{username}\" is already taken.");

        await _users.InsertAsync(new UserDocument { Username = username, PasswordHash = _hasher.Hash(password), IsAdmin = isAdmin });
        return (true, null);
    }

    /// Admin-only (caller enforces). Any of newUsername/newPassword/newIsAdmin
    /// may be omitted to leave that field alone. Refuses to demote the last
    /// remaining admin, to avoid locking every admin feature out with no way
    /// back in short of direct database access.
    public async Task<(bool Success, string? Message, string? RenamedFrom, string? RenamedTo)> AdminUpdateUserAsync(
        string targetUsername, string? newUsername, string? newPassword, bool? newIsAdmin)
    {
        var user = await _users.FindByUsernameAsync(targetUsername);
        if (user == null) return (false, $"User \"{targetUsername}\" not found.", null, null);

        newUsername = string.IsNullOrWhiteSpace(newUsername) ? null : newUsername.Trim();
        if (newUsername == targetUsername) newUsername = null; // not actually a change

        if (newIsAdmin == false && user.IsAdmin)
        {
            var adminCount = await _users.CountAdminsAsync();
            if (adminCount <= 1) return (false, "Can't remove admin from the last remaining admin.", null, null);
        }

        if (newUsername == null && string.IsNullOrEmpty(newPassword) && !newIsAdmin.HasValue)
        {
            return (false, "Nothing to update.", null, null);
        }

        if (newUsername != null)
        {
            var existing = await _users.FindByUsernameAsync(newUsername);
            if (existing != null) return (false, $"Username \"{newUsername}\" is already taken.", null, null);
            await _users.SetUsernameAsync(targetUsername, newUsername);
        }
        if (!string.IsNullOrEmpty(newPassword))
        {
            var passwordError = _passwordPolicy.Validate(newPassword);
            if (passwordError != null) return (false, passwordError, null, null);
            await _users.SetPasswordHashAsync(newUsername ?? targetUsername, _hasher.Hash(newPassword));
        }
        if (newIsAdmin.HasValue)
        {
            await _users.SetIsAdminAsync(newUsername ?? targetUsername, newIsAdmin.Value);
        }

        // Every session for this user, not just one — an admin renaming/
        // re-roling someone else might be changing a user logged in on
        // several devices at once (unlike self-service rename, which only
        // has its own current session to worry about).
        if (newUsername != null)
        {
            await _sessions.UpdateUsernameForAllAsync(targetUsername, newUsername);
        }
        if (newIsAdmin.HasValue)
        {
            await _sessions.UpdateIsAdminForAllAsync(newUsername ?? targetUsername, newIsAdmin.Value);
        }

        return (true, null, targetUsername, newUsername);
    }

    /// Admin-only (caller enforces). Refuses to delete the caller's own
    /// account (use a different admin account for that, or just stop using
    /// this one) and the last remaining admin (would lock every admin
    /// feature out with no way back short of direct database access).
    public async Task<(bool Success, string? Message)> AdminDeleteUserAsync(string targetUsername, string callerUsername)
    {
        if (targetUsername == callerUsername) return (false, "Can't delete your own account while logged in as it.");

        var user = await _users.FindByUsernameAsync(targetUsername);
        if (user == null) return (false, $"User \"{targetUsername}\" not found.");

        if (user.IsAdmin)
        {
            var adminCount = await _users.CountAdminsAsync();
            if (adminCount <= 1) return (false, "Can't delete the last remaining admin.");
        }

        await _users.DeleteAsync(targetUsername);
        await _sessions.DeleteAllForUsernameAsync(targetUsername);
        return (true, null);
    }

    public async Task LogoutAsync(string? sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken)) return;
        await _sessions.DeleteByTokenAsync(sessionToken);
    }

    public async Task<(bool Valid, string? Username, bool IsAdmin)> ValidateSessionAsync(string? sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken)) return (false, null, false);

        var session = await _sessions.FindByTokenAsync(sessionToken);
        if (session == null) return (false, null, false);

        if (session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await _sessions.DeleteByTokenAsync(sessionToken);
            return (false, null, false);
        }

        return (true, session.Username, session.IsAdmin);
    }

    private async Task<string> CreateSessionAsync(string username, bool isAdmin)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var session = new SessionDocument
        {
            Token = token,
            Username = username,
            IsAdmin = isAdmin,
            ExpiresAtUtc = DateTime.UtcNow.Add(SessionLifetime),
        };
        await _sessions.InsertAsync(session);
        return token;
    }
}
