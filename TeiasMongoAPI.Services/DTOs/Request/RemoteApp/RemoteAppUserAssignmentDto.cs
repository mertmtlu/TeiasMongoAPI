using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.RemoteApp
{
    public class RemoteAppUserAssignmentDto
    {
        [Required]
        public required string UserId { get; set; }
    }
}