using MongoDB.Bson;
using System.Text.Json;

public static class JsonElementExtensions
{
    public static BsonDocument ToBsonDocument(this JsonElement element)
    {
        var json = element.GetRawText(); // get raw JSON string
        return BsonSerializer.Deserialize<BsonDocument>(json); // convert to BsonDocument
    }
}
