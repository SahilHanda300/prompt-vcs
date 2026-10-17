using System.Security.Claims;

namespace PromptVcs.Web;

public static class ClaimsPrincipalExtensions
{
    public const string SessionTokenClaimType = "promptvcs_session_token";
    public const string IsAdminClaimType = "promptvcs_is_admin";
    // When this claim was last confirmed against the live server (whoAmI),
    // not when the cookie itself was issued — lets Program.cs's
    // OnValidatePrincipal throttle re-checks instead of calling the MCP
    // server on every single request. See RenameOwner/AdminUpdateUserAsync:
    // a role or username change made by an admin takes effect server-side
    // immediately, but without this re-check a browser's existing cookie
    // would keep granting the OLD role/username for up to 30 days.
    public const string ValidatedAtClaimType = "promptvcs_validated_at";

    public static string? GetSessionToken(this ClaimsPrincipal user) =>
        user.FindFirst(SessionTokenClaimType)?.Value;

    public static bool IsPromptVcsAdmin(this ClaimsPrincipal user) =>
        user.FindFirst(IsAdminClaimType)?.Value == "true";

    /// One place that builds the auth cookie's claim set, used by every
    /// sign-in call site (Login, Register, Settings' rename, and the
    /// periodic re-validation in Program.cs) so none of them can drift
    /// out of sync with each other on what a "signed in" principal looks
    /// like.
    public static List<Claim> BuildClaims(string username, string sessionToken, bool isAdmin) => new()
    {
        new(ClaimTypes.Name, username),
        new(SessionTokenClaimType, sessionToken),
        new(IsAdminClaimType, isAdmin ? "true" : "false"),
        new(ValidatedAtClaimType, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
    };
}
