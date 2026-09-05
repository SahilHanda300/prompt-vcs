using System.Text.Json;

namespace PromptVcs.Cli;

/// <summary>
/// Caches the session token issued by "login" so the CLI doesn't have to
/// re-authenticate on every command. Deliberately separate from the old
/// server-side ".promptvcs" store dir concept — this holds a credential, not
/// application data, and the CLI otherwise still keeps no local store.
///
/// PROMPTVCS_SESSION_PATH overrides the file location — set by
/// TerminalSessionManager to a unique per-browser-session temp file, since
/// every browser terminal session spawns a CLI process on the *same server
/// machine*: without this, two browser tabs (or two different logged-in
/// users) would collide on the same %USERPROFILE% path and share a login.
/// The default path is only correct for a real local CLI user.
/// </summary>
public record CachedSession(string SessionToken, string Username, bool IsAdmin);

public static class SessionCache
{
    private static string FilePath =>
        Environment.GetEnvironmentVariable("PROMPTVCS_SESSION_PATH") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".promptvcs-cli", "session.json");

    public static CachedSession? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JsonSerializer.Deserialize<CachedSession>(File.ReadAllText(FilePath));
        }
        catch
        {
            return null;
        }
    }

    public static void Save(CachedSession session)
    {
        var dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(session));
    }

    public static void Clear()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
