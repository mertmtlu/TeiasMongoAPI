using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Auth
{
    public class UserPasswordResetRequestDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }
}