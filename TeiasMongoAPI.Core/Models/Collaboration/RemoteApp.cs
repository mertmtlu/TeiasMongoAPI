using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public class RemoteApp : AEntityBase
    {
        [BsonElement("name")]
        public required string Name { get; set; }

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("url")]
        public required string Url { get; set; }

        [BsonElement("isPublic")]
        public bool IsPublic { get; set; } = false;

        [BsonElement("assignedUsers")]
        public List<ObjectId> AssignedUsers { get; set; } = new();

        [BsonElement("creator")]
        public required string Creator { get; set; }

        [BsonElement("status")]
        public string Status { get; set; } = "active";

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("modifiedAt")]
        public DateTime? ModifiedAt { get; set; }

        [BsonElement("metadata")]
        public object Metadata { get; set; } = new object();

        [BsonElement("defaultUsername")]
        public string? DefaultUsername { get; set; }

        [BsonElement("defaultPassword")]
        public string? DefaultPassword { get; set; }

        [BsonElement("ssoUrl")]
        public string? SsoUrl { get; set; }
    }
}