using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PromptVcs.Web;

// How often an already-signed-in cookie's role/username is re-checked
// against the live server, rather than trusted as-is until it expires.
// Exists because an admin action (demote/rename, see AdminUpdateUserAsync)
// takes effect in MongoDB immediately, but a browser's cookie is an
// independent copy of that same information made at login time — without
// this, a just-demoted admin would keep seeing the "Admin" nav link and
// reaching /Admin for up to 30 days, until their cookie happened to expire.
var revalidationInterval = TimeSpan.FromSeconds(30);

const string DefaultPort = "5285";
// Render (and most PaaS hosts) inject PORT and expect the app to bind it;
// PROMPTVCS_WEB_PORT stays as a manual override for local/other-host use.
var port = Environment.GetEnvironmentVariable("PORT")
    ?? Environment.GetEnvironmentVariable("PROMPTVCS_WEB_PORT")
    ?? DefaultPort;

var builder = WebApplication.CreateBuilder(args);
// 0.0.0.0, not localhost — localhost only accepts loopback connections, so
// a container's own healthcheck/reverse proxy (or anyone outside the
// container) couldn't reach the app at all if it bound to localhost.
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddSingleton<ServerClient>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "PromptVcsWebAuth";
        options.LoginPath = "/Login";
        // Matches MongoAuthService.SessionLifetime server-side — the cookie
        // shouldn't outlive the session token it carries.
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            var validatedAtRaw = context.Principal?.FindFirst(ClaimsPrincipalExtensions.ValidatedAtClaimType)?.Value;
            var token = context.Principal?.GetSessionToken();
            if (validatedAtRaw == null || token == null || !long.TryParse(validatedAtRaw, out var validatedAtSeconds))
            {
                // Cookie predates this claim (issued by an old build) or is
                // otherwise malformed — don't guess at its role, just make
                // the user log back in.
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var validatedAt = DateTimeOffset.FromUnixTimeSeconds(validatedAtSeconds);
            if (DateTimeOffset.UtcNow - validatedAt < revalidationInterval)
            {
                return; // checked recently enough — trust the cookie as-is.
            }

            var client = context.HttpContext.RequestServices.GetRequiredService<ServerClient>();
            try
            {
                var who = await client.WhoAmIAsync(token);
                var identity = new ClaimsIdentity(
                    ClaimsPrincipalExtensions.BuildClaims(who.Username, token, who.IsAdmin),
                    CookieAuthenticationDefaults.AuthenticationScheme);
                context.ReplacePrincipal(new ClaimsPrincipal(identity));
                context.ShouldRenew = true;
            }
            catch (InvalidOperationException)
            {
                // Session no longer valid server-side (logged out elsewhere,
                // revoked, or the account was deleted) — a stale cookie must
                // not keep granting access on the strength of its own say-so.
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizePage("/Dashboard");
    options.Conventions.AuthorizePage("/Prompt");
    options.Conventions.AuthorizePage("/Settings");
    options.Conventions.AuthorizePage("/Admin");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
