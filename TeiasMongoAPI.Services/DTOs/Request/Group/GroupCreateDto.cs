using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Group
{
    public class GroupCreateDto
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public required string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public List<string> MemberIds { get; set; } = new();

        public object Metadata { get; set; } = new object();
    }
}