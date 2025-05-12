using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Region
{
    public class RegionCityUpdateDto
    {
        public enum Operation
        {
            Add,
            Remove
        }

        [Required]
        public Operation Action { get; set; }

        [Required]
        [MinLength(1)]
        public required List<string> Cities { get; set; }
    }
}