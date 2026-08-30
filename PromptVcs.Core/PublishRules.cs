using System.Text;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace PromptVcs.Core;

public record PublishOutcome(bool Ok, int BuildVersion, string? ArtifactRelativePath, string? Detail);

/// <summary>
/// Owns the first-run vs. diff-aware-update decision — the core of what the
/// publish stage is responsible for, per design: it doesn't just pass the
/// prompt through to the generator, it decides how to build/update the site.
///
/// Runs in-process as part of Pipeline now (previously a separate MCP server
/// call from the CLI). Prior-build lookup comes directly from the PromptRecord
/// it's given — no separate build-metadata store to keep in sync, since the
/// whole pipeline (store, QA, publish) runs in one process against one Store.
/// </summary>
public class PublishRules
{
    private readonly IClaudeCodeInvoker _invoker;
    private readonly string _siteRootDir;

    public PublishRules(IClaudeCodeInvoker invoker, string siteRootDir)
    {
        _invoker = invoker;
        _siteRootDir = siteRootDir;
    }

    public async Task<PublishOutcome> PublishAsync(PromptRecord record, int promptVersion, string content)
    {
        var buildVersion = record.Builds.Count + 1;
        var previousBuild = record.Builds.LastOrDefault(b => b.Status == BuildStatus.Success);
        var previousContent = previousBuild != null
            ? record.History.FirstOrDefault(h => h.Version == previousBuild.PromptVersion)?.Content
            : null;

        var previousArtifactPath = Path.Combine(_siteRootDir, record.Id, "index.html");
        var previousArtifact = previousBuild != null && File.Exists(previousArtifactPath)
            ? await File.ReadAllTextAsync(previousArtifactPath)
            : null;

        var generationPrompt = previousContent == null || previousArtifact == null
            ? BuildFirstRunPrompt(record.Name, content)
            : BuildUpdatePrompt(record.Name, previousContent, content, previousArtifact);

        var claudeResult = await _invoker.InvokeAsync(generationPrompt);
        if (!claudeResult.Ok)
        {
            return new PublishOutcome(false, buildVersion, null, claudeResult.Detail);
        }

        var sanitized = ArtifactSanitizer.Sanitize(claudeResult.Text);
        if (!sanitized.Ok)
        {
            return new PublishOutcome(false, buildVersion, null, sanitized.Detail);
        }

        var siteDir = Path.Combine(_siteRootDir, record.Id);
        var versionedDir = Path.Combine(siteDir, $"v{buildVersion}");
        Directory.CreateDirectory(versionedDir);
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await File.WriteAllTextAsync(Path.Combine(versionedDir, "index.html"), sanitized.Html, utf8NoBom);
        await File.WriteAllTextAsync(Path.Combine(siteDir, "index.html"), sanitized.Html, utf8NoBom);

        return new PublishOutcome(true, buildVersion, $"/site/{record.Id}/", null);
    }

    private static string BuildFirstRunPrompt(string promptName, string content)
    {
        return string.Join("\n",
            "You are generating a complete, self-contained, single-file HTML artifact for a tool called PromptVCS.",
            "The output may be a content site or a fully functional interactive app (infer which from the prompt) — infer this yourself, do not ask.",
            "Requirements: a single HTML file with any needed CSS/JS inlined, no external dependencies, include a viewport meta tag.",
            "Respond with ONLY the HTML document — no explanation, no markdown code fences.",
            "",
            $"Prompt name: {promptName}",
            "Prompt:",
            content);
    }

    private static string BuildUpdatePrompt(string promptName, string previousContent, string newContent, string previousArtifact)
    {
        var diffBuilder = new InlineDiffBuilder(new Differ());
        var diffResult = diffBuilder.BuildDiffModel(previousContent, newContent);
        var diffText = string.Join("\n", diffResult.Lines.Select(FormatDiffLine));

        return string.Join("\n",
            "You are updating an existing self-contained, single-file HTML artifact for a tool called PromptVCS.",
            "The prompt that generated it has changed. Below is a line diff of the prompt change (+ added, - removed) and the existing artifact.",
            "Apply a targeted update reflecting the diff — do not regenerate from scratch unless the diff requires it.",
            "Respond with ONLY the complete updated HTML document — no explanation, no markdown code fences.",
            "",
            $"Prompt name: {promptName}",
            "Prompt diff:",
            diffText,
            "",
            "Existing artifact:",
            previousArtifact);
    }

    private static string FormatDiffLine(DiffPiece line) => line.Type switch
    {
        ChangeType.Inserted => $"+ {line.Text}",
        ChangeType.Deleted => $"- {line.Text}",
        _ => $"  {line.Text}",
    };
}
