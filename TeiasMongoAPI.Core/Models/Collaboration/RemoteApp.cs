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

        [BsonElement("permissions")]
        public RemoteAppPermissions Permissions { get; set; } = new RemoteAppPermissions();

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

    public class RemoteAppPermissions
    {
        public List<RemoteAppGroupPermission> Groups { get; set; } = new List<RemoteAppGroupPermission>();
        public List<RemoteAppUserPermission> Users { get; set; } = new List<RemoteAppUserPermission>();
    }

    public class RemoteAppGroupPermission
    {
        public string GroupId { get; set; } = string.Empty;
        public string AccessLevel { get; set; } = string.Empty;
    }

    public class RemoteAppUserPermission
    {
        public string UserId { get; set; } = string.Empty;
        public string AccessLevel { get; set; } = string.Empty;
    }
}