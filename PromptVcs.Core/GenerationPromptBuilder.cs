using System.Text.RegularExpressions;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace PromptVcs.Core;

/// <summary>
/// Builds every prompt text PublishRules sends to Claude Code. Extracted out
/// of PublishRules (which now only orchestrates: invoke Claude Code, parse
/// the response, sanitize, write) so prompt wording can change/be swapped
/// independently of that orchestration — see IGenerationPromptBuilder.
/// </summary>
public class GenerationPromptBuilder : IGenerationPromptBuilder
{
    /// Tone alternates dark/light/dark/light by fixed position, found
    /// necessary by actually reading generated pages, not just checking
    /// they built: without it, independently-generated sections have no way
    /// to know what lightness their neighbors picked, and in practice often
    /// converged on the same "safe" dark (or light) variant of the shared
    /// palette. Fixing this at the role level guarantees adjacent-section
    /// contrast by construction.
    public IReadOnlyList<ContentSection> ContentSections { get; } = new ContentSection[]
    {
        new("hero", "An inviting header/hero introducing the subject by name, with a short tagline and a clear call-to-action.", "dark"),
        new("highlights", "The key features, offerings, menu items, services, or highlights relevant to the subject.", "light"),
        new("details", "Supporting detail — the subject's story, background, social proof, or testimonials, whichever fits best.", "dark"),
        new("footer", "A footer with contact info, hours, or location as relevant, and a final call-to-action.", "light"),
    };

    private static readonly string[] DesignBriefs =
    {
        "Warm earthy palette (deep browns, cream, terracotta accent), serif display headings, clean sans-serif body text, generous spacing, cozy artisanal tone.",
        "Cool modern palette (deep navy, white, a single vivid accent like teal or coral), bold geometric sans-serif headings, confident and minimal, tight spacing.",
        "Soft pastel palette (blush, sage, cream), rounded friendly sans-serif typography throughout, airy generous whitespace, approachable and calm tone.",
        "High-contrast monochrome (near-black, white, one sharp accent color), condensed bold headings, editorial/magazine feel, crisp structured grid.",
        "Nature-inspired palette (forest green, warm beige, soft gold accent), classic serif headings paired with humanist sans-serif body, organic and grounded tone.",
        "Vibrant energetic palette (deep purple, electric accent color, white), rounded modern sans-serif throughout, playful and bold, dynamic spacing.",
    };

    public string PickDesignBrief(string content)
    {
        // string.GetHashCode() is randomized per process in .NET (security
        // feature) — not stable across runs, so a simple stable sum is used
        // instead to keep the "same prompt text -> same brief" property real.
        var sum = content.Aggregate(0, (acc, c) => unchecked(acc + c));
        return DesignBriefs[sum % DesignBriefs.Length];
    }

