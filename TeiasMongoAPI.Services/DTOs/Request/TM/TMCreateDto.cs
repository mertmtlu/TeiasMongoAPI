using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Common;
using TeiasMongoAPI.Services.DTOs.Request.Hazard;

namespace TeiasMongoAPI.Services.DTOs.Request.TM
{
    public class TMCreateDto
    {
        [Required]
        public required string RegionId { get; set; }

        [Required]
        public int TmId { get; set; }

        [Required]
        [MaxLength(200)]
        public required string Name { get; set; }

        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public TMType Type { get; set; } = TMType.Default;

        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public TMState State { get; set; } = TMState.Active;

        [Required]
        [MinLength(1)]
        public required List<int> Voltages { get; set; }

        public DateOnly? ProvisionalAcceptanceDate { get; set; }

        public required LocationRequestDto Location { get; set; }

        public AddressDto? Address { get; set; }
    }
}