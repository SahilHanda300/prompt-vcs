using System.Security.Cryptography;

namespace PromptVcs.McpServer.Services;

/// <summary>
/// No third-party hashing package or JWT library needed — PBKDF2 via the
/// framework's own Rfc2898DeriveBytes. Stored as
/// "{iterations}.{saltBase64}.{hashBase64}" so the iteration count can
/// change later without invalidating already-hashed passwords.
/// </summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Pbkdf2Iterations = 100_000;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return $"{Pbkdf2Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