    public string BuildFirstRunPrompt(string promptName, string content)
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
            "Requirements: a single HTML file with any needed CSS/JS inlined, no external dependencies (no CDNs, no external scripts) — three exceptions: a known font provider (e.g. Google Fonts via <link>/@import) may be used for better typography; real photos may be used via https://picsum.photos/seed/<descriptive-seed>/<width>/<height> (always returns a real photo — never guess a URL from any other image host or a specific photo id, since that's almost certainly a broken link); and a provided attachment's placeholder token is resolved to an embedded image, not an external resource. If any navigation link points elsewhere on the same page (e.g. a \"See Menu\" link), make sure a matching id actually exists on the page — a link to a section that doesn't exist is a broken page. Include a viewport meta tag and a descriptive <title> tag.",
            "Respond with ONLY the HTML document — no explanation, no markdown code fences.",
            "",
            $"Prompt name: {promptName}",
            "Prompt:",
            content,
        }));
    }

    /// Short, fast call: ONLY screening (safe/feasible + content-vs-app).
    /// Deliberately asks for nothing else — no design brief, no section
    /// plan — so this call's output stays tiny and its latency stays close
    /// to the ~4-6s floor measured for a minimal call in this environment,
    /// rather than scaling with how much creative content the model decides
    /// to describe up front.
    public string BuildFirstRunScreenPrompt(string promptName, string content)
    {
        return string.Join("\n",
            "You are screening a prompt for a tool called PromptVCS. Just the screening judgment — do not generate or describe any content, design, or layout.",
            "The people using this tool are non-technical and often describe what they want in just a few words. Treat brevity as creative freedom, not missing information — never ask for more detail, and this is not a reason to fail either check below.",
            "",
            "Assess:",
            "1. safe — is it free of genuinely unsafe/harmful content? Being brief or vague is not unsafe.",
            "2. feasible — can something reasonable be built from it as a single self-contained HTML page? Mark true unless the prompt is empty/gibberish, or explicitly requires something a single page structurally cannot provide (e.g. a real multi-user backend, a database, server-side payments).",
            "3. type — ONLY if safe and feasible: \"content\" for a content/marketing-style site (landing page, portfolio, informational site, storefront listing, etc.), or \"app\" for a tool with real interactive state/logic (a calculator, a todo list, a game, a converter, etc. — anything where UI and JS logic are too tightly coupled to generate in independent pieces). If not safe/feasible, type can be anything.",
            "4. name — ONLY if safe and feasible and type is \"content\": a short, specific brand/subject name (2-4 words) for this page. If the prompt already names one, use that exactly; if not, invent ONE plausible name. This exact name will be handed to several independent calls that generate the page's sections, so they stay consistent with each other instead of each inventing a different name — do not skip this or leave it generic.",
            "",
            "Respond with ONLY a single-line JSON object, nothing else:",
            "{\"safe\": boolean, \"safetyReason\": short string, \"feasible\": boolean, \"feasibilitySummary\": short string, \"type\": \"content\"|\"app\", \"name\": short string}",
            "Keep safetyReason and feasibilitySummary each to one short sentence.",
            "",
            $"Prompt name: {promptName}",
            "Prompt:",
            content);
    }

    /// One content section of a parallel-generated first-run page — several
    /// of these run concurrently against the same shared designBrief so
    /// they read as one cohesive page despite being generated independently.
    /// Uses a trimmed, section-specific quality bar rather than the full
    /// QualityBarLines() (shared with the full-page prompts) — several of
    /// those bullets (app JS functionality, calculator-specific input
    /// handling) don't apply to an individual content section.
    public string BuildSectionPrompt(string sharedName, string content, string designBrief, ContentSection section)
    {
        var toneInstruction = section.Tone == "dark"
            ? "This section must use the DARKEST, most saturated/deep color(s) from the shared design brief's own palette as its primary background (stay within that palette — don't jump to pure black if the brief doesn't call for it)."
            : "This section must use the LIGHTEST, most pale/airy color(s) from the shared design brief's own palette as its primary background (stay within that palette — don't jump to pure white if the brief doesn't call for it).";
        return string.Join("\n", new[]
        {
            "You are generating ONE section of a larger self-contained HTML page for a tool called PromptVCS. Several other sections are being generated in parallel by separate calls like this one — you will not see their output, so strict adherence to the shared design brief, shared name, and this section's assigned background weight below is the only thing keeping the final page consistent and visually rhythmic rather than flat.",
            $"Shared brand/subject name — use this EXACT name wherever the page names itself (e.g. in a logo/heading/footer), do not invent or use a different name: {sharedName}",
            $"Shared design brief — follow exactly, do not deviate or reinterpret: {designBrief}",
            $"Assigned background weight for THIS section (important — this is how sections stay visually distinct from their neighbors despite being generated independently): {toneInstruction}",
            $"This call's section: \"{section.Id}\" — {section.Role}",
            "",
            "Quality bar for this section:",
            "- Make confident, specific creative decisions for everything else (copy, details, offerings) that fits the overall prompt and the shared name above. Never use placeholder text like 'Lorem ipsum', '[Your text here]', or similar.",
            "- Genuinely polished: readable typography, sensible spacing, fits the shared design brief exactly.",
            "- Fully responsive at phone width and up.",
            "- No external dependencies, with three exceptions: a known font provider (e.g. a Google Fonts <link>/@import) may be used for better typography than a system font stack alone; real photos may be used via https://picsum.photos/seed/<descriptive-seed>/<width>/<height> (e.g. https://picsum.photos/seed/coffee-shop-hero/1200/600) — pick a descriptive seed text, this always returns a real photo, never guess a URL from any other image host or a specific photo id, since that would almost certainly be a broken link; and a provided attachment's placeholder token (if any, and if this section is where it belongs — e.g. a logo in a header) is resolved to an embedded image, not an external resource. No other external CDN links, scripts, or image URLs.",
            "- Keep this one section lean and efficient — roughly 60-120 words of visible copy and compact, non-repetitive CSS (reuse one class per logical element, don't over-decorate with extra wrapper divs or redundant rules). This is ONE part of a multi-section page, not the whole page: being concise here is correct, not a quality compromise — the page's overall polish comes from several well-made concise sections together, not from any single section being exhaustive.",
            "- If this section includes a call-to-action that should link elsewhere ON THIS PAGE (e.g. \"See Laptops\", \"View Menu\", \"Contact us\"), it may ONLY use one of these exact anchor targets, whichever content it's pointing at — these are guaranteed to exist, nothing else will: #hero, #highlights, #details, #footer. Never invent any other anchor id (e.g. #laptops, #pricing) — nothing else on the page is guaranteed to have that id, and a link to a non-existent section is a broken page, not a working one.",
            "",
            "Respond with ONLY a single HTML fragment for this section — a <header>, <section>, or <footer> element as appropriate (no <html>/<head>/<body>, no markdown code fences, no explanation). Any CSS this section needs goes in its own <style> block scoped under a unique class name for this section, so it can't collide with other sections' styles. Any JS this section needs (e.g. a mobile nav toggle) goes in its own <script> block, using only ids/classes scoped to this section. A provided attachment's placeholder token (if any, and if this section is where it belongs) is used verbatim as an <img src=\"...\">.",
            "",
            "Overall prompt (for tone/content fit — this section is only PART of the full page):",
            content,
        });
    }

    /// Shared between BuildFirstRunPrompt and BuildSectionPrompt
    private static IEnumerable<string> QualityBarLines() => new[]
    {
        "- Make confident, specific creative decisions to fill in anything unstated: invent a plausible name/brand, tagline, and realistic body copy that fits the theme. Never use placeholder text like 'Lorem ipsum', '[Your text here]', 'Company Name', or similar.",
        "- Produce a genuinely polished visual design: a cohesive modern color palette, readable typography with a clear hierarchy, sensible spacing, and multiple relevant sections — not a single heading and a paragraph.",
        "- If the prompt describes an app or tool (a calculator, a todo list, a converter, a game, etc.), implement REAL working functionality in JavaScript — actual computation, state, and interactivity. Never ship a static mockup or a button that does nothing.",
        "- Handle the range of input a real (non-technical) person would reasonably type, not just the narrowest valid case. For example, a calculator should accept natural notation like implicit multiplication ('8cos(6)', '2(3+4)', '3π') rather than showing a bare error for it; a form should tolerate minor formatting variation. Only show an error for genuinely invalid input, and make it specific, not just the word 'Error'.",
        "- Fully responsive at phone width and up.",
    };

    public string BuildUpdatePrompt(string promptName, string previousContent, string newContent, string previousArtifact)
    {
        var diffBuilder = new InlineDiffBuilder(new Differ());
        var diffResult = diffBuilder.BuildDiffModel(previousContent, newContent);
        var diffText = string.Join("\n", diffResult.Lines.Select(FormatDiffLine));

        return string.Join("\n",
            "You are updating an existing self-contained, single-file HTML artifact for a tool called PromptVCS, used by non-technical people.",
            "The prompt that generated it has changed. Below is a line diff of the prompt change (+ added, - removed) and the existing artifact.",
            "Apply a targeted update reflecting the diff — do not regenerate from scratch unless the diff requires it.",
            "Keep (or improve) the same quality bar as the original: no placeholder-looking text, a polished cohesive design, and real working functionality for anything interactive — never let an update make the result feel sparser or less finished than before.",
            "If the diff references an attached image (e.g. a logo) and you were given its placeholder token above, actually add an <img> using that exact placeholder token as its src in the appropriate place — don't just mention it in passing.",
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

    /// Forces the given section's root element to carry `id="{id}"`,
    /// overwriting any id the model used instead — guarantees the fixed
    /// anchor targets (#hero/#highlights/#details/#footer) a section's CTA
    /// is told it may link to actually exist on the assembled page,
    /// regardless of whether the model remembered to add one itself.
    /// ArtifactSanitizer's broken-internal-link check is the backstop for
    /// anything this can't fix.
    public string EnsureSectionId(string sectionHtml, string id)
    {
        var trimmed = sectionHtml.Trim();
        var tagMatch = Regex.Match(trimmed, @"^<(header|section|footer|div)\b[^>]*>", RegexOptions.IgnoreCase);
        if (!tagMatch.Success) return trimmed;

        var openTag = tagMatch.Value;
        var newOpenTag = Regex.IsMatch(openTag, @"\bid\s*=\s*[""'][^""']*[""']", RegexOptions.IgnoreCase)
            ? Regex.Replace(openTag, @"\bid\s*=\s*[""'][^""']*[""']", $"id=\"{id}\"", RegexOptions.IgnoreCase)
            : Regex.Replace(openTag, @"^<(\w+)", $"<$1 id=\"{id}\"", RegexOptions.IgnoreCase);

        return newOpenTag + trimmed[openTag.Length..];
    }
}
