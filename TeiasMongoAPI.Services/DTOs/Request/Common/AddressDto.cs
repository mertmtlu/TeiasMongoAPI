using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Common
{
    public class AddressDto
    {
        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? County { get; set; }

        [MaxLength(100)]
        public string? District { get; set; }

        [MaxLength(200)]
        public string? Street { get; set; }
    }
}