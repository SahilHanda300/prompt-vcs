using System.Linq;
using System.Text.RegularExpressions;

namespace PromptVcs.Core;

public record SanitizeResult(bool Ok, string Html, string? Detail);

/// <summary>
/// Sanity-checks and normalizes Claude Code's raw output into a servable
/// artifact: strips stray prose/code-fence wrapping and enforces baseline
/// scaffolding (UTF-8 charset, viewport meta tag) across all builds.
///
/// The charset meta tag matters concretely: without it, a browser that can't
/// otherwise detect the encoding may guess a legacy codepage (e.g.
/// Windows-1252) instead of UTF-8, turning multi-byte characters like × ÷ √
/// into mojibake even though the file on disk is valid UTF-8.
/// </summary>
public static class ArtifactSanitizer
{
    private static readonly Regex CodeFence = new(@"^```(?:html)?\s*\n([\s\S]*?)\n```\s*$", RegexOptions.Multiline);
    private static readonly Regex CharsetMeta = new(@"<meta[^>]*charset\s*=", RegexOptions.IgnoreCase);
    private static readonly Regex ViewportMeta = new(@"<meta[^>]*viewport", RegexOptions.IgnoreCase);
    private const int MinLength = 20;

    // Every generation prompt tells the model "no external dependencies" in
    // plain English — real measurement showed that's necessary but NOT
    // sufficient: a parallel-section first-run build once slipped a Google
    // Fonts @import into every section despite the instruction being present
    // elsewhere in the same prompt (see CLAUDE.md's "parallel first-run
    // generation" entry). Prompt wording is a request the model can get
    // wrong; this is a deterministic, code-enforced backstop that can't.
    // Each pattern targets an actual resource-LOADING construct — not a
    // plain <a href="https://..."> content link, which is legitimate (e.g.
    // a social media link) and not a self-containment violation at all.
    private static readonly (string Label, Regex Pattern)[] ExternalResourcePatterns =
    {
        ("<link> to an external stylesheet/font", new Regex(@"<link\b[^>]*href\s*=\s*[""']https?://", RegexOptions.IgnoreCase)),
        ("<script> with an external src", new Regex(@"<script\b[^>]*\bsrc\s*=\s*[""']https?://", RegexOptions.IgnoreCase)),
        ("<img>/<iframe> with an external src", new Regex(@"<(?:img|iframe)\b[^>]*\bsrc\s*=\s*[""']https?://", RegexOptions.IgnoreCase)),
        ("CSS @import of an external URL", new Regex(@"@import\s+(?:url\()?[""']?https?://", RegexOptions.IgnoreCase)),
        ("CSS url() referencing an external resource", new Regex(@"url\(\s*[""']?https?://", RegexOptions.IgnoreCase)),
    };

    public static SanitizeResult Sanitize(string raw)
    {
        var text = raw.Trim();

        var fenceMatch = CodeFence.Match(text);
        if (fenceMatch.Success)
        {
            text = fenceMatch.Groups[1].Value.Trim();
        }

        if (text.Length < MinLength)
        {
            return new SanitizeResult(false, "", $"Generated output too short ({text.Length} chars) to be a valid artifact.");
        }

        var externalRefs = ExternalResourcePatterns.Where(p => p.Pattern.IsMatch(text)).Select(p => p.Label).ToList();
        if (externalRefs.Count > 0)
        {
            return new SanitizeResult(false, "",
                $"Generated output references external resource(s), violating the self-contained-artifact rule: {string.Join("; ", externalRefs)}.");
        }

        if (!ViewportMeta.IsMatch(text))
        {
            text = InjectHeadTag(text, "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        }

        // Charset must be injected last so it ends up as the first child of
        // <head> — browsers only honor it if it appears within the first
        // ~1024 bytes, so it needs to precede other injected/existing tags.
        if (!CharsetMeta.IsMatch(text))
        {
            text = InjectHeadTag(text, "<meta charset=\"utf-8\">");
        }

        return new SanitizeResult(true, text, null);
    }

    private static string InjectHeadTag(string html, string tag)
    {
        var headMatch = Regex.Match(html, "<head[^>]*>", RegexOptions.IgnoreCase);
        if (headMatch.Success)
        {
            var insertAt = headMatch.Index + headMatch.Length;
            return html.Insert(insertAt, "\n" + tag);
        }
        // No <head> at all — fall back to prepending a minimal one.
        return $"<head>{tag}</head>\n{html}";
    }
}
