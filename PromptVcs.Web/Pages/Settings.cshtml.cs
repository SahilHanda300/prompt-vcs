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

    // Carries the success message across the redirect that follows a
    // username change — see OnPostChangeUsernameAsync.
    [TempData]
    public string? FlashSuccessMessage { get; set; }

    public IActionResult OnGet()
    {
        // Admins manage their own username/password from the Admin panel
        // now (a "Your account" section there), not here — having the same
        // two forms live in two places was the actual complaint, not just
        // where they lived, so an admin landing here gets sent to the one
        // place that still has them.
        if (User.IsPromptVcsAdmin()) return RedirectToPage("/Admin");
        SuccessMessage ??= FlashSuccessMessage;
        return Page();
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
            // Force a clean re-login rather than reissuing this session's
            // cookie in place — see Admin.cshtml.cs's OnPostUpdateAccountAsync
            // for why. Signing out removes every bit of identity-bound
            // antiforgery state in one step instead of trying to keep the
            // cookie and open forms' tokens in lockstep.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["InfoMessage"] = $"Username changed to \"{result.Username}\" — log in with your new username.";
            return RedirectToPage("/Login");
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
}
