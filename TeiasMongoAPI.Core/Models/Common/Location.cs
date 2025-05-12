using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Core.Models.Common
{
    public class Location
    {
        [Required]
        [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90 degrees")]
        public required double Latitude { get; set; }

        [Required]
        [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180 degrees")]
        public required double Longitude { get; set; }
    }
}
