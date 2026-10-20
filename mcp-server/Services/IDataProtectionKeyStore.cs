namespace PromptVcs.McpServer.Services;

/// <summary>
/// Storage for PromptVcs.Web's ASP.NET Core Data Protection keyring (the
/// material behind both antiforgery tokens and the auth cookie's
/// encryption). Backed by Mongo, not Web's container disk, because Render's
/// free tier gives Web no persistent disk — without this, every restart
/// (including a plain idle spin-down/wake, which happens on its own) throws
/// the previous keys away, silently invalidating every open form (bare 400
/// on submit) and signed-in session (forced re-login) that was issued
/// before the restart.
/// </summary>
public interface IDataProtectionKeyStore
{
    Task<IReadOnlyList<string>> GetAllAsync();
    Task StoreAsync(string id, string xml);
}
