using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Icon
{
    public class IconBatchRequestDto
    {
        [Required]
        public required IEnumerable<string> IconIds { get; set; }
    }
}