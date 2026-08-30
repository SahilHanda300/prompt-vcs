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
