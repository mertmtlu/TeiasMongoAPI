using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.RemoteApp
{
    public class RemoteAppCreateDto
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public required string Name { get; set; }

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Url]
        [StringLength(2000)]
        public required string Url { get; set; }

        public bool IsPublic { get; set; } = false;

        [StringLength(100)]
        public string? DefaultUsername { get; set; }

        [StringLength(100)]
        public string? DefaultPassword { get; set; }

        [Url]
        [StringLength(2000)]
        public string? SsoUrl { get; set; }
    }
}