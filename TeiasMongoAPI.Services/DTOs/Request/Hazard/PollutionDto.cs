using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Services.DTOs.Request.Common;

namespace TeiasMongoAPI.Services.DTOs.Request.Hazard
{
    public class PollutionDto
    {
        [Required]
        public required LocationDto PollutantLocation { get; set; }

        [Required]
        public int PollutantNo { get; set; }

        public string? PollutantSource { get; set; }

        public double PollutantDistance { get; set; }

        public Level PollutantLevel { get; set; }
    }
}