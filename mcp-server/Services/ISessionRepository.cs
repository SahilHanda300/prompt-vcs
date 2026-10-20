namespace PromptVcs.McpServer.Services;

/// <summary>
/// Storage for session tokens — single responsibility: reading/writing
/// SessionDocuments. No expiry-policy decisions beyond what's asked
/// (AuthService decides what "expired" means and acts on it).
/// </summary>
public interface ISessionRepository
{
    Task<SessionDocument?> FindByTokenAsync(string token);
    Task InsertAsync(SessionDocument session);
    Task DeleteByTokenAsync(string token);

    /// Only the caller's own current session — used by self-service rename.
    Task UpdateUsernameForTokenAsync(string token, string newUsername);

    /// Every session this user currently holds — used by an admin renaming
    /// someone else, who might be logged in on several devices at once.
    Task UpdateUsernameForAllAsync(string oldUsername, string newUsername);

    Task UpdateIsAdminForAllAsync(string username, bool isAdmin);
    Task DeleteAllForUsernameAsync(string username);
}
