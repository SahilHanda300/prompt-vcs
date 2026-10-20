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

    // Carries the success message across the redirect that follows a
    // credential change — see OnPostUpdateAccountAsync for why that handler
    // redirects instead of re-rendering Page() directly like the others.
    [TempData]
    public string? FlashSuccessMessage { get; set; }

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

        SuccessMessage ??= FlashSuccessMessage;
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
                successes.Add($"username changed to \"{result.Username}\"");
            }
            if (wantsPasswordChange)
            {
                await _client.ChangePasswordAsync(CurrentPassword, NewPassword, token);
                successes.Add("password changed");
            }

            if (wantsUsernameChange)
            {
                // Force a clean re-login instead of reissuing this session's
                // cookie in place. ASP.NET Core's antiforgery tokens are
                // bound to the authenticated username, and there is no way
                // to atomically swap "the cookie" and "every antiforgery
                // token already rendered into this and other open tabs" at
                // the same instant — a prior attempt here (reissue + redirect
                // back to /Admin) still left a window where the background
                // cookie-revalidation in Program.cs (OnValidatePrincipal)
                // could swap HttpContext.User to the new username mid
                // request, out from under a token that was rendered for the
                // old one, producing the exact "meant for a different
                // claims-based user" 400 this replaces. Signing out removes
                // all of that identity-bound state in one step — the next
                // antiforgery token the admin gets is minted for an
                // anonymous request, and only becomes username-bound again
                // after they log back in with the new name.
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                // Shares the "InfoMessage" TempData key LoginModel reads —
                // TempData is one dictionary per session, not per page type.
                TempData["InfoMessage"] = successes.Count > 1
                    ? $"Username changed to \"{NewUsername.Trim()}\" and password changed — log in with your new username."
                    : $"Username changed to \"{NewUsername.Trim()}\" — log in with your new username.";
                return RedirectToPage("/Login");
            }

            FlashSuccessMessage = char.ToUpperInvariant(successes[0][0]) + successes[0][1..] + ".";
            return RedirectToPage();
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
