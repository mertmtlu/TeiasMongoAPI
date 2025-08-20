using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.Common
{
    public class Icon : AEntityBase
    {
        [BsonElement("name")]
        public required string Name { get; set; }

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("iconData")]
        public required string IconData { get; set; }

        [BsonElement("format")]
        public required string Format { get; set; }

        [BsonElement("size")]
        public int Size { get; set; }

        [BsonElement("entityType")]
        public required IconEntityType EntityType { get; set; }

        [BsonElement("entityId")]
        public required ObjectId EntityId { get; set; }

        [BsonElement("creator")]
        public required string Creator { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("modifiedAt")]
        public DateTime? ModifiedAt { get; set; }

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("metadata")]
        public BsonDocument Metadata { get; set; } = new();
    }

    public enum IconEntityType
    {
        Program,
        Workflow,
        RemoteApp
    }
}