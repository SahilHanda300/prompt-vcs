namespace PromptVcs.Core;

public record ContentSection(string Id, string Role, string Tone);

/// <summary>
/// Abstraction over constructing the actual prompt text sent to Claude
/// Code for generation — single responsibility: prompt templates only, no
/// Claude Code invocation, no file I/O, no sanitization. Swappable/testable
/// independently of <see cref="PublishRules"/>'s orchestration.
/// </summary>
public interface IGenerationPromptBuilder
{
    string BuildFirstRunPrompt(string promptName, string content);
    string BuildFirstRunScreenPrompt(string promptName, string content);
    string BuildUpdatePrompt(string promptName, string previousContent, string newContent, string previousArtifact);

    /// Always these 4 roles, in this fixed order, for a parallel-generated
    /// content-type first run — see GenerationPromptBuilder for why this is
    /// fixed rather than model-decided.
    IReadOnlyList<ContentSection> ContentSections { get; }

    string BuildSectionPrompt(string sharedName, string content, string designBrief, ContentSection section);

    /// Deterministic per prompt text (not random), so re-running the exact
    /// same prompt lands on the same brief.
    string PickDesignBrief(string content);

    /// Forces the given section's root element to carry `id="{id}"`,
    /// overwriting any id the model used instead.
    string EnsureSectionId(string sectionHtml, string id);
}
