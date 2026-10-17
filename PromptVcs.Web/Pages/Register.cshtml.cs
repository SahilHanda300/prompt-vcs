using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PromptVcs.Web.Pages;

public class RegisterModel : PageModel
{
    private const int MinPasswordLength = 8;

    private readonly ServerClient _client;

    public RegisterModel(ServerClient client)
    {
        _client = client;
    }

    [BindProperty]
    public string Username { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Username and password are required.";
            return Page();
        }
        if (Password.Length < MinPasswordLength)
        {
            ErrorMessage = $"Password must be at least {MinPasswordLength} characters.";
            return Page();
        }

        try
        {
            var result = await _client.RegisterAsync(Username.Trim(), Password);
            await SignInAsync(Username.Trim(), result.SessionToken!, result.IsAdmin);
            // The very first user ever registered becomes admin automatically
            // (see MongoAuthService) — they land on the admin dashboard from
            // the start, not the regular prompt dashboard.
            return LocalRedirect(result.IsAdmin ? "/Admin" : "/Dashboard");
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
