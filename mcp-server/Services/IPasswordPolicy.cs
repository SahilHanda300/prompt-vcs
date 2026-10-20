namespace PromptVcs.McpServer.Services;

/// <summary>
/// Abstraction over the password strength rule — swappable/testable
/// independently of AuthService, and the one place the rule is defined so
/// it can't drift between register/change-password/admin-create/admin-update.
/// </summary>
public interface IPasswordPolicy
{
    /// Returns an error message if the password is too weak, or null if
    /// it's acceptable.
    string? Validate(string? password);
}
