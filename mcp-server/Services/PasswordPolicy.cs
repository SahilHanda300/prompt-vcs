namespace PromptVcs.McpServer.Services;

/// At least 8 characters, and must contain both a letter and a digit —
/// length alone still allows a trivially-guessable all-digit or
/// all-letter password.
public class PasswordPolicy : IPasswordPolicy
{
    private const int MinPasswordLength = 8;

    public string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength ||
            !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return $"Password must be at least {MinPasswordLength} characters and contain both letters and numbers.";
        }
        return null;
    }
}
