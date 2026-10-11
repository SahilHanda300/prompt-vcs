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
    //
    // Font and image providers are deliberate, narrow exceptions (fonts:
    // decided after the blanket block was found to hurt visual quality —
    // system fonts alone looked noticeably plainer than real generated
    // examples. Images: decided so pages can use real photography, not
    // decorative CSS alone). <link>/@import/url() may reference a known
    // font CDN; <img>/url() may additionally reference a known image CDN
    // (see ImageProviderHosts). Any OTHER external host through those same
    // constructs is still blocked — an arbitrary external stylesheet or
    // image from an unlisted host is exactly the reliability/self-
    // containment risk the guard exists for. <script>/<iframe> stay
    // hard-blocked regardless of host, including the allowed hosts
    // themselves — neither fonts nor images justify an exception there.
    private static readonly string[] FontProviderHosts =
    {
        "fonts.googleapis.com",
        "fonts.gstatic.com",
        "fonts.bunny.net",
        "use.typekit.net",
        "p.typekit.net",
    };

    // Images: deliberately NOT a general "any image host" allowance, and
    // deliberately NOT a photo-ID-based service (e.g. images.unsplash.com,
    // where a real photo URL embeds a specific photo's id) — a model has no
    // way to know a REAL id ahead of time, so it would be guessing/inventing
    // one, which renders as a broken image. picsum.photos URLs are
    // template-based instead (e.g. picsum.photos/seed/<any-text>/800/600) —
    // any well-formed URL in that shape returns a real photo, so there's no
    // hallucinated-id failure mode the way there would be with an ID-based
    // provider.
    private static readonly string[] ImageProviderHosts = { "picsum.photos" };

    private static readonly (string Label, Regex Pattern, bool AllowFontProviders, bool AllowImageProviders)[] ExternalResourceChecks =
    {
        ("<link> to an external resource", new Regex(@"<link\b[^>]*href\s*=\s*[""'](https?://[^""']+)[""']", RegexOptions.IgnoreCase), true, false),
        ("<script> with an external src", new Regex(@"<script\b[^>]*\bsrc\s*=\s*[""'](https?://[^""']+)[""']", RegexOptions.IgnoreCase), false, false),
        ("<img> with an external src", new Regex(@"<img\b[^>]*\bsrc\s*=\s*[""'](https?://[^""']+)[""']", RegexOptions.IgnoreCase), false, true),
        ("<iframe> with an external src", new Regex(@"<iframe\b[^>]*\bsrc\s*=\s*[""'](https?://[^""']+)[""']", RegexOptions.IgnoreCase), false, false),
        ("CSS @import of an external URL", new Regex(@"@import\s+(?:url\()?[""']?(https?://[^""'\)]+)", RegexOptions.IgnoreCase), true, false),
        ("CSS url() referencing an external resource", new Regex(@"url\(\s*[""']?(https?://[^""'\)]+)", RegexOptions.IgnoreCase), true, true),
    };

    private static List<string> FindExternalResourceViolations(string html)
    {
        var violations = new List<string>();
        foreach (var (label, pattern, allowFontProviders, allowImageProviders) in ExternalResourceChecks)
        {
            foreach (Match match in pattern.Matches(html))
            {
                var url = match.Groups[1].Value;
                if (allowFontProviders && IsAllowedHost(url, FontProviderHosts)) continue;
                if (allowImageProviders && IsAllowedHost(url, ImageProviderHosts)) continue;
                violations.Add($"{label} ({url})");
            }
        }
        return violations;
    }

    private static bool IsAllowedHost(string url, string[] allowedHosts)
    {
        try
        {
            var host = new Uri(url).Host;
            return allowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    // Found via real user feedback that generated pages can have dead
    // internal navigation — a CTA like "See Laptops" linking to
    // href="#laptops" where nothing on the page actually has id="laptops".
    // Most likely on the parallel first-run path specifically: each section
    // is generated independently and can't see what ids other sections
    // created (see PublishRules.ContentSectionTemplate/EnsureSectionId,
    // which now injects a guaranteed id onto each section regardless of
    // what the model wrote) — but checked on every path here, since a
    // single-call generation can in principle make the same mistake too.
    private static readonly Regex HashHref = new(@"href\s*=\s*[""']#([a-zA-Z][\w-]*)[""']", RegexOptions.IgnoreCase);
    private static readonly Regex IdAttr = new(@"\bid\s*=\s*[""']([a-zA-Z][\w-]*)[""']", RegexOptions.IgnoreCase);

    private static List<string> FindBrokenInternalLinks(string html)
    {
        var targets = HashHref.Matches(html).Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (targets.Count == 0) return new List<string>();
        var existingIds = new HashSet<string>(IdAttr.Matches(html).Select(m => m.Groups[1].Value), StringComparer.OrdinalIgnoreCase);
        return targets.Where(t => !existingIds.Contains(t)).ToList();
    }

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

        var externalRefs = FindExternalResourceViolations(text);
        if (externalRefs.Count > 0)
        {
            return new SanitizeResult(false, "",
                $"Generated output references external resource(s) violating the self-contained-artifact rule (known font providers like Google Fonts, and picsum.photos for images, are allowed; other external resources are not): {string.Join("; ", externalRefs)}.");
        }

        var brokenLinks = FindBrokenInternalLinks(text);
        if (brokenLinks.Count > 0)
        {
            return new SanitizeResult(false, "",
                $"Generated output has internal link(s) pointing at a section that doesn't exist on the page: {string.Join(", ", brokenLinks.Select(b => $"#{b}"))}.");
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
