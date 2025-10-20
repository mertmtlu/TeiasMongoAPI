using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.KeyModels
{
    public class User : AEntityBase
    {
        [BsonElement("email")]
        public required string Email { get; set; }

        [BsonElement("username")]
        public required string Username { get; set; }

        [BsonElement("passwordHash")]
        public required string PasswordHash { get; set; }

        [BsonElement("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [BsonElement("lastName")]
        public string LastName { get; set; } = string.Empty;

        [BsonElement("role")]
        public string Role { get; set; } = string.Empty;

        [BsonElement("permissions")]
        public List<string> Permissions { get; set; } = new();

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = false;

        [BsonElement("passwordResetToken")]
        public string? PasswordResetToken { get; set; }

        [BsonElement("passwordResetTokenExpiry")]
        public DateTime? PasswordResetTokenExpiry { get; set; }

        [BsonElement("lastLoginDate")]
        public DateTime? LastLoginDate { get; set; }

        [BsonElement("createdDate")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [BsonElement("modifiedDate")]
        public DateTime? ModifiedDate { get; set; }

        [BsonElement("refreshTokens")]
        public List<RefreshToken> RefreshTokens { get; set; } = new();

        [BsonElement("assignedClients")]
        public List<ObjectId> AssignedClients { get; set; } = new();

        [BsonElement("groups")]
        public List<ObjectId> Groups { get; set; } = new();

        [BsonElement("tokenVersion")]
        public int TokenVersion { get; set; } = 1;

        // Legacy fields for backward compatibility - to be removed after data migration
        [BsonElement("groupIds")]
        [BsonIgnoreIfNull]
        public List<ObjectId>? GroupIds { get; set; }

        [BsonElement("roles")]
        [BsonIgnoreIfNull]
        public List<string>? Roles { get; set; }

        [BsonIgnore]
        public string FullName => $"{FirstName} {LastName}".Trim();

        /// <summary>
        /// Migrates old roles data to new single role format and updates permissions
        /// </summary>
        public void MigrateFromLegacyRoles()
        {
            if (string.IsNullOrEmpty(Role) && Roles != null && Roles.Count > 0)
            {
                // Take the first role as the primary role
                Role = Roles[0];
                // Clear the old roles field after migration
                Roles = null;
                // Update permissions based on the new role
                Permissions = RolePermissions.GetUserPermissions(this);
            }
        }
    }
}