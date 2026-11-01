using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PromptVcs.Web.Pages;

public class PromptModel : PageModel
{
    private readonly ServerClient _client;

    public PromptModel(ServerClient client)
    {
        _client = client;
    }

    [BindProperty(SupportsGet = true)]
    public string Name { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public int? V1 { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? V2 { get; set; }

    [BindProperty]
    public string EditContent { get; set; } = "";

    [BindProperty]
    public List<IFormFile>? Attachments { get; set; }

    [BindProperty]
    public string NewPromptName { get; set; } = "";

    public PromptRecordDto? Record { get; set; }
    public int Version { get; set; }
    public List<DiffLineDto>? DiffLines { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string SiteBaseUrl => ServerClient.SiteBaseUrl();

    // Carries a success message across the redirect that follows a run/
    // rename — both are PRG (redirect-on-success), so a plain property set
    // before the redirect wouldn't survive into the page that renders it.
    [TempData]
    public string? FlashSuccessMessage { get; set; }

    // A legacy (ownerless) prompt, or one the viewer owns, gets the normal
    // "edit content and re-run" form. An admin viewing someone else's
    // prompt (the only other way to reach this page — a non-admin viewing
    // another user's prompt is rejected server-side before Record is ever
    // set) gets a read-only view instead: changing someone else's prompt
    // text isn't an admin action, it's silently speaking for them. What an
    // admin can do there is rename or delete the prompt — see
    // OnPostRenameAsync/OnPostDeleteAsync.
    public bool CanEditContent => Record == null || Record.Owner == null || Record.Owner == User.Identity?.Name;

    public async Task<IActionResult> OnGetAsync()
    {
        var token = User.GetSessionToken();
        if (token == null) return RedirectToPage("/Login");

        SuccessMessage ??= FlashSuccessMessage;
        await LoadAsync(token);

        if (V1.HasValue && V2.HasValue)
        {
            try
            {
                DiffLines = await _client.DiffAsync(Name, V1.Value, V2.Value, token);
            }
            catch (InvalidOperationException ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        var token = User.GetSessionToken();
        if (token == null) return RedirectToPage("/Login");

        if (string.IsNullOrWhiteSpace(EditContent))
        {
            ErrorMessage = "Content is required.";
            await LoadAsync(token);
            return Page();
        }

        var (attachments, attachmentError) = await AttachmentHelper.ToAttachmentDtosAsync(Attachments);
        if (attachmentError != null)
        {
            ErrorMessage = attachmentError;
            await LoadAsync(token);
            return Page();
        }

        try
        {
            await _client.EditAsync(Name, EditContent, attachments, token);
            return RedirectToPage("/Prompt", new { name = Name });
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            await LoadAsync(token);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostToggleFavoriteAsync()
    {
        var token = User.GetSessionToken();
        if (token == null) return RedirectToPage("/Login");

        try
        {
            await _client.ToggleFavoriteAsync(Name, token);
            return RedirectToPage("/Prompt", new { name = Name });
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            await LoadAsync(token);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var token = User.GetSessionToken();
        if (token == null) return RedirectToPage("/Login");

        try
        {
            await _client.DeletePromptAsync(Name, token);
            return RedirectToPage("/Dashboard");
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            await LoadAsync(token);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRenameAsync()
    {
        var token = User.GetSessionToken();
        if (token == null) return RedirectToPage("/Login");

        if (string.IsNullOrWhiteSpace(NewPromptName))
        {
            ErrorMessage = "New name is required.";
            await LoadAsync(token);
            return Page();
        }

        try
        {
            var result = await _client.RenamePromptAsync(Name, NewPromptName.Trim(), token);
            FlashSuccessMessage = $"Renamed to \"{result.Name}\".";
            return RedirectToPage("/Prompt", new { name = result.Name });
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            await LoadAsync(token);
            return Page();
        }
    }

    private async Task LoadAsync(string token)
    {
        try
        {
            var result = await _client.ShowAsync(Name, null, token);
            Record = result.Record;
            Version = result.Version;
            EditContent = result.Content;
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
