using MongoDB.Bson.Serialization.Attributes;

namespace TeiasMongoAPI.Core.Models.KeyModels
{
    public class RefreshToken
    {
        [BsonElement("token")]
        public required string Token { get; set; }

        [BsonElement("expires")]
        public DateTime Expires { get; set; }

        [BsonElement("created")]
        public DateTime Created { get; set; } = DateTime.UtcNow;

        [BsonElement("createdByIp")]
        public string? CreatedByIp { get; set; }

        [BsonElement("revoked")]
        public DateTime? Revoked { get; set; }

        [BsonElement("revokedByIp")]
        public string? RevokedByIp { get; set; }

        [BsonElement("replacedByToken")]
        public string? ReplacedByToken { get; set; }

        [BsonIgnore]
        public bool IsActive => Revoked == null && !IsExpired;

        [BsonIgnore]
        public bool IsExpired => DateTime.UtcNow >= Expires;
    }
}