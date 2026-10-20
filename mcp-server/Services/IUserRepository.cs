namespace PromptVcs.McpServer.Services;

/// <summary>
/// Storage for user accounts — single responsibility: reading/writing
/// UserDocuments. No password hashing, no session knowledge, no business
/// rules (e.g. "can't demote the last admin" lives in AuthService, which
/// uses CountAdminsAsync to enforce it, not here).
/// </summary>
public interface IUserRepository
{
    bool IsConfigured { get; }

    Task<bool> AnyAsync();
    Task<UserDocument?> FindByUsernameAsync(string username);
    Task<List<UserDocument>> ListAllAsync();
    Task<long> CountAdminsAsync();
    Task InsertAsync(UserDocument user);
    Task SetPasswordHashAsync(string username, string passwordHash);
    Task SetUsernameAsync(string oldUsername, string newUsername);
    Task SetIsAdminAsync(string username, bool isAdmin);
    Task DeleteAsync(string username);
}
