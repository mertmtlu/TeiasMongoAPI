using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Building
{
    public class BuildingBlockAddDto
    {
        [Required]
        public required string BlockId { get; set; }
    }
}