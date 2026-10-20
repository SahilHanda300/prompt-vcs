using MongoDB.Driver;

namespace PromptVcs.McpServer.Services;

public class MongoSessionRepository : ISessionRepository
{
    private readonly IMongoCollection<SessionDocument>? _sessions;

    public MongoSessionRepository(MongoDatabaseProvider provider)
    {
        _sessions = provider.Database?.GetCollection<SessionDocument>("sessions");
    }

    public Task<SessionDocument?> FindByTokenAsync(string token) =>
        Require().Find(s => s.Token == token).FirstOrDefaultAsync()!;

    public Task InsertAsync(SessionDocument session) =>
        Require().InsertOneAsync(session);

    public Task DeleteByTokenAsync(string token) =>
        Require().DeleteOneAsync(s => s.Token == token);

    public Task UpdateUsernameForTokenAsync(string token, string newUsername) =>
        Require().UpdateOneAsync(s => s.Token == token, Builders<SessionDocument>.Update.Set(s => s.Username, newUsername));

    public Task UpdateUsernameForAllAsync(string oldUsername, string newUsername) =>
        Require().UpdateManyAsync(s => s.Username == oldUsername, Builders<SessionDocument>.Update.Set(s => s.Username, newUsername));

    public Task UpdateIsAdminForAllAsync(string username, bool isAdmin) =>
        Require().UpdateManyAsync(s => s.Username == username, Builders<SessionDocument>.Update.Set(s => s.IsAdmin, isAdmin));

    public Task DeleteAllForUsernameAsync(string username) =>
        Require().DeleteManyAsync(s => s.Username == username);

    private IMongoCollection<SessionDocument> Require() =>
        _sessions ?? throw new InvalidOperationException("PROMPTVCS_MONGO_URI is not configured on the server.");
}
