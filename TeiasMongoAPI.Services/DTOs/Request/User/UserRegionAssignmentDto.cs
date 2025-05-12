using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.User
{
    public class UserRegionAssignmentDto
    {
        [Required]
        public required string UserId { get; set; }

        [Required]
        public required List<string> RegionIds { get; set; }
    }
}