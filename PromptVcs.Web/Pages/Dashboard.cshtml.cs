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

    [BindProperty]
    public string NewPromptName { get; set; } = "";

    [BindProperty]
    public string NewPromptContent { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
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

        try
        {
            await _client.CreateAsync(NewPromptName.Trim(), NewPromptContent, token);
            return RedirectToPage("/Prompt", new { name = NewPromptName.Trim() });
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            await LoadPromptsAsync();
            return Page();
        }
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
