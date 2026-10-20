using System.Text;

namespace PromptVcs.Core;

public class ArtifactWriter : IArtifactWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _siteRootDir;

    public ArtifactWriter(string siteRootDir)
    {
        _siteRootDir = siteRootDir;
    }

    public async Task<ArtifactWriteResult> WriteAsync(string promptId, int buildVersion, string html)
    {
        var siteDir = Path.Combine(_siteRootDir, promptId);
        var versionedDir = Path.Combine(siteDir, $"v{buildVersion}");
        Directory.CreateDirectory(versionedDir);
        await File.WriteAllTextAsync(Path.Combine(versionedDir, "index.html"), html, Utf8NoBom);
        await File.WriteAllTextAsync(Path.Combine(siteDir, "index.html"), html, Utf8NoBom);

        return new ArtifactWriteResult($"/site/{promptId}/v{buildVersion}/");
    }

    public async Task<string?> ReadLatestAsync(string promptId)
    {
        var path = Path.Combine(_siteRootDir, promptId, "index.html");
        return File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
    }
}
