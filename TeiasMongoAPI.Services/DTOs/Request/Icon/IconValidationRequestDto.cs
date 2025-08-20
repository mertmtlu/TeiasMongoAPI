using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Common;

namespace TeiasMongoAPI.Services.DTOs.Request.Icon
{
    public class IconValidationRequestDto
    {
        [Required]
        public required IconEntityType EntityType { get; set; }

        [Required]
        public required string EntityId { get; set; }

        public string? ExcludeIconId { get; set; }
    }
}