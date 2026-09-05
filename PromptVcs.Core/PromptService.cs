using System.Text.RegularExpressions;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace PromptVcs.Core;

public class PromptService
{
    private readonly Pipeline _pipeline;

    public PromptService(Pipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public Task<PipelineResult> CreateAsync(Store store, string name, string content)
    {
        var id = Slugify(name);
        if (store.Prompts.ContainsKey(id))
        {
            throw new InvalidOperationException($"Prompt \"{name}\" already exists (id \"{id}\"). Use \"edit\" to add a new version.");
        }
        var record = new PromptRecord { Id = id, Name = name };
        store.Prompts[id] = record;
        return _pipeline.SubmitAsync(record, content);
    }

    public Task<PipelineResult> EditAsync(Store store, string name, string content)
    {
        var record = GetOrThrow(store, name);
        return _pipeline.SubmitAsync(record, content);
    }

    public static List<PromptRecord> List(Store store) =>
        store.Prompts.Values.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();

    public static (PromptRecord Record, int Version, string Content) Show(Store store, string name, int? version = null)
    {
        var record = GetOrThrow(store, name);
        var targetVersion = version ?? record.Environments.Prod ?? record.History.LastOrDefault()?.Version;
        var entry = record.History.FirstOrDefault(h => h.Version == targetVersion);
        if (entry == null)
        {
            throw new InvalidOperationException($"Version {targetVersion} not found for prompt \"{name}\".");
        }
        return (record, entry.Version, entry.Content);
    }

    public static DiffPaneModel Diff(Store store, string name, int v1, int v2)
    {
        var record = GetOrThrow(store, name);
        var e1 = record.History.FirstOrDefault(h => h.Version == v1) ?? throw new InvalidOperationException($"Version {v1} not found for prompt \"{name}\".");
        var e2 = record.History.FirstOrDefault(h => h.Version == v2) ?? throw new InvalidOperationException($"Version {v2} not found for prompt \"{name}\".");
        return InlineDiffBuilder.Diff(e1.Content, e2.Content);
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
