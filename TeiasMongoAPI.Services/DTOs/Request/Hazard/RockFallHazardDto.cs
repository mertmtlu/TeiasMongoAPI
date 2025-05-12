using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Common;

namespace TeiasMongoAPI.Services.DTOs.Request.Hazard
{
    public class RockFallHazardDto
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
    }
}