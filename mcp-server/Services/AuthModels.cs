using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PromptVcs.McpServer.Services;

public class UserDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsAdmin { get; set; }
}

public class SessionDocument
{
    // The session token itself is the document's _id — looking up a session
    // is then a single indexed point lookup, and logout is a single delete.
    [BsonId]
    public string Token { get; set; } = "";

    public string Username { get; set; } = "";
    public bool IsAdmin { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
