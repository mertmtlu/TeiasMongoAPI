using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Common;
using TeiasMongoAPI.Services.DTOs.Request.Hazard;

namespace TeiasMongoAPI.Services.DTOs.Request.TM
{
    public class TMUpdateDto
    {
        public string? RegionId { get; set; }
        public int? Id { get; set; }
        public string? Name { get; set; }
        public TMType? Type { get; set; }
        public TMState? State { get; set; }
        public List<int>? Voltages { get; set; }
        public DateOnly? ProvisionalAcceptanceDate { get; set; }
        public LocationDto? Location { get; set; }
        public AddressDto? Address { get; set; }
        public EarthquakeLevelDto? DD1 { get; set; }
        public EarthquakeLevelDto? DD2 { get; set; }
        public EarthquakeLevelDto? DD3 { get; set; }
        public EarthquakeLevelDto? EarthquakeScenario { get; set; }
        public PollutionDto? Pollution { get; set; }
        public FireHazardDto? FireHazard { get; set; }
        public SecurityHazardDto? SecurityHazard { get; set; }
        public NoiseHazardDto? NoiseHazard { get; set; }
        public AvalancheHazardDto? AvalancheHazard { get; set; }
        public LandslideHazardDto? LandslideHazard { get; set; }
        public RockFallHazardDto? RockFallHazard { get; set; }
        public FloodHazardDto? FloodHazard { get; set; }
        public TsunamiHazardDto? TsunamiHazard { get; set; }
        public SoilDto? Soil { get; set; }
    }
}