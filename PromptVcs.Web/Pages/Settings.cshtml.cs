using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PromptVcs.Web.Pages;

public class SettingsModel : PageModel
{
    private readonly ServerClient _client;

    public SettingsModel(ServerClient client)
    {
        _client = client;
    }

    [BindProperty]
    public string NewUsername { get; set; } = "";

    [BindProperty]
    public string UsernamePassword { get; set; } = "";

    [BindProperty]
    public string CurrentPassword { get; set; } = "";

    [BindProperty]
    public string NewPassword { get; set; } = "";

    [BindProperty]
    public string ConfirmNewPassword { get; set; } = "";

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string CurrentUsername => User.Identity?.Name ?? "";

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostChangeUsernameAsync()
    {
        var token = User.GetSessionToken();
        if (token == null) return RedirectToPage("/Login");

        if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(UsernamePassword))
        {
            ErrorMessage = "New username and your current password are both required.";
            return Page();
        }

        try
        {
            var result = await _client.ChangeUsernameAsync(UsernamePassword, NewUsername.Trim(), token);
            // The auth cookie carries the username as a claim — it has to be
            // re-issued with the new value, or the page would keep showing
            // (and the rest of the app would keep acting as) the old name
            // until the cookie naturally expired.
            await SignInAsync(result.Username, token, User.IsPromptVcsAdmin());
            SuccessMessage = $"Username changed to \"{result.Username}\".";
            NewUsername = "";
            UsernamePassword = "";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        var token = User.GetSessionToken();
        if (token == null) return RedirectToPage("/Login");

        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "Current and new password are both required.";
            return Page();
        }
        if (NewPassword != ConfirmNewPassword)
        {
            ErrorMessage = "New password and confirmation don't match.";
            return Page();
        }

        try
        {
            await _client.ChangePasswordAsync(CurrentPassword, NewPassword, token);
            SuccessMessage = "Password changed.";
            CurrentPassword = "";
            NewPassword = "";
            ConfirmNewPassword = "";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        return Page();
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
