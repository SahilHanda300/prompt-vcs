using System.Text.Json;
using System.Text.RegularExpressions;

namespace PromptVcs.Core;

/// <summary>
/// Load/save for the CLI's (and future web app's) prompt store — a single
/// JSON file at .promptvcs/store.json in the working directory. This is the
/// one store the shared core module operates on.
/// </summary>
public static class PromptStore
{
    private const string StoreDirName = ".promptvcs";
    private const string StoreFileName = "store.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string StoreDir(string? cwd = null) => Path.Combine(cwd ?? Directory.GetCurrentDirectory(), StoreDirName);

    public static string StorePath(string? cwd = null) => Path.Combine(StoreDir(cwd), StoreFileName);

    public static bool IsInitialized(string? cwd = null) => File.Exists(StorePath(cwd));

    public static Store Init(string? cwd = null)
    {
        if (IsInitialized(cwd)) return Load(cwd);
        Directory.CreateDirectory(StoreDir(cwd));
        var store = new Store();
        Save(store, cwd);
        return store;
    }

    public static Store Load(string? cwd = null)
    {
        var path = StorePath(cwd);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"No Sites registerd currently in PromptVCS. Run \"promptvcs init\" first.");
        }
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Store>(json) ?? new Store();
    }

    public static void Save(Store store, string? cwd = null)
    {
        var dir = StoreDir(cwd);
        Directory.CreateDirectory(dir);
        var path = StorePath(cwd);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(store, JsonOptions));
        File.Move(tmp, path, overwrite: true);
    }

    public static string Slugify(string name)
    {
        var slug = Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "prompt" : slug;
    }
}
