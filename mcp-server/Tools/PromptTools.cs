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

    private readonly IServerStore _store;
    private readonly IPromptService _promptService;
    private readonly IAuthService _auth;
    private readonly string _siteDir;

    public PromptTools(IServerStore store, IPromptService promptService, IAuthService auth, IWebHostEnvironment env)
    {
        _store = store;
        _promptService = promptService;
        _auth = auth;
        _siteDir = Path.Combine(env.ContentRootPath, "site");
    }

    [McpServerTool(Name = "reset"), Description(
        "Wipes every prompt, QA checkpoint, and build record, and deletes all generated " +
        "sites. Cannot be undone. Admin only.")]
    public async Task<string> Reset([Description("Session token")] string? sessionToken)
    {
        var (authError, _, _) = await RequireAuthAsync(sessionToken, requireAdmin: true);
        if (authError != null) return authError;

        await _store.ResetAsync();
        if (Directory.Exists(_siteDir))
        {
            foreach (var dir in Directory.GetDirectories(_siteDir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        return Ok(new { reset = true });
    }

    private const int MaxAttachments = 4;
    private const int MaxAttachmentBytes = 2 * 1024 * 1024;

    [McpServerTool(Name = "create"), Description(
        "Creates a new prompt and runs it through the automatic dev->qa->prod pipeline: " +
        "validation, content safety, trial generation, then publish if all pass. The caller " +
        "becomes the prompt's owner.")]
    public async Task<string> Create(
        [Description("Name for the new prompt")] string name,
        [Description("Prompt content")] string content,
        [Description("Reference image attachments (max 4, 2MB each) passed to Claude Code as generation context")] List<Attachment>? attachments = null,
        [Description("Session token")] string? sessionToken = null)
    {
        var (authError, username, _) = await RequireAuthAsync(sessionToken, requireAdmin: false);
        if (authError != null) return authError;
        var attachmentError = ValidateAttachments(attachments);
        if (attachmentError != null) return attachmentError;
        return await RunPipelineAsync(store => _promptService.CreateAsync(store, name, content, username!, attachments));
    }

    [McpServerTool(Name = "edit"), Description(
        "Adds a new version to an existing prompt and runs it through the pipeline again. " +
        "Only the prompt's owner or an admin may edit it.")]
    public async Task<string> Edit(
        [Description("Name of the existing prompt")] string name,
        [Description("New prompt content")] string content,
        [Description("Reference image attachments (max 4, 2MB each) passed to Claude Code as generation context")] List<Attachment>? attachments = null,
        [Description("Session token")] string? sessionToken = null)
    {
        var (authError, username, isAdmin) = await RequireAuthAsync(sessionToken, requireAdmin: false);
        if (authError != null) return authError;
        var attachmentError = ValidateAttachments(attachments);
        if (attachmentError != null) return attachmentError;
        return await RunPipelineAsync(store => _promptService.EditAsync(store, name, content, username!, isAdmin, attachments));
    }

    [McpServerTool(Name = "deletePrompt"), Description(
        "Permanently deletes a prompt and its version/QA/build history. Only the prompt's " +
        "owner or an admin may delete it. Does not delete the generated site files on disk.")]
    public async Task<string> DeletePrompt(
        [Description("Prompt name")] string name,
        [Description("Session token")] string? sessionToken = null)
    {
        var (authError, username, isAdmin) = await RequireAuthAsync(sessionToken, requireAdmin: false);
        if (authError != null) return authError;

        try
        {
            await _store.MutateAsync<object?>(store =>
            {
                _promptService.Delete(store, name, username!, isAdmin);
                return Task.FromResult<object?>(null);
            });
            // Best-effort — a missing directory (e.g. never had a successful
            // build, or Render's ephemeral disk already lost it) isn't an
            // error worth failing the whole delete over.
            var promptDir = Path.Combine(_siteDir, Slugify(name));
            if (Directory.Exists(promptDir))
            {
                Directory.Delete(promptDir, recursive: true);
            }
            return Ok(new { deleted = true });
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
        catch (PromptAccessDeniedException ex)
        {
            return Error(ex.Message);
        }
    }

    /// Returns an error envelope if any attachment is oversized, not an
    /// image, or there are too many; null when the caller may proceed.
    /// Images only, and base64-in-Mongo with a tight per-file cap, per the
    /// same reasoning the rest of this project's store already follows: no
    /// new storage system, just a size limit so the store document doesn't
    /// bloat.
    private static string? ValidateAttachments(List<Attachment>? attachments)
    {
        if (attachments == null || attachments.Count == 0) return null;
        if (attachments.Count > MaxAttachments) return Error($"Too many attachments (max {MaxAttachments}).");
        foreach (var attachment in attachments)
        {
            if (!attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Error($"Attachment \"{attachment.FileName}\" is not an image ({attachment.ContentType}). Only image attachments are supported.");
            }
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(attachment.Base64Data);
            }
            catch (FormatException)
            {
                return Error($"Attachment \"{attachment.FileName}\" has invalid base64 data.");
            }
            if (bytes.Length > MaxAttachmentBytes)
            {
                return Error($"Attachment \"{attachment.FileName}\" exceeds the {MaxAttachmentBytes / (1024 * 1024)}MB limit.");
            }
        }
        return null;
    }

    [McpServerTool(Name = "list"), Description(
        "Lists prompts and their environment/build status. A non-admin sees only their own " +
        "prompts (plus any pre-ownership legacy prompt); an admin sees every prompt, with its owner.")]
    public async Task<string> List([Description("Session token")] string? sessionToken)
    {
        var (authError, username, isAdmin) = await RequireAuthAsync(sessionToken, requireAdmin: false);
        if (authError != null) return authError;

        return await _store.ReadAsync(store =>
        {
            var items = _promptService.List(store, username!, isAdmin).Select(r => new
            {
                name = r.Name,
                owner = r.Owner,
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

    [McpServerTool(Name = "show"), Description(
        "Shows a prompt's content, environments, QA history, and build history. Only the " +
        "prompt's owner or an admin may view it.")]
    public async Task<string> Show(
        [Description("Prompt name")] string name,
        [Description("Specific version to show; defaults to the prod version, or latest")] int? version = null,
        [Description("Session token")] string? sessionToken = null)
    {
        var (authError, username, isAdmin) = await RequireAuthAsync(sessionToken, requireAdmin: false);
        if (authError != null) return authError;

        try
        {
            return await _store.ReadAsync(store =>
            {
                var (record, resolvedVersion, content) = _promptService.Show(store, name, username!, isAdmin, version);
                return Ok(new { record, version = resolvedVersion, content });
            });
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
        catch (PromptAccessDeniedException ex)
        {
            return Error(ex.Message);
        }
    }

    [McpServerTool(Name = "diff"), Description(
        "Line diff between two versions of a prompt's content. Only the prompt's owner or an " +
        "admin may view it.")]
    public async Task<string> Diff(
        [Description("Prompt name")] string name,
        [Description("First version")] int v1,
        [Description("Second version")] int v2,
        [Description("Session token")] string? sessionToken = null)
    {
        var (authError, username, isAdmin) = await RequireAuthAsync(sessionToken, requireAdmin: false);
        if (authError != null) return authError;

        try
        {
            return await _store.ReadAsync(store =>
            {
                var diffModel = _promptService.Diff(store, name, v1, v2, username!, isAdmin);
                var lines = diffModel.Lines.Select(l => new { type = l.Type.ToString().ToLowerInvariant(), text = l.Text ?? "" });
                return Ok(lines);
            });
        }
        catch (InvalidOperationException ex)
        {
            return Error(ex.Message);
        }
        catch (PromptAccessDeniedException ex)
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
        catch (PromptAccessDeniedException ex)
        {
            return Error(ex.Message);
        }
    }

    /// Returns an error envelope (and null username) if the session is
    /// missing/expired/lacks admin when required; otherwise null error plus
    /// the caller's username and admin flag, for ownership checks downstream.
    private async Task<(string? Error, string? Username, bool IsAdmin)> RequireAuthAsync(string? sessionToken, bool requireAdmin)
    {
        var (valid, username, isAdmin) = await _auth.ValidateSessionAsync(sessionToken);
        if (!valid) return (Error("Not logged in. Run \"login\" first."), null, false);
        if (requireAdmin && !isAdmin) return (Error("Admin privileges required for this command."), null, false);
        return (null, username, isAdmin);
    }

    private static string Slugify(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-') is { Length: > 0 } s ? s : "prompt";

    private static string Ok(object data) => JsonSerializer.Serialize(new { status = "ok", data }, JsonOptions);
    private static string Error(string message) => JsonSerializer.Serialize(new { status = "error", message }, JsonOptions);
}
