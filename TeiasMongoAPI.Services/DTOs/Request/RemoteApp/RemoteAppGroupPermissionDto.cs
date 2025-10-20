using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.RemoteApp
{
    public class RemoteAppGroupPermissionDto
    {
        [Required]
        public required string GroupId { get; set; }

        [Required]
        [MaxLength(20)]
        public required string AccessLevel { get; set; } // "read", "write", "admin"
    }
}
