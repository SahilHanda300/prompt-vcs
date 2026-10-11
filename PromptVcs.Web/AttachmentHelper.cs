using Microsoft.AspNetCore.Http;

namespace PromptVcs.Web;

/// Converts uploaded files from a Razor Pages form into the base64 shape
/// the "attachments" MCP tool argument expects. Mirrors the server-side
/// validation (mcp-server/Tools/PromptTools.cs's ValidateAttachments) so
/// the user gets a fast, friendly error instead of a round trip just to
/// find out a file was too big or the wrong type.
public static class AttachmentHelper
{
    public const int MaxAttachments = 4;
    public const int MaxAttachmentBytes = 2 * 1024 * 1024;

    public static async Task<(List<AttachmentDto>? Attachments, string? Error)> ToAttachmentDtosAsync(List<IFormFile>? files)
    {
        var nonEmpty = files?.Where(f => f.Length > 0).ToList();
        if (nonEmpty == null || nonEmpty.Count == 0) return (null, null);

        if (nonEmpty.Count > MaxAttachments)
        {
            return (null, $"Too many attachments (max {MaxAttachments}).");
        }

        var result = new List<AttachmentDto>();
        foreach (var file in nonEmpty)
        {
            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return (null, $"\"{file.FileName}\" is not an image. Only image attachments are supported.");
            }
            if (file.Length > MaxAttachmentBytes)
            {
                return (null, $"\"{file.FileName}\" exceeds the {MaxAttachmentBytes / (1024 * 1024)}MB limit.");
            }

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            result.Add(new AttachmentDto(file.FileName, file.ContentType, Convert.ToBase64String(stream.ToArray())));
        }
        return (result, null);
    }
}
