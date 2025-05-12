using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Common;
using TeiasMongoAPI.Services.DTOs.Request.Hazard;

namespace TeiasMongoAPI.Services.DTOs.Request.TM
{
    public class TMCreateDto
    {
        [Required]
        public required string RegionId { get; set; }

        [Required]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public required string Name { get; set; }

        public TMType Type { get; set; } = TMType.Default;

        public TMState State { get; set; } = TMState.Active;

        [Required]
        [MinLength(1)]
        public required List<int> Voltages { get; set; }

        public DateOnly? ProvisionalAcceptanceDate { get; set; }

        [Required]
        public required LocationDto Location { get; set; }

        public AddressDto? Address { get; set; }

        [Required]
        public required EarthquakeLevelDto DD1 { get; set; }

        [Required]
        public required EarthquakeLevelDto DD2 { get; set; }

        [Required]
        public required EarthquakeLevelDto DD3 { get; set; }

        public EarthquakeLevelDto? EarthquakeScenario { get; set; }

        [Required]
        public required PollutionDto Pollution { get; set; }

        [Required]
        public required FireHazardDto FireHazard { get; set; }

        [Required]
        public required SecurityHazardDto SecurityHazard { get; set; }

        [Required]
        public required NoiseHazardDto NoiseHazard { get; set; }

        [Required]
        public required AvalancheHazardDto AvalancheHazard { get; set; }

        [Required]
        public required LandslideHazardDto LandslideHazard { get; set; }

        [Required]
        public required RockFallHazardDto RockFallHazard { get; set; }

        [Required]
        public required FloodHazardDto FloodHazard { get; set; }

        [Required]
        public required TsunamiHazardDto TsunamiHazard { get; set; }

        [Required]
        public required SoilDto Soil { get; set; }
    }
}