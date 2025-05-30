using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Models.Base
{
    [BsonDiscriminator(RootClass = true)]
    //[BsonKnownTypes(
    //    typeof(Client),
    //    typeof(Region),
    //    typeof(TM),
    //    typeof(Building),
    //    typeof(User),
    //    typeof(AlternativeTM))]
    public abstract class AEntityBase
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId _ID { get; set; }
    }
}