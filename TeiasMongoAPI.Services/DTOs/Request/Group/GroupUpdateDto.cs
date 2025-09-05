using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Group
{
    public class GroupUpdateDto
    {
        [StringLength(100, MinimumLength = 2)]
        public string? Name { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public bool? IsActive { get; set; }

        public object? Metadata { get; set; }
    }
}