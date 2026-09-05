using MongoDB.Bson.Serialization.Attributes;

namespace PromptVcs.McpServer.Services;

/// <summary>
/// The whole prompt Store graph (Core's own System.Text.Json shape,
/// unchanged), stored as one field on one MongoDB document rather than
/// mapped field-by-field into BSON. See ServerStore for why.
/// </summary>
public class StoreDocument
{
    [BsonId]
    public string Id { get; set; } = "singleton";

    public string Json { get; set; } = "{}";
}
