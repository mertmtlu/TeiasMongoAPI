using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
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

        [BsonElement("roles")]
        public List<string> Roles { get; set; } = new();

        [BsonElement("permissions")]
        public List<string> Permissions { get; set; } = new();

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("isEmailVerified")]
        public bool IsEmailVerified { get; set; } = false;

        [BsonElement("emailVerificationToken")]
        public string? EmailVerificationToken { get; set; }

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

        // Navigation properties for user relationships
        [BsonElement("assignedRegions")]
        public List<ObjectId> AssignedRegions { get; set; } = new();

        [BsonElement("assignedTMs")]
        public List<ObjectId> AssignedTMs { get; set; } = new();

        [BsonIgnore]
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}