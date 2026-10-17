using System.Security.Cryptography;
using MongoDB.Driver;

namespace PromptVcs.McpServer.Services;

/// <summary>
/// CLI-facing login now goes through MongoDB-backed accounts instead of the
/// shared PROMPTVCS_API_TOKEN — a "users" collection (username, hashed
/// password, isAdmin) and a "sessions" collection (opaque token -> username,
/// expiry). Kept deliberately simple: no JWT, no third-party hashing
/// package — PBKDF2 via the framework's own Rfc2898DeriveBytes, and a
/// session is just a random token stored server-side, revocable by deleting
/// its document. Registration is open to anyone — the very first user ever
/// registered becomes admin automatically; everyone after that registers as
/// a normal (non-admin) user, no invitation or approval needed.
/// </summary>
public class MongoAuthService
{
    private const int Pbkdf2Iterations = 100_000;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    private readonly IMongoCollection<UserDocument>? _users;
    private readonly IMongoCollection<SessionDocument>? _sessions;

    public bool IsConfigured => _users != null;

    public MongoAuthService(MongoDatabaseProvider provider)
    {
        if (provider.Database == null) return;
        _users = provider.Database.GetCollection<UserDocument>("users");
        _sessions = provider.Database.GetCollection<SessionDocument>("sessions");
    }

    public async Task<bool> HasAnyUsersAsync() =>
        await RequireUsers().Find(FilterDefinition<UserDocument>.Empty).AnyAsync();

    /// Admin-only (caller enforces — see AuthTools.ListUsers). Just
    /// username + role, never the password hash.
    public async Task<List<(string Username, bool IsAdmin)>> ListUsersAsync()
    {
        var users = await RequireUsers().Find(FilterDefinition<UserDocument>.Empty)
            .SortBy(u => u.Username)
            .ToListAsync();
        return users.Select(u => (u.Username, u.IsAdmin)).ToList();
    }

    public async Task<(bool Success, string? Message, string? SessionToken, bool IsAdmin)> RegisterAsync(
        string username, string password)
    {
        var users = RequireUsers();
        var isFirstUser = !await HasAnyUsersAsync();

        var existing = await users.Find(u => u.Username == username).FirstOrDefaultAsync();
        if (existing != null)
        {
            return (false, $"Username \"{username}\" is already taken.", null, false);
        }

        var user = new UserDocument
        {
            Username = username,
            PasswordHash = HashPassword(password),
            IsAdmin = isFirstUser,
        };
        await users.InsertOneAsync(user);

        var sessionToken = await CreateSessionAsync(username, user.IsAdmin);
        return (true, null, sessionToken, user.IsAdmin);
    }

    public async Task<(bool Success, string? Message, string? SessionToken, bool IsAdmin)> LoginAsync(string username, string password)
    {
        var user = await RequireUsers().Find(u => u.Username == username).FirstOrDefaultAsync();
        if (user == null || !VerifyPassword(password, user.PasswordHash))
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

        var users = RequireUsers();
        var user = await users.Find(u => u.Username == username).FirstOrDefaultAsync();
        if (user == null) return (false, "User not found.");
        if (!VerifyPassword(currentPassword, user.PasswordHash)) return (false, "Current password is incorrect.");
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8) return (false, "New password must be at least 8 characters.");

        await users.UpdateOneAsync(u => u.Username == username, Builders<UserDocument>.Update.Set(u => u.PasswordHash, HashPassword(newPassword)));
        return (true, null);
    }

    /// Same current-password reverification as ChangePasswordAsync. Keeps
    /// the caller's own session alive under the new username afterward
    /// (updates that one SessionDocument's Username field) rather than
    /// forcing an immediate re-login right after a successful rename —
    /// other devices' sessions, if any, go stale until they next log in,
    /// an accepted tradeoff for not needing a bulk session update here.
    public async Task<(bool Success, string? Message, string? NewUsername)> ChangeUsernameAsync(
        string? sessionToken, string currentPassword, string newUsername)
    {
        var (valid, username, _) = await ValidateSessionAsync(sessionToken);
        if (!valid) return (false, "Not logged in. Run \"login\" first.", null);

        newUsername = newUsername.Trim();
        if (string.IsNullOrEmpty(newUsername)) return (false, "New username is required.", null);
        if (newUsername == username) return (false, "That's already your username.", null);

        var users = RequireUsers();
        var user = await users.Find(u => u.Username == username).FirstOrDefaultAsync();
        if (user == null) return (false, "User not found.", null);
        if (!VerifyPassword(currentPassword, user.PasswordHash)) return (false, "Current password is incorrect.", null);

        var existing = await users.Find(u => u.Username == newUsername).FirstOrDefaultAsync();
        if (existing != null) return (false, $"Username \"{newUsername}\" is already taken.", null);

        await users.UpdateOneAsync(u => u.Username == username, Builders<UserDocument>.Update.Set(u => u.Username, newUsername));
        await RequireSessions().UpdateOneAsync(s => s.Token == sessionToken, Builders<SessionDocument>.Update.Set(s => s.Username, newUsername));

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
        if (string.IsNullOrEmpty(password) || password.Length < 8) return (false, "Password must be at least 8 characters.");

        var users = RequireUsers();
        var existing = await users.Find(u => u.Username == username).FirstOrDefaultAsync();
        if (existing != null) return (false, $"Username \"{username}\" is already taken.");

        await users.InsertOneAsync(new UserDocument { Username = username, PasswordHash = HashPassword(password), IsAdmin = isAdmin });
        return (true, null);
    }

    /// Admin-only (caller enforces). Any of newUsername/newPassword/newIsAdmin
    /// may be omitted to leave that field alone. Refuses to demote the last
    /// remaining admin, to avoid locking every admin feature out with no way
    /// back in short of direct database access.
    public async Task<(bool Success, string? Message, string? RenamedFrom, string? RenamedTo)> AdminUpdateUserAsync(
        string targetUsername, string? newUsername, string? newPassword, bool? newIsAdmin)
    {
        var users = RequireUsers();
        var user = await users.Find(u => u.Username == targetUsername).FirstOrDefaultAsync();
        if (user == null) return (false, $"User \"{targetUsername}\" not found.", null, null);

        newUsername = string.IsNullOrWhiteSpace(newUsername) ? null : newUsername.Trim();
        if (newUsername == targetUsername) newUsername = null; // not actually a change

        if (newIsAdmin == false && user.IsAdmin)
        {
            var adminCount = await users.CountDocumentsAsync(u => u.IsAdmin);
            if (adminCount <= 1) return (false, "Can't remove admin from the last remaining admin.", null, null);
        }

        var updates = new List<UpdateDefinition<UserDocument>>();
        if (newUsername != null)
        {
            var existing = await users.Find(u => u.Username == newUsername).FirstOrDefaultAsync();
            if (existing != null) return (false, $"Username \"{newUsername}\" is already taken.", null, null);
            updates.Add(Builders<UserDocument>.Update.Set(u => u.Username, newUsername));
        }
        if (!string.IsNullOrEmpty(newPassword))
        {
            if (newPassword.Length < 8) return (false, "New password must be at least 8 characters.", null, null);
            updates.Add(Builders<UserDocument>.Update.Set(u => u.PasswordHash, HashPassword(newPassword)));
        }
        if (newIsAdmin.HasValue)
        {
            updates.Add(Builders<UserDocument>.Update.Set(u => u.IsAdmin, newIsAdmin.Value));
        }
        if (updates.Count == 0) return (false, "Nothing to update.", null, null);

        await users.UpdateOneAsync(u => u.Username == targetUsername, Builders<UserDocument>.Update.Combine(updates));

        // Every session for this user, not just one — an admin renaming/
        // re-roling someone else might be changing a user logged in on
        // several devices at once (unlike self-service rename, which only
        // has its own current session to worry about).
        if (newUsername != null)
        {
            await RequireSessions().UpdateManyAsync(s => s.Username == targetUsername, Builders<SessionDocument>.Update.Set(s => s.Username, newUsername));
        }
        if (newIsAdmin.HasValue)
        {
            await RequireSessions().UpdateManyAsync(s => s.Username == (newUsername ?? targetUsername), Builders<SessionDocument>.Update.Set(s => s.IsAdmin, newIsAdmin.Value));
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

        var users = RequireUsers();
        var user = await users.Find(u => u.Username == targetUsername).FirstOrDefaultAsync();
        if (user == null) return (false, $"User \"{targetUsername}\" not found.");

        if (user.IsAdmin)
        {
            var adminCount = await users.CountDocumentsAsync(u => u.IsAdmin);
            if (adminCount <= 1) return (false, "Can't delete the last remaining admin.");
        }

        await users.DeleteOneAsync(u => u.Username == targetUsername);
        await RequireSessions().DeleteManyAsync(s => s.Username == targetUsername);
        return (true, null);
    }

    public async Task LogoutAsync(string? sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken)) return;
        await RequireSessions().DeleteOneAsync(s => s.Token == sessionToken);
    }

    public async Task<(bool Valid, string? Username, bool IsAdmin)> ValidateSessionAsync(string? sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken)) return (false, null, false);

        var session = await RequireSessions().Find(s => s.Token == sessionToken).FirstOrDefaultAsync();
        if (session == null) return (false, null, false);

        if (session.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await RequireSessions().DeleteOneAsync(s => s.Token == sessionToken);
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
        await RequireSessions().InsertOneAsync(session);
        return token;
    }

    private IMongoCollection<UserDocument> RequireUsers() =>
        _users ?? throw new InvalidOperationException("PROMPTVCS_MONGO_URI is not configured on the server.");

    private IMongoCollection<SessionDocument> RequireSessions() =>
        _sessions ?? throw new InvalidOperationException("PROMPTVCS_MONGO_URI is not configured on the server.");

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return $"{Pbkdf2Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
