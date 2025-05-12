using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Services.DTOs.Request.Building
{
    public class BuildingCreateDto
    {
        [Required]
        public required string TmId { get; set; }

        [Required]
        public int BuildingTMID { get; set; }

        [MaxLength(200)]
        public string? Name { get; set; }

        [Required]
        public BuildingType Type { get; set; }

        public bool InScopeOfMETU { get; set; }

        [MaxLength(500)]
        public string? ReportName { get; set; }
    }
}