using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Region
{
    public class RegionUpdateDto
    {
        public string? ClientId { get; set; }

        [Range(1, int.MaxValue)]
        public int? Id { get; set; }

        public List<string>? Cities { get; set; }

        [MaxLength(100)]
        public string? Headquarters { get; set; }
    }
}