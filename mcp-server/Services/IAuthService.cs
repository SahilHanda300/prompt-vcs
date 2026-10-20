namespace PromptVcs.McpServer.Services;

/// <summary>
/// Orchestrates registration/login/session-validation/account-management by
/// composing IUserRepository, ISessionRepository, IPasswordHasher, and
/// IPasswordPolicy — the business rules (first user becomes admin, can't
/// demote/delete the last admin, current-password reverification for
/// self-service changes) live here, not in the repositories.
/// </summary>
public interface IAuthService
{
    bool IsConfigured { get; }

    Task<List<(string Username, bool IsAdmin)>> ListUsersAsync();
    Task<(bool Success, string? Message, string? SessionToken, bool IsAdmin)> RegisterAsync(string username, string password);
    Task<(bool Success, string? Message, string? SessionToken, bool IsAdmin)> LoginAsync(string username, string password);
    Task<(bool Success, string? Message)> ChangePasswordAsync(string? sessionToken, string currentPassword, string newPassword);
    Task<(bool Success, string? Message, string? NewUsername)> ChangeUsernameAsync(string? sessionToken, string currentPassword, string newUsername);
    Task<(bool Success, string? Message)> AdminCreateUserAsync(string username, string password, bool isAdmin);
    Task<(bool Success, string? Message, string? RenamedFrom, string? RenamedTo)> AdminUpdateUserAsync(string targetUsername, string? newUsername, string? newPassword, bool? newIsAdmin);
    Task<(bool Success, string? Message)> AdminDeleteUserAsync(string targetUsername, string callerUsername);
    Task LogoutAsync(string? sessionToken);
    Task<(bool Valid, string? Username, bool IsAdmin)> ValidateSessionAsync(string? sessionToken);
}
