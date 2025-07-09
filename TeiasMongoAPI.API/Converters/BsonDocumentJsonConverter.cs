using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace TeiasMongoAPI.API.Converters
{
    public class BsonDocumentJsonConverter : JsonConverter<BsonDocument>
    {
        public override BsonDocument? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return new BsonDocument();
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var jsonDocument = JsonDocument.ParseValue(ref reader);
                var json = jsonDocument.RootElement.GetRawText();
                
                try
                {
                    return BsonSerializer.Deserialize<BsonDocument>(json);
                }
                catch
                {
                    return new BsonDocument();
                }
            }

            return new BsonDocument();
        }

        public override void Write(Utf8JsonWriter writer, BsonDocument value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            var json = value.ToJson();
            using var jsonDoc = JsonDocument.Parse(json);
            jsonDoc.WriteTo(writer);
        }
    }
}