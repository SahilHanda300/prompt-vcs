using MongoDB.Driver;

namespace PromptVcs.McpServer.Services;

/// <summary>
/// One MongoDB connection, built once from PROMPTVCS_MONGO_URI, shared by
/// MongoAuthService (users/sessions) and ServerStore (the prompt store
/// itself) — avoids each standing up its own MongoClient against the same
/// cluster.
/// </summary>
public class MongoDatabaseProvider
{
    public IMongoDatabase? Database { get; }
    public bool IsConfigured => Database != null;

    public MongoDatabaseProvider()
    {
        var uri = Environment.GetEnvironmentVariable("PROMPTVCS_MONGO_URI");
        if (string.IsNullOrEmpty(uri)) return;

        var mongoUrl = MongoUrl.Create(uri);
        var databaseName = string.IsNullOrEmpty(mongoUrl.DatabaseName) ? "promptvcs" : mongoUrl.DatabaseName;
        Database = new MongoClient(mongoUrl).GetDatabase(databaseName);
    }
}
