namespace PromptVcs.Core;

/// <summary>
/// Abstraction over sanity-checking/normalizing Claude Code's raw output
/// into a servable artifact. Single responsibility: pure text-in/text-out
/// validation — no file I/O (that's <see cref="IArtifactWriter"/>) and no
/// knowledge of prompts/Claude Code (that's <see cref="IGenerationPromptBuilder"/>
/// and <see cref="IClaudeCodeInvoker"/>).
/// </summary>
public interface IArtifactSanitizer
{
    SanitizeResult Sanitize(string raw);
}
