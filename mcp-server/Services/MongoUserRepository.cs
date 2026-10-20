using MongoDB.Driver;

namespace PromptVcs.McpServer.Services;

public class MongoUserRepository : IUserRepository
{
    private readonly IMongoCollection<UserDocument>? _users;

    public bool IsConfigured => _users != null;

    public MongoUserRepository(MongoDatabaseProvider provider)
    {
        _users = provider.Database?.GetCollection<UserDocument>("users");
    }

    public Task<bool> AnyAsync() =>
        Require().Find(FilterDefinition<UserDocument>.Empty).AnyAsync();

    public Task<UserDocument?> FindByUsernameAsync(string username) =>
        Require().Find(u => u.Username == username).FirstOrDefaultAsync()!;

    public Task<List<UserDocument>> ListAllAsync() =>
        Require().Find(FilterDefinition<UserDocument>.Empty).SortBy(u => u.Username).ToListAsync();

    public Task<long> CountAdminsAsync() =>
        Require().CountDocumentsAsync(u => u.IsAdmin);

    public Task InsertAsync(UserDocument user) =>
        Require().InsertOneAsync(user);

    public Task SetPasswordHashAsync(string username, string passwordHash) =>
        Require().UpdateOneAsync(u => u.Username == username, Builders<UserDocument>.Update.Set(u => u.PasswordHash, passwordHash));

    public Task SetUsernameAsync(string oldUsername, string newUsername) =>
        Require().UpdateOneAsync(u => u.Username == oldUsername, Builders<UserDocument>.Update.Set(u => u.Username, newUsername));

    public Task SetIsAdminAsync(string username, bool isAdmin) =>
        Require().UpdateOneAsync(u => u.Username == username, Builders<UserDocument>.Update.Set(u => u.IsAdmin, isAdmin));

    public Task DeleteAsync(string username) =>
        Require().DeleteOneAsync(u => u.Username == username);

    private IMongoCollection<UserDocument> Require() =>
        _users ?? throw new InvalidOperationException("PROMPTVCS_MONGO_URI is not configured on the server.");
}
