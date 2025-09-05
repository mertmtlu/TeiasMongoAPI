using MongoDB.Bson.Serialization.Attributes;

namespace TeiasMongoAPI.Core.Models.Common
{
    public abstract class BasePermissions
    {
        [BsonElement("users")]
        public List<EntityUserPermission> Users { get; set; } = new();

        [BsonElement("groups")]
        public List<EntityGroupPermission> Groups { get; set; } = new();

        [BsonElement("roles")]
        public List<EntityRolePermission> Roles { get; set; } = new();

        [BsonElement("isPublic")]
        public bool IsPublic { get; set; } = false;
    }

    public class EntityUserPermission
    {
        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("accessLevel")]
        public EntityAccessLevel AccessLevel { get; set; } = EntityAccessLevel.Read;

        [BsonElement("permissions")]
        public List<EntityPermissionType> Permissions { get; set; } = new();

        [BsonElement("grantedAt")]
        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("grantedBy")]
        public string GrantedBy { get; set; } = string.Empty;
    }

    public class EntityGroupPermission
    {
        [BsonElement("groupId")]
        public string GroupId { get; set; } = string.Empty;

        [BsonElement("accessLevel")]
        public EntityAccessLevel AccessLevel { get; set; } = EntityAccessLevel.Read;

        [BsonElement("permissions")]
        public List<EntityPermissionType> Permissions { get; set; } = new();

        [BsonElement("grantedAt")]
        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("grantedBy")]
        public string GrantedBy { get; set; } = string.Empty;
    }

    public class EntityRolePermission
    {
        [BsonElement("roleName")]
        public string RoleName { get; set; } = string.Empty;

        [BsonElement("accessLevel")]
        public EntityAccessLevel AccessLevel { get; set; } = EntityAccessLevel.Read;

        [BsonElement("permissions")]
        public List<EntityPermissionType> Permissions { get; set; } = new();

        [BsonElement("grantedAt")]
        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("grantedBy")]
        public string GrantedBy { get; set; } = string.Empty;
    }

    public enum EntityAccessLevel
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 3,
        Admin = 4,
        Full = 5
    }

    public enum EntityPermissionType
    {
        View,
        Edit,
        Delete,
        Execute,
        Share,
        ManagePermissions,
        Clone,
        Export,
        Deploy,
        Debug
    }
}