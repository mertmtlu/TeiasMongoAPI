using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.User
{
    public class UserClientAssignmentDto
    {
        [Required]
        public required string UserId { get; set; }

        [Required]
        public required List<string> ClientIds { get; set; }
    }
}