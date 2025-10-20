using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.RemoteApp
{
    public class RemoteAppUserPermissionDto
    {
        [Required]
        public required string UserId { get; set; }

        [Required]
        [MaxLength(20)]
        public required string AccessLevel { get; set; } // "read", "write", "admin"
    }
}
