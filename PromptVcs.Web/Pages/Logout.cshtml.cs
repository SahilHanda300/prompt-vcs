using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PromptVcs.Web.Pages;

public class LogoutModel : PageModel
{
    private readonly ServerClient _client;

    public LogoutModel(ServerClient client)
    {
        _client = client;
    }

    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        var token = User.GetSessionToken();
        if (token != null)
        {
            try
            {
                await _client.LogoutAsync(token);
            }
            catch (InvalidOperationException)
            {
                // Best-effort — the server session may already be gone; the
                // cookie sign-out below is what actually matters client-side.
            }
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
