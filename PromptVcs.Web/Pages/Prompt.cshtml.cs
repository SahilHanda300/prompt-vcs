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

    public PromptRecordDto? Record { get; set; }
    public int Version { get; set; }
    public List<DiffLineDto>? DiffLines { get; set; }
    public string? ErrorMessage { get; set; }
    public string SiteBaseUrl => ServerClient.SiteBaseUrl();

    public async Task<IActionResult> OnGetAsync()
    {
        var token = User.GetSessionToken();
        if (token == null) return RedirectToPage("/Login");

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

        try
        {
            await _client.EditAsync(Name, EditContent, token);
            return RedirectToPage("/Prompt", new { name = Name });
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
