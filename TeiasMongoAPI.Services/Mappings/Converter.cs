using AutoMapper;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Text.Json;

namespace TeiasMongoAPI.Services.Mappings
{
    public class DictionaryToBsonDocumentConverter : ITypeConverter<Dictionary<string, object>, BsonDocument>
    {
        public BsonDocument Convert(Dictionary<string, object> source, BsonDocument destination, ResolutionContext context)
        {
            return source == null ? null : new BsonDocument(source);
        }
    }

    public class BsonDocumentToDictionaryConverter : ITypeConverter<BsonDocument, Dictionary<string, object>>
    {
        public Dictionary<string, object> Convert(BsonDocument source, Dictionary<string, object> destination, ResolutionContext context)
        {
            if (source == null)
            {
                return null;
            }

            // The MongoDB driver provides a convenient method to handle this conversion,
            // including correctly handling nested types.
            return source.ToDictionary();
        }
    }

    public class JsonElementToDictionaryConverter : ITypeConverter<JsonElement, Dictionary<string, object>>
    {
        public Dictionary<string, object> Convert(JsonElement source, Dictionary<string, object> destination, ResolutionContext context)
        {
            return ConvertElement(source) as Dictionary<string, object>;
        }

        private object ConvertElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    return element.EnumerateObject()
                        .ToDictionary(prop => prop.Name, prop => ConvertElement(prop.Value));

                case JsonValueKind.Array:
                    return element.EnumerateArray().Select(e => ConvertElement(e)).ToList();

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long l)) return l;
                    return element.GetDouble();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;

                default:
                    throw new NotSupportedException($"JsonValueKind {element.ValueKind} is not supported.");
            }
        }
    }

    public class JsonElementToBsonDocumentConverter : ITypeConverter<JsonElement, BsonDocument>
    {
        public BsonDocument Convert(JsonElement source, BsonDocument destination, ResolutionContext context)
        {
            return ConvertJsonElementToBsonValue(source).AsBsonDocument;
        }

        private BsonValue ConvertJsonElementToBsonValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var doc = new BsonDocument();
                    foreach (var prop in element.EnumerateObject())
                    {
                        doc.Add(prop.Name, ConvertJsonElementToBsonValue(prop.Value));
                    }
                    return doc;

                case JsonValueKind.Array:
                    return new BsonArray(element.EnumerateArray().Select(ConvertJsonElementToBsonValue));

                case JsonValueKind.String:
                    // Check for GUID and DateTime formats if needed
                    if (element.TryGetGuid(out var guid)) return new BsonBinaryData(guid, GuidRepresentation.Standard);
                    if (element.TryGetDateTime(out var dateTime)) return new BsonDateTime(dateTime);
                    return new BsonString(element.GetString());

                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long l)) return new BsonInt64(l);
                    return new BsonDouble(element.GetDouble());

                case JsonValueKind.True:
                    return BsonBoolean.True;

                case JsonValueKind.False:
                    return BsonBoolean.False;

                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return BsonNull.Value;

                default:
                    throw new NotSupportedException($"JsonValueKind {element.ValueKind} is not supported.");
            }
        }
    }
}