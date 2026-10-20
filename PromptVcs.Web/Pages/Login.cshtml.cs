using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PromptVcs.Web.Pages;

public class LoginModel : PageModel
{
    private readonly ServerClient _client;

    public LoginModel(ServerClient client)
    {
        _client = client;
    }

    [BindProperty]
    public string Username { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public string? ErrorMessage { get; set; }

    // Set by Admin/Settings after a username change forces a sign-out — see
    // those pages' OnPostUpdateAccountAsync/OnPostChangeUsernameAsync for why.
    [TempData]
    public string? InfoMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Username and password are required.";
            return Page();
        }

        try
        {
            var result = await _client.LoginAsync(Username.Trim(), Password);
            await SignInAsync(Username.Trim(), result.SessionToken!, result.IsAdmin);
            // An explicit returnUrl (e.g. bounced here from a deep link)
            // still wins; otherwise an admin's default landing page is the
            // admin dashboard, not the regular prompt dashboard.
            var defaultPage = result.IsAdmin ? "/Admin" : "/Dashboard";
            return LocalRedirect(string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl) ? defaultPage : returnUrl);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    private async Task SignInAsync(string username, string sessionToken, bool isAdmin)
    {
        var identity = new ClaimsIdentity(
            ClaimsPrincipalExtensions.BuildClaims(username, sessionToken, isAdmin),
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
    }
}
