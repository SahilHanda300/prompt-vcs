using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace PromptVcs.Cli;

/// <summary>
/// The CLI's only connection to the outside world: a real MCP client calling
/// the server's login/register/logout and create/edit/list/show/diff/reset
/// tools. The CLI holds no local store (besides the cached session token,
/// see SessionCache) and never touches Claude Code itself — every command is
/// one round trip here. Every tool response is a {"status":"ok"|"error",...}
/// envelope, so failures (both "server unreachable" and "server said no")
/// surface uniformly as exceptions the caller can catch once.
/// </summary>
public class ServerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string McpUrl() => Environment.GetEnvironmentVariable("PROMPTVCS_MCP_URL") ?? "http://localhost:5279/mcp";

    public async Task<AuthResultDto> LoginAsync(string username, string password) =>
        await CallAsync<AuthResultDto>("login", new Dictionary<string, object?> { ["username"] = username, ["password"] = password }, requireSession: false);

    public async Task<AuthResultDto> RegisterAsync(string username, string password) =>
        await CallAsync<AuthResultDto>("register", new Dictionary<string, object?> { ["username"] = username, ["password"] = password }, requireSession: false);

    public async Task<LogoutResultDto> LogoutAsync(string sessionToken) =>
        await CallAsync<LogoutResultDto>("logout", new Dictionary<string, object?> { ["sessionToken"] = sessionToken }, requireSession: false);

    public async Task<ResetResultDto> ResetAsync() =>
        await CallAsync<ResetResultDto>("reset", new Dictionary<string, object?>());

    public async Task<PipelineResultDto> CreateAsync(string name, string content) =>
        await CallAsync<PipelineResultDto>("create", new Dictionary<string, object?> { ["name"] = name, ["content"] = content });

    public async Task<PipelineResultDto> EditAsync(string name, string content) =>
        await CallAsync<PipelineResultDto>("edit", new Dictionary<string, object?> { ["name"] = name, ["content"] = content });

    public async Task<List<PromptListItemDto>> ListAsync() =>
        await CallAsync<List<PromptListItemDto>>("list", new Dictionary<string, object?>());

    public async Task<ShowResultDto> ShowAsync(string name, int? version) =>
        await CallAsync<ShowResultDto>("show", new Dictionary<string, object?> { ["name"] = name, ["version"] = version });

    public async Task<List<DiffLineDto>> DiffAsync(string name, int v1, int v2) =>
        await CallAsync<List<DiffLineDto>>("diff", new Dictionary<string, object?> { ["name"] = name, ["v1"] = v1, ["v2"] = v2 });

    // requireSession: false for login/register/logout, which have to work
    // before the caller has any session token to present.
    private static async Task<T> CallAsync<T>(string toolName, Dictionary<string, object?> arguments, bool requireSession = true)
    {
        if (requireSession)
        {
            var cached = SessionCache.Load();
            if (cached == null)
            {
                throw new InvalidOperationException("Not logged in. Run \"login\" first.");
            }
            arguments["sessionToken"] = cached.SessionToken;
        }

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
