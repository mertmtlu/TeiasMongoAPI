using AutoMapper;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.Mappings
{
    public class BaseMappingProfile : Profile
    {
        public BaseMappingProfile()
        {
            // For converting request DTOs with dictionaries into BsonDocuments for the database
            CreateMap<Dictionary<string, object>, BsonDocument>().ConvertUsing<DictionaryToBsonDocumentConverter>();

            // For converting BsonDocuments from the database into dictionaries for response DTOs
            CreateMap<BsonDocument, Dictionary<string, object>>().ConvertUsing<BsonDocumentToDictionaryConverter>();
        }
    }
}
