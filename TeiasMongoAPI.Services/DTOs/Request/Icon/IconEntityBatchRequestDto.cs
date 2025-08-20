using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Common;

namespace TeiasMongoAPI.Services.DTOs.Request.Icon
{
    public class IconEntityBatchRequestDto
    {
        [Required]
        public required IconEntityType EntityType { get; set; }

        [Required]
        public required IEnumerable<string> EntityIds { get; set; }
    }
}