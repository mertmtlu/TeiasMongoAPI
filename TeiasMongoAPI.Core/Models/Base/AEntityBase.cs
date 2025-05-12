using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Core.Models.Block;

namespace TeiasMongoAPI.Core.Models.Base
{
    [BsonDiscriminator(RootClass = true)]
    [BsonKnownTypes(
    typeof(Client),
    typeof(Region),
    typeof(TM),
    typeof(Building),
    typeof(Concrete),
    typeof(Masonry))]
    public abstract class AEntityBase
    {
        [BsonId]
        public ObjectId _ID { get; set; }
    }
}
