using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace PromptVcs.Web;

/// <summary>
/// The web app's only connection to the outside world: a real MCP client
/// calling the server's login/register/logout and create/edit/list/show/diff
/// tools — same tool surface PromptVcs.Cli uses, mirrored here rather than
/// shared, per the thin-client architecture decision. Unlike the CLI (which
/// caches its session token in a local file, since it's a single-user
/// process), a browser session belongs to one HTTP request/response cycle at
/// a time, so every call here takes the caller's sessionToken explicitly —
/// callers (page models) source it from the signed-in cookie's claims.
/// </summary>
public class ServerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static string McpUrl() => Environment.GetEnvironmentVariable("PROMPTVCS_MCP_URL") ?? "http://localhost:5279/mcp";

    /// Base URL for resolving the server's relative artifact links
    /// (e.g. "/site/<promptId>/") into absolute ones — needed because this
    /// app is deployed as its own Render service, on its own domain, not
    /// sharing a host with the MCP server the way the browser terminal's
    /// spawned CLI does over localhost.
    public static string SiteBaseUrl()
    {
        var url = McpUrl();
        return url.EndsWith("/mcp", StringComparison.OrdinalIgnoreCase) ? url[..^4] : url;
    }

    public Task<AuthResultDto> LoginAsync(string username, string password) =>
        CallAsync<AuthResultDto>("login", new Dictionary<string, object?> { ["username"] = username, ["password"] = password });

    public Task<AuthResultDto> RegisterAsync(string username, string password) =>
        CallAsync<AuthResultDto>("register", new Dictionary<string, object?> { ["username"] = username, ["password"] = password });

    public Task<LogoutResultDto> LogoutAsync(string sessionToken) =>
        CallAsync<LogoutResultDto>("logout", new Dictionary<string, object?> { ["sessionToken"] = sessionToken });

    public Task<PipelineResultDto> CreateAsync(string name, string content, string sessionToken) =>
        CallAsync<PipelineResultDto>("create", new Dictionary<string, object?> { ["name"] = name, ["content"] = content, ["sessionToken"] = sessionToken });

    public Task<PipelineResultDto> EditAsync(string name, string content, string sessionToken) =>
        CallAsync<PipelineResultDto>("edit", new Dictionary<string, object?> { ["name"] = name, ["content"] = content, ["sessionToken"] = sessionToken });

    public Task<List<PromptListItemDto>> ListAsync(string sessionToken) =>
        CallAsync<List<PromptListItemDto>>("list", new Dictionary<string, object?> { ["sessionToken"] = sessionToken });

    public Task<ShowResultDto> ShowAsync(string name, int? version, string sessionToken) =>
        CallAsync<ShowResultDto>("show", new Dictionary<string, object?> { ["name"] = name, ["version"] = version, ["sessionToken"] = sessionToken });

    public Task<List<DiffLineDto>> DiffAsync(string name, int v1, int v2, string sessionToken) =>
        CallAsync<List<DiffLineDto>>("diff", new Dictionary<string, object?> { ["name"] = name, ["v1"] = v1, ["v2"] = v2, ["sessionToken"] = sessionToken });

    private static async Task<T> CallAsync<T>(string toolName, Dictionary<string, object?> arguments)
    {
        var options = new HttpClientTransportOptions { Endpoint = new Uri(McpUrl()) };
        var transport = new HttpClientTransport(options);

        McpClient client;
        try
        {
            client = await McpClient.CreateAsync(transport);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"MCP server not reachable at {McpUrl()}: {ex.Message}");
        }

        await using (client)
        {
            CallToolResult result;
            try
            {
                result = await client.CallToolAsync(toolName, arguments!);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Call to \"{toolName}\" failed: {ex.Message}");
            }

            var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
            if (text == null)
            {
                throw new InvalidOperationException($"\"{toolName}\" returned no content.");
            }
            if (result.IsError == true)
            {
                throw new InvalidOperationException($"\"{toolName}\" failed: {text}");
            }

            JsonElement envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<JsonElement>(text, JsonOptions);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException($"\"{toolName}\" returned unparseable response: {text}");
            }

            var status = envelope.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
            if (status == "error")
            {
                var message = envelope.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown server error.";
                throw new InvalidOperationException(message);
            }

            if (!envelope.TryGetProperty("data", out var dataProp))
            {
                throw new InvalidOperationException($"\"{toolName}\" response missing \"data\".");
            }

            var data = dataProp.Deserialize<T>(JsonOptions);
            if (data == null)
            {
                throw new InvalidOperationException($"\"{toolName}\" returned null data.");
            }
            return data;
        }
    }
}
