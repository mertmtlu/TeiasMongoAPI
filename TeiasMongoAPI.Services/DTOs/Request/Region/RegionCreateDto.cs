using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Region
{
    public class RegionCreateDto
    {
        [Required]
        public required string ClientId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }

        [Required]
        [MinLength(1)]
        public required List<string> Cities { get; set; }

        [Required]
        [MaxLength(100)]
        public required string Headquarters { get; set; }
    }
}