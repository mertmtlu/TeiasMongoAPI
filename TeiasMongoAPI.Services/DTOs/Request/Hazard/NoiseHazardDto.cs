using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Common;

namespace TeiasMongoAPI.Services.DTOs.Request.Hazard
{
    public class NoiseHazardDto
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

        public Dictionary<BuildingType, double>? NoiseMeasurementsForBuildings { get; set; }
        public Dictionary<LocationDto, double>? NoiseMeasurementsForCoordinates { get; set; }
        public bool ResidentialArea { get; set; }
        public bool Exists { get; set; }
        public bool ExtremeNoise { get; set; }
        public string? ExtremeNoiseDescription { get; set; }
    }
}