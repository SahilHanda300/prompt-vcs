using System.Text.RegularExpressions;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace PromptVcs.Core;

/// Thrown when an authenticated-but-non-admin caller tries to read/write/
/// delete a prompt owned by someone else — distinct from
/// InvalidOperationException ("not found"/"already exists") so callers
/// (MCP tools) can tell "doesn't exist" apart from "exists, but isn't
/// yours" if they ever want to. Currently both still surface as the same
/// {"status":"error"} envelope either way.
public class PromptAccessDeniedException(string message) : Exception(message);

public class PromptService : IPromptService
{
    private readonly IPipelineService _pipeline;

    public PromptService(IPipelineService pipeline)
    {
        _pipeline = pipeline;
    }

    public Task<PipelineResult> CreateAsync(Store store, string name, string content, string owner, List<Attachment>? attachments = null)
    {
        var id = Slugify(name);
        if (store.Prompts.ContainsKey(id))
        {
            throw new InvalidOperationException($"Prompt \"{name}\" already exists (id \"{id}\"). Use \"edit\" to add a new version.");
        }
        var record = new PromptRecord { Id = id, Name = name, Owner = owner };
        store.Prompts[id] = record;
        return _pipeline.SubmitAsync(record, content, attachments);
    }

    public Task<PipelineResult> EditAsync(Store store, string name, string content, string callerUsername, bool callerIsAdmin, List<Attachment>? attachments = null)
    {
        var record = GetOwnedOrThrow(store, name, callerUsername, callerIsAdmin);
        return _pipeline.SubmitAsync(record, content, attachments);
    }

    /// Removes the prompt record entirely. Does NOT touch anything on disk
    /// (generated artifacts) — callers with access to the site directory
    /// (currently only PromptTools, same as "reset") delete that
    /// separately, same division of responsibility "reset" already has.
    public void Delete(Store store, string name, string callerUsername, bool callerIsAdmin)
    {
        var record = GetOwnedOrThrow(store, name, callerUsername, callerIsAdmin);
        store.Prompts.Remove(record.Id);
    }

    public void RenameOwner(Store store, string oldUsername, string newUsername)
    {
        foreach (var record in store.Prompts.Values)
        {
            if (record.Owner == oldUsername)
            {
                record.Owner = newUsername;
            }
        }
    }

    public List<PromptRecord> List(Store store, string callerUsername, bool callerIsAdmin) =>
        store.Prompts.Values
            .Where(r => callerIsAdmin || r.Owner == null || r.Owner == callerUsername)
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToList();

    public (PromptRecord Record, int Version, string Content) Show(Store store, string name, string callerUsername, bool callerIsAdmin, int? version = null)
    {
        var record = GetOwnedOrThrow(store, name, callerUsername, callerIsAdmin);
        var targetVersion = version ?? record.Environments.Prod ?? record.History.LastOrDefault()?.Version;
        var entry = record.History.FirstOrDefault(h => h.Version == targetVersion);
        if (entry == null)
        {
            throw new InvalidOperationException($"Version {targetVersion} not found for prompt \"{name}\".");
        }
        return (record, entry.Version, entry.Content);
    }

    public DiffPaneModel Diff(Store store, string name, int v1, int v2, string callerUsername, bool callerIsAdmin)
    {
        var record = GetOwnedOrThrow(store, name, callerUsername, callerIsAdmin);
        var e1 = record.History.FirstOrDefault(h => h.Version == v1) ?? throw new InvalidOperationException($"Version {v1} not found for prompt \"{name}\".");
        var e2 = record.History.FirstOrDefault(h => h.Version == v2) ?? throw new InvalidOperationException($"Version {v2} not found for prompt \"{name}\".");
        return InlineDiffBuilder.Diff(e1.Content, e2.Content);
    }

    private static PromptRecord GetOwnedOrThrow(Store store, string name, string callerUsername, bool callerIsAdmin)
    {
        var record = GetOrThrow(store, name);
        if (!callerIsAdmin && record.Owner != null && record.Owner != callerUsername)
        {
            throw new PromptAccessDeniedException($"Prompt \"{name}\" belongs to another user.");
        }
        return record;
    }

    private static PromptRecord GetOrThrow(Store store, string name)
    {
        var id = Slugify(name);
        if (!store.Prompts.TryGetValue(id, out var record))
        {
            throw new InvalidOperationException($"Prompt \"{name}\" not found.");
        }
        return record;
    }

    private static string Slugify(string name)
    {
        var slug = Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "prompt" : slug;
    }
}
