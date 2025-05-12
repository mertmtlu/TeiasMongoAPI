using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Building
{
    public class BuildingBlockRemoveDto
    {
        [Required]
        public required string BlockId { get; set; }
    }
}