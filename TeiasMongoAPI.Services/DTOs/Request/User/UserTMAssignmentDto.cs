using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.User
{
    public class UserTMAssignmentDto
    {
        [Required]
        public required string UserId { get; set; }

        [Required]
        public required List<string> TMIds { get; set; }
    }
}