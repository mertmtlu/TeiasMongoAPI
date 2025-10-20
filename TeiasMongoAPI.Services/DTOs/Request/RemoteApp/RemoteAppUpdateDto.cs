using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.RemoteApp
{
    public class RemoteAppUpdateDto
    {
        [StringLength(200, MinimumLength = 1)]
        public string? Name { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [Url]
        [StringLength(2000)]
        public string? Url { get; set; }

        public bool? IsPublic { get; set; }

        [StringLength(100)]
        public string? DefaultUsername { get; set; }

        [StringLength(100)]
        public string? DefaultPassword { get; set; }

        [Url]
        [StringLength(2000)]
        public string? SsoUrl { get; set; }
    }
}