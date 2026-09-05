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
