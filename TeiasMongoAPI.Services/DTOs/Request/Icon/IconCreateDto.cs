using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Common;
using MongoDB.Bson;

namespace TeiasMongoAPI.Services.DTOs.Request.Icon
{
    public class IconCreateDto
    {
        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public required string IconData { get; set; }

        [Required]
        [StringLength(10)]
        public required string Format { get; set; }

        [Required]
        public required IconEntityType EntityType { get; set; }

        [Required]
        public required string EntityId { get; set; }

        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}