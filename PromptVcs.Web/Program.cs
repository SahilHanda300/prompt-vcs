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

builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
