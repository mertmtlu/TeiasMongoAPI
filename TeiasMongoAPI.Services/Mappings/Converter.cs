using AutoMapper;
using MongoDB.Bson;
using System.Collections.Generic;

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
}