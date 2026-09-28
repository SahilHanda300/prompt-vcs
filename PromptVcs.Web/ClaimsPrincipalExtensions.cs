using System.Security.Claims;

namespace PromptVcs.Web;

public static class ClaimsPrincipalExtensions
{
    public const string SessionTokenClaimType = "promptvcs_session_token";
    public const string IsAdminClaimType = "promptvcs_is_admin";

    public static string? GetSessionToken(this ClaimsPrincipal user) =>
        user.FindFirst(SessionTokenClaimType)?.Value;

    public static bool IsPromptVcsAdmin(this ClaimsPrincipal user) =>
        user.FindFirst(IsAdminClaimType)?.Value == "true";
}
