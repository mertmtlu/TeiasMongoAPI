using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Services.DTOs.Request.Common;

namespace TeiasMongoAPI.Services.DTOs.Request.Hazard
{
    public class AvalancheHazardDto
    {
        [Range(0, 1)]
        public double Score { get; set; }

        public Level Level { get; set; }

        public Dictionary<string, int>? EliminationCosts { get; set; }

        [Required]
        public bool PreviousIncidentOccurred { get; set; }

        public string? PreviousIncidentDescription { get; set; }

        [Required]
        public double DistanceToInventory { get; set; }

        public string? Incident { get; set; }
        public string? IncidentDescription { get; set; }
        public double SnowDepth { get; set; }
        public LocationDto? FirstHillLocation { get; set; }
        public double ElevationDifference { get; set; }
    }
}