namespace PromptVcs.McpServer.Services;

/// <summary>
/// Abstraction over password hashing/verification — single responsibility,
/// no knowledge of users, sessions, or the password strength rule.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}
