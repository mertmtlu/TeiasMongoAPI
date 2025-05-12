using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Common
{
    public class StoreyHeightDto
    {
        [Required]
        public int StoreyNumber { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public double Height { get; set; }
    }
}