using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Icon
{
    public class IconUpdateDto
    {
        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public string? IconData { get; set; }

        [StringLength(10)]
        public string? Format { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }
    }
}