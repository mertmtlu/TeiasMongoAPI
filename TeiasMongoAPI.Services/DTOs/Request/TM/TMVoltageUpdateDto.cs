using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.TM
{
    public class TMVoltageUpdateDto
    {
        [Required]
        [MinLength(1)]
        public required List<int> Voltages { get; set; }
    }
}