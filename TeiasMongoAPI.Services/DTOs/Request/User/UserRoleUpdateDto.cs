using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.User
{
    public class UserRoleUpdateDto
    {
        //[Required]
        //public required string UserId { get; set; }

        [Required]
        public required string Role { get; set; }
    }
}