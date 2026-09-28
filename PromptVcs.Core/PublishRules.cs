using System.Text;
using System.Text.Json;
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
    private const string HtmlMarker = "===HTML===";

    private readonly IClaudeCodeInvoker _invoker;
    private readonly string _siteRootDir;

    public PublishRules(IClaudeCodeInvoker invoker, string siteRootDir)
    {
        _invoker = invoker;
        _siteRootDir = siteRootDir;
    }

    /// Update path only (Pipeline calls this when a prior successful build
    /// exists) — one Claude Code call, diff-aware.
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

        return await FinalizeArtifactAsync(record, buildVersion, claudeResult.Text);
    }

    /// First-run path only (Pipeline calls this when there is no prior
    /// successful build) — combines the content-safety/feasibility
    /// screening and the actual generation into ONE Claude Code call, to
    /// cut latency further than QA's own combined call already does (see
    /// Qa.RunSafetyAndFeasibilityAsync). Deliberately NOT used for updates:
    /// BuildUpdatePrompt needs the diff + full previous artifact as
    /// context, which doesn't fit cleanly into a single screen-then-generate
    /// call the way a from-scratch first run does.
    ///
    /// The HTML is returned after a plain-text "===HTML===" marker, not as
    /// a JSON string field — asking a model to correctly JSON-escape a
    /// multi-KB HTML/CSS/JS document is a real reliability risk (quotes,
    /// backslashes, newlines all need perfect escaping); a marker needs no
    /// escaping at all and only costs one string search.
    public async Task<(QaCheckResult Safety, QaCheckResult Feasibility, PublishOutcome Outcome)> ScreenAndPublishFirstRunAsync(
        PromptRecord record, int promptVersion, string content)
    {
        var buildVersion = record.Builds.Count + 1;
        var prompt = BuildFirstRunScreenAndGeneratePrompt(record.Name, content);

        var claudeResult = await _invoker.InvokeAsync(prompt);
        if (!claudeResult.Ok)
        {
            var failure = new QaCheckResult(false, claudeResult.Detail ?? "Screening call failed to run.");
            return (failure, failure, new PublishOutcome(false, buildVersion, null, claudeResult.Detail));
        }

        var text = claudeResult.Text;
        var markerIndex = text.IndexOf(HtmlMarker, StringComparison.Ordinal);
        var metaText = markerIndex >= 0 ? text[..markerIndex] : text;
        var html = markerIndex >= 0 ? text[(markerIndex + HtmlMarker.Length)..].TrimStart('\r', '\n') : null;

        bool safe;
        bool feasible;
        string? safetyReason;
        string? feasibilitySummary;
        try
        {
            var start = metaText.IndexOf('{');
            var end = metaText.LastIndexOf('}');
            var jsonText = start >= 0 && end > start ? metaText[start..(end + 1)] : metaText;
            using var doc = JsonDocument.Parse(jsonText);
            safe = doc.RootElement.TryGetProperty("safe", out var safeProp) && safeProp.GetBoolean();
            safetyReason = doc.RootElement.TryGetProperty("safetyReason", out var reasonProp) ? reasonProp.GetString() : metaText.Trim();
            feasible = doc.RootElement.TryGetProperty("feasible", out var feasibleProp) && feasibleProp.GetBoolean();
            feasibilitySummary = doc.RootElement.TryGetProperty("feasibilitySummary", out var summaryProp) ? summaryProp.GetString() : metaText.Trim();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            var failure = new QaCheckResult(false, $"Could not parse screening response: {metaText.Trim()}");
            return (failure, failure, new PublishOutcome(false, buildVersion, null, "Screening response unparseable."));
        }

        var safetyResult = new QaCheckResult(safe, safetyReason);
        var feasibilityResult = new QaCheckResult(feasible, feasibilitySummary);

        if (!safe || !feasible)
        {
            return (safetyResult, feasibilityResult, new PublishOutcome(false, buildVersion, null, "Skipped: did not pass screening."));
        }

        if (string.IsNullOrWhiteSpace(html))
        {
            var missingHtml = new PublishOutcome(false, buildVersion, null, "Marked safe and feasible but no HTML was returned.");
            return (safetyResult, feasibilityResult, missingHtml);
        }

        var outcome = await FinalizeArtifactAsync(record, buildVersion, html);
        return (safetyResult, feasibilityResult, outcome);
    }

    private async Task<PublishOutcome> FinalizeArtifactAsync(PromptRecord record, int buildVersion, string rawHtml)
    {
        var sanitized = ArtifactSanitizer.Sanitize(rawHtml);
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

        // Points at this build's own versioned copy, not the shared
        // "latest" copy at /site/{id}/ — otherwise every build's link in
        // build history would show whatever is currently live instead of
        // what that specific build actually produced.
        return new PublishOutcome(true, buildVersion, $"/site/{record.Id}/v{buildVersion}/", null);
    }

    private static string BuildFirstRunPrompt(string promptName, string content)
    {
        return string.Join("\n", new[]
        {
            "You are generating a complete, self-contained, single-file HTML artifact for a tool called PromptVCS.",
            "The people using this tool are non-technical and often describe what they want in just a few words. Treat brevity as creative freedom, not missing information — never ask for more detail, and never output something sparse, generic, or placeholder-looking.",
            "The output may be a content site or a fully functional interactive app (infer which from the prompt) — infer this yourself, do not ask.",
            "",
            "Quality bar, non-negotiable regardless of how short or vague the prompt is:",
        }
        .Concat(QualityBarLines())
        .Concat(new[]
        {
            "",
            "Requirements: a single HTML file with any needed CSS/JS inlined, no external dependencies (no CDNs, no external fonts or images), include a viewport meta tag and a descriptive <title> tag.",
            "Respond with ONLY the HTML document — no explanation, no markdown code fences.",
            "",
            $"Prompt name: {promptName}",
            "Prompt:",
            content,
        }));
    }

    private static string BuildFirstRunScreenAndGeneratePrompt(string promptName, string content)
    {
        return string.Join("\n", new[]
        {
            "You are screening a prompt AND, if appropriate, generating the resulting artifact for a tool called PromptVCS — both in this one response.",
            "The people using this tool are non-technical and often describe what they want in just a few words. Treat brevity as creative freedom, not missing information — never ask for more detail, and this is not a reason to fail either check below.",
            "",
            "First, assess two independent things about the prompt:",
            "1. safe — is it free of genuinely unsafe/harmful content? Being brief or vague is not unsafe.",
            "2. feasible — can something reasonable be built from it as a single self-contained HTML page? Mark true unless the prompt is empty/gibberish, or explicitly requires something a single page structurally cannot provide (e.g. a real multi-user backend, a database, server-side payments).",
            "",
            "Then, ONLY if both are true, generate the artifact — a content site or a fully functional interactive app, inferred from the prompt yourself.",
            "",
            "Quality bar for the artifact, non-negotiable regardless of how short or vague the prompt is:",
        }
        .Concat(QualityBarLines())
        .Concat(new[]
        {
            "- A single HTML file with any needed CSS/JS inlined, no external dependencies (no CDNs, no external fonts or images), a viewport meta tag, and a descriptive <title> tag.",
            "",
            "Respond in EXACTLY this format and nothing else:",
            "Line 1: a single-line JSON object {\"safe\": boolean, \"safetyReason\": string, \"feasible\": boolean, \"feasibilitySummary\": string}",
            $"If safe and feasible are both true: immediately after, a line containing exactly {HtmlMarker} , then the complete HTML document — raw, NOT JSON-escaped, NOT wrapped in markdown code fences, nothing else after it.",
            "If either is false: output nothing after the JSON line.",
            "",
            $"Prompt name: {promptName}",
            "Prompt:",
            content,
        }));
    }

    /// Shared between BuildFirstRunPrompt and BuildFirstRunScreenAndGeneratePrompt
    /// so the two generation prompts can't silently drift apart.
    private static IEnumerable<string> QualityBarLines() => new[]
    {
        "- Make confident, specific creative decisions to fill in anything unstated: invent a plausible name/brand, tagline, and realistic body copy that fits the theme. Never use placeholder text like 'Lorem ipsum', '[Your text here]', 'Company Name', or similar.",
        "- Produce a genuinely polished visual design: a cohesive modern color palette, readable typography with a clear hierarchy, sensible spacing, and multiple relevant sections — not a single heading and a paragraph.",
        "- If the prompt describes an app or tool (a calculator, a todo list, a converter, a game, etc.), implement REAL working functionality in JavaScript — actual computation, state, and interactivity. Never ship a static mockup or a button that does nothing.",
        "- Handle the range of input a real (non-technical) person would reasonably type, not just the narrowest valid case. For example, a calculator should accept natural notation like implicit multiplication ('8cos(6)', '2(3+4)', '3π') rather than showing a bare error for it; a form should tolerate minor formatting variation. Only show an error for genuinely invalid input, and make it specific, not just the word 'Error'.",
        "- Fully responsive at phone width and up.",
    };

    private static string BuildUpdatePrompt(string promptName, string previousContent, string newContent, string previousArtifact)
    {
        var diffBuilder = new InlineDiffBuilder(new Differ());
        var diffResult = diffBuilder.BuildDiffModel(previousContent, newContent);
        var diffText = string.Join("\n", diffResult.Lines.Select(FormatDiffLine));

        return string.Join("\n",
            "You are updating an existing self-contained, single-file HTML artifact for a tool called PromptVCS, used by non-technical people.",
            "The prompt that generated it has changed. Below is a line diff of the prompt change (+ added, - removed) and the existing artifact.",
            "Apply a targeted update reflecting the diff — do not regenerate from scratch unless the diff requires it.",
            "Keep (or improve) the same quality bar as the original: no placeholder-looking text, a polished cohesive design, and real working functionality for anything interactive — never let an update make the result feel sparser or less finished than before.",
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
