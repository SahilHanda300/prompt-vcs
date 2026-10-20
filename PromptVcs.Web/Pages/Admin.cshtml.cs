using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PromptVcs.Web.Pages;

public class AdminModel : PageModel
{
    private readonly ServerClient _client;

    public AdminModel(ServerClient client)
    {
        _client = client;
    }

    public List<UserSummaryDto> Users { get; set; } = new();
    public List<PromptListItemDto> Prompts { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public string NewUserUsername { get; set; } = "";

    [BindProperty]
    public string NewUserPassword { get; set; } = "";

    [BindProperty]
    public bool NewUserIsAdmin { get; set; }

    // "Your account" — the admin's own username/password change, moved here
    // from /Settings (which admins no longer use) so there's exactly one
    // place, with exactly one security model (current-password
    // reverification), for an admin to change their own credentials. The
    // per-row edit form further down is for managing OTHER users and
    // deliberately has no such requirement — that's an admin override, not
    // a self-service account change.
    //
    // One CurrentPassword field covers both a username change and a
    // password change — originally two separate forms, each with its own
    // "current password" field, meaning changing both on one visit meant
    // typing the same password twice. Either NewUsername or NewPassword
    // (or both) may be filled in; CurrentPassword is always required.
    [BindProperty]
    public string CurrentPassword { get; set; } = "";

    [BindProperty]
    public string NewUsername { get; set; } = "";

    [BindProperty]
    public string NewPassword { get; set; } = "";

    [BindProperty]
    public string ConfirmNewPassword { get; set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        var token = User.GetSessionToken();
        if (token == null) return RedirectToPage("/Login");
        // Belt-and-suspenders: the nav only shows this link to admins, but a
        // non-admin typing the URL directly should land back on their own
        // dashboard, not an error page or an empty admin view.
        if (!User.IsPromptVcsAdmin()) return RedirectToPage("/Dashboard");

        await LoadAsync(token);
        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAccountAsync()
    {
        var (tokenOrNull, redirect) = RequireAdmin();
        if (redirect != null) return redirect;
        var token = tokenOrNull!;

        var wantsUsernameChange = !string.IsNullOrWhiteSpace(NewUsername);
        var wantsPasswordChange = !string.IsNullOrWhiteSpace(NewPassword);

        if (string.IsNullOrWhiteSpace(CurrentPassword) || (!wantsUsernameChange && !wantsPasswordChange))
        {
            ErrorMessage = "Enter your current password, and a new username and/or new password.";
            await LoadAsync(token);
            return Page();
        }
        if (wantsPasswordChange && NewPassword != ConfirmNewPassword)
        {
            ErrorMessage = "New password and confirmation don't match.";
            await LoadAsync(token);
            return Page();
        }

        try
        {
            var successes = new List<string>();

            // Username first — it doesn't touch the password hash, so
            // CurrentPassword is still valid for the password change below
            // regardless of order.
            if (wantsUsernameChange)
            {
                var result = await _client.ChangeUsernameAsync(CurrentPassword, NewUsername.Trim(), token);
                // Re-issue the cookie with the new name — otherwise the page
                // (and the rest of the app) would keep showing the old
                // username until the cookie's natural 30-day expiry.
                await SignInAsync(result.Username, token, isAdmin: true);
                successes.Add($"username changed to \"{result.Username}\"");
            }
            if (wantsPasswordChange)
            {
                await _client.ChangePasswordAsync(CurrentPassword, NewPassword, token);
                successes.Add("password changed");
            }

            SuccessMessage = char.ToUpperInvariant(successes[0][0]) + successes[0][1..] +
                (successes.Count > 1 ? " and " + successes[1] : "") + ".";
            CurrentPassword = "";
            NewUsername = "";
            NewPassword = "";
            ConfirmNewPassword = "";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        await LoadAsync(token);
        return Page();
    }

    public async Task<IActionResult> OnPostResetAsync()
    {
        var (tokenOrNull, redirect) = RequireAdmin();
        if (redirect != null) return redirect;
        var token = tokenOrNull!;

        try
        {
            await _client.ResetAsync(token);
            SuccessMessage = "Store reset. All prompts and generated sites deleted.";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        await LoadAsync(token);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateUserAsync()
    {
        var (tokenOrNull, redirect) = RequireAdmin();
        if (redirect != null) return redirect;
        var token = tokenOrNull!;

        if (string.IsNullOrWhiteSpace(NewUserUsername) || string.IsNullOrWhiteSpace(NewUserPassword))
        {
            ErrorMessage = "Username and password are both required.";
            await LoadAsync(token);
            return Page();
        }

        try
        {
            await _client.CreateUserAsync(NewUserUsername.Trim(), NewUserPassword, NewUserIsAdmin, token);
            SuccessMessage = $"Created user \"{NewUserUsername.Trim()}\".";
            NewUserUsername = "";
            NewUserPassword = "";
            NewUserIsAdmin = false;
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        await LoadAsync(token);
        return Page();
    }

    // Edit-user fields come from the per-row <details> form as plain
    // handler parameters, not [BindProperty] — several near-identical forms
    // share this page, and binding them to class properties risked one
    // form's empty fields silently overwriting another's just-submitted
    // values on redisplay.
    public async Task<IActionResult> OnPostUpdateUserAsync(string targetUsername, string? editUsername, string? editPassword, string? editRole)
    {
        var (tokenOrNull, redirect) = RequireAdmin();
        if (redirect != null) return redirect;
        var token = tokenOrNull!;

        // editRole is a <select> with three options — "", "true", "false" —
        // not a checkbox, specifically so "leave the role unchanged" can be
        // expressed distinctly from "set it to false (a normal user)",
        // which a plain checkbox can't do (an unchecked box submits nothing
        // at all, indistinguishable from "not present in the form").
        bool? isAdmin = editRole switch { "true" => true, "false" => false, _ => null };
        var newUsername = string.IsNullOrWhiteSpace(editUsername) ? null : editUsername.Trim();
        var newPassword = string.IsNullOrWhiteSpace(editPassword) ? null : editPassword;

        if (newUsername == null && newPassword == null && isAdmin == null)
        {
            ErrorMessage = "Nothing to update — fill in at least one field.";
            await LoadAsync(token);
            return Page();
        }

        try
        {
            var result = await _client.UpdateUserAsync(targetUsername, newUsername, newPassword, isAdmin, token);
            SuccessMessage = $"Updated \"{targetUsername}\"" + (result.Username != targetUsername ? $" (now \"{result.Username}\")." : ".");
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        await LoadAsync(token);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteUserAsync(string targetUsername)
    {
        var (tokenOrNull, redirect) = RequireAdmin();
        if (redirect != null) return redirect;
        var token = tokenOrNull!;

        try
        {
            await _client.DeleteUserAsync(targetUsername, token);
            SuccessMessage = $"Deleted user \"{targetUsername}\".";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        await LoadAsync(token);
        return Page();
    }

    public async Task<IActionResult> OnPostDeletePromptAsync(string name)
    {
        var (tokenOrNull, redirect) = RequireAdmin();
        if (redirect != null) return redirect;
        var token = tokenOrNull!;

        try
        {
            await _client.DeletePromptAsync(name, token);
            SuccessMessage = $"Deleted prompt \"{name}\".";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        await LoadAsync(token);
        return Page();
    }

    /// Distinguishes "not logged in at all" (→ /Login) from "logged in but
    /// not an admin" (→ /Dashboard, same as OnGetAsync's guard) — these
    /// need different redirects, not just a single "no token" null check.
    private (string? Token, IActionResult? Redirect) RequireAdmin()
    {
        var token = User.GetSessionToken();
        if (token == null) return (null, RedirectToPage("/Login"));
        if (!User.IsPromptVcsAdmin()) return (null, RedirectToPage("/Dashboard"));
        return (token, null);
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

    private async Task LoadAsync(string token)
    {
        try
        {
            Users = await _client.ListUsersAsync(token);
            // An admin's own "list" call already returns every prompt (with
            // owner), not just the admin's — same tool, no separate
            // "admin list" endpoint needed, see PromptTools.List.
            Prompts = await _client.ListAsync(token);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage ??= ex.Message;
        }
    }
}
