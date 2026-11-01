using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PromptVcs.Web.Pages;

public class DashboardModel : PageModel
{
    private readonly ServerClient _client;

    public DashboardModel(ServerClient client)
    {
        _client = client;
    }

    public List<PromptListItemDto> Prompts { get; set; } = new();
    public string? ErrorMessage { get; set; }

    // OnPostToggleFavoriteAsync redirects back here (PRG) rather than
    // re-rendering directly, so a plain ErrorMessage set before that
    // redirect wouldn't survive into the page that renders it.
    [TempData]
    public string? FlashErrorMessage { get; set; }

    [BindProperty]
    public string NewPromptName { get; set; } = "";

    [BindProperty]
    public string NewPromptContent { get; set; } = "";

    [BindProperty]
    public List<IFormFile>? Attachments { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        ErrorMessage ??= FlashErrorMessage;
        await LoadPromptsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var token = User.GetSessionToken();
        if (token == null) return RedirectToPage("/Login");

        if (string.IsNullOrWhiteSpace(NewPromptName) || string.IsNullOrWhiteSpace(NewPromptContent))
        {
            ErrorMessage = "Name and content are both required.";
            await LoadPromptsAsync();
            return Page();
        }

        var (attachments, attachmentError) = await AttachmentHelper.ToAttachmentDtosAsync(Attachments);
        if (attachmentError != null)
        {
            ErrorMessage = attachmentError;
            await LoadPromptsAsync();
            return Page();
        }

        try
        {
            await _client.CreateAsync(NewPromptName.Trim(), NewPromptContent, attachments, token);
            return RedirectToPage("/Prompt", new { name = NewPromptName.Trim() });
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            await LoadPromptsAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostToggleFavoriteAsync(string name)
    {
        var token = User.GetSessionToken();
        if (token == null) return RedirectToPage("/Login");

        try
        {
            await _client.ToggleFavoriteAsync(name, token);
        }
        catch (InvalidOperationException ex)
        {
            FlashErrorMessage = ex.Message;
        }
        return RedirectToPage("/Dashboard");
    }

    private async Task LoadPromptsAsync()
    {
        var token = User.GetSessionToken();
        if (token == null) return;

        try
        {
            Prompts = await _client.ListAsync(token);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
