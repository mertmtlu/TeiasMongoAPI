using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Common;

namespace TeiasMongoAPI.Services.DTOs.Request.Hazard
{
    public class FireHazardDto
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

        public bool FireSystem { get; set; }
        public bool AfforestationCondition { get; set; }
        public string? ForestType { get; set; }
        public bool StubbleBurning { get; set; }
        public bool ExternalFireIncident { get; set; }
        public string? ExternalFireIncidentDescription { get; set; }
        public bool NearbyGasStation { get; set; }
        public double DistanceToNearbyGasStation { get; set; }
        public bool HasIndustrialFireDanger { get; set; }
        public int IndustrialFireExposedFacade { get; set; }
        public bool ForestFireDanger { get; set; }
        public double DistanceToClosestForest { get; set; }
        public string? VegetationType { get; set; }
    }
}
