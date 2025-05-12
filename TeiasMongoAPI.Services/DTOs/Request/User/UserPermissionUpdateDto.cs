using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.User
{
    public class UserPermissionUpdateDto
    {
        [Required]
        public required string UserId { get; set; }

        [Required]
        public required List<string> Permissions { get; set; }
    }
}