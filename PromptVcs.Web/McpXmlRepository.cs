using System.Net.Http.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace PromptVcs.Web;

/// <summary>
/// Persists the Data Protection keyring (antiforgery tokens, auth cookie
/// encryption) to the server's Mongo-backed store instead of Web's own
/// container disk — see mcp-server's IDataProtectionKeyStore for why.
/// IXmlRepository's methods are synchronous by contract (the framework
/// calls them from non-async code paths), hence the blocking .Result/
/// GetAwaiter().GetResult() calls below.
/// </summary>
public class McpXmlRepository : IXmlRepository
{
    private readonly HttpClient _http;

    public McpXmlRepository(HttpClient http)
    {
        _http = http;
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        try
        {
            var xmls = _http.GetFromJsonAsync<List<string>>("internal/dataprotection-keys").GetAwaiter().GetResult();
            return xmls?.Select(XElement.Parse).ToList() ?? new List<XElement>();
        }
        catch
        {
            // Server unreachable or secret not configured yet — fall back to
            // an empty ring rather than crashing startup; the framework will
            // just generate (and try to store) a fresh key.
            return Array.Empty<XElement>();
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        try
        {
            var id = element.Attribute("id")?.Value ?? friendlyName;
            _http.PostAsJsonAsync("internal/dataprotection-keys", new { Id = id, Xml = element.ToString(SaveOptions.DisableFormatting) })
                .GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort — losing persistence here degrades back to the
            // ephemeral-keyring symptom this class exists to avoid, not a
            // hard failure of the request that triggered key generation.
        }
    }
}
