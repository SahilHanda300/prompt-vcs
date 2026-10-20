using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace PromptVcs.McpServer.Services;

public class MongoDataProtectionKeyStore : IDataProtectionKeyStore
{
    private readonly IMongoCollection<KeyDocument>? _collection;

    public MongoDataProtectionKeyStore(MongoDatabaseProvider provider)
    {
        _collection = provider.Database?.GetCollection<KeyDocument>("dataprotection_keys");
    }

    public async Task<IReadOnlyList<string>> GetAllAsync()
    {
        if (_collection == null) return Array.Empty<string>();
        var docs = await _collection.Find(FilterDefinition<KeyDocument>.Empty).ToListAsync();
        return docs.Select(d => d.Xml).ToList();
    }

    public async Task StoreAsync(string id, string xml)
    {
        if (_collection == null) return;
        var doc = new KeyDocument { Id = id, Xml = xml };
        await _collection.ReplaceOneAsync(d => d.Id == id, doc, new ReplaceOptions { IsUpsert = true });
    }

    private class KeyDocument
    {
        [BsonId]
        public string Id { get; set; } = "";
        public string Xml { get; set; } = "";
    }
}
