using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using PromptVcs.Core;
using PromptVcs.McpServer.Services;

namespace PromptVcs.McpServer.Tools;

/// <summary>
/// The CLI-facing MCP surface — one tool per CLI command. The CLI is a thin
/// client now: every command (including create/edit, which used to run the
/// whole pipeline in-process on the CLI) becomes a call here. Every response
/// is a JSON envelope: {"status":"ok","data":...} or
/// {"status":"error","message":...}, so the CLI has one uniform way to parse
/// every tool's result.
/// </summary>
[McpServerToolType]
public class PromptTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ServerStore _store;
    private readonly PromptService _promptService;

    public PromptTools(ServerStore store, PromptService promptService)
    {
        _store = store;
        _promptService = promptService;
    }

    [McpServerTool(Name = "init"), Description("Initializes the PromptVCS store on the server, if not already initialized.")]
    public async Task<string> Init()
    {
        var alreadyInitialized = await _store.InitAsync();
        return Ok(new { alreadyInitialized });
    }

    [McpServerTool(Name = "create"), Description(
        "Creates a new prompt and runs it through the automatic dev->qa->prod pipeline: " +
        "validation, content safety, trial generation, then publish if all pass.")]
    public Task<string> Create(
        [Description("Name for the new prompt")] string name,
        [Description("Prompt content")] string content) =>
        RunPipelineAsync(store => _promptService.CreateAsync(store, name, content));

    [McpServerTool(Name = "edit"), Description("Adds a new version to an existing prompt and runs it through the pipeline again.")]
    public Task<string> Edit(
        [Description("Name of the existing prompt")] string name,
        [Description("New prompt content")] string content) =>
        RunPipelineAsync(store => _promptService.EditAsync(store, name, content));

    [McpServerTool(Name = "list"), Description("Lists all prompts and their environment/build status.")]
    public async Task<string> List()
    {
        return await _store.ReadAsync(store =>
        {
            var items = PromptService.List(store).Select(r => new
            {
                name = r.Name,
                dev = r.Environments.Dev,
                qa = r.Environments.Qa,
                prod = r.Environments.Prod,
                latestBuild = r.Builds.LastOrDefault() is { } b
                    ? new { b.BuildVersion, status = b.Status, b.ArtifactUrl }
                    : null,
            });
            return Ok(items);
        });
    }

    [McpServerTool(Name = "show"), Description("Shows a prompt's content, environments, QA history, and build history.")]
    public async Task<string> Show(
        [Description("Prompt name")] string name,
        [Description("Specific version to show; defaults to the prod version, or latest")] int? version = null)
    {
        try
        {
            return await _store.ReadAsync(store =>
            {
                var (record, resolvedVersion, content) = PromptService.Show(store, name, version);
                return Ok(new { record, version = resolvedVersion, content });
            });
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
    }

    [McpServerTool(Name = "diff"), Description("Line diff between two versions of a prompt's content.")]
    public async Task<string> Diff(
        [Description("Prompt name")] string name,
        [Description("First version")] int v1,
        [Description("Second version")] int v2)
    {
        try
        {
            return await _store.ReadAsync(store =>
            {
                var diffModel = PromptService.Diff(store, name, v1, v2);
                var lines = diffModel.Lines.Select(l => new { type = l.Type.ToString().ToLowerInvariant(), text = l.Text ?? "" });
                return Ok(lines);
            });
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
    }

    private async Task<string> RunPipelineAsync(Func<Store, Task<PipelineResult>> action)
    {
        try
        {
            return await _store.MutateAsync(async store => Ok(await action(store)));
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
    }

    private static string Ok(object data) => JsonSerializer.Serialize(new { status = "ok", data }, JsonOptions);
    private static string Error(string message) => JsonSerializer.Serialize(new { status = "error", message }, JsonOptions);
}
