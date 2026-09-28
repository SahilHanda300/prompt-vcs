using Microsoft.AspNetCore.Authentication.Cookies;
using PromptVcs.Web;

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
    });

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizePage("/Dashboard");
    options.Conventions.AuthorizePage("/Prompt");
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
