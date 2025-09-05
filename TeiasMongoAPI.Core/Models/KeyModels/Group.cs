using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.KeyModels
{
    public class Group : AEntityBase
    {
        [BsonElement("name")]
        public required string Name { get; set; }

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("members")]
        public List<ObjectId> Members { get; set; } = new();

        [BsonElement("createdBy")]
        public required string CreatedBy { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("modifiedAt")]
        public DateTime? ModifiedAt { get; set; }

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("metadata")]
        public object Metadata { get; set; } = new object();

        public int MemberCount => Members.Count;
    }
}