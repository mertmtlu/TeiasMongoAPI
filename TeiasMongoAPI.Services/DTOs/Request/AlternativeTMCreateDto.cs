namespace TeiasMongoAPI.Services.DTOs.Request
{
    public class AlternativeTMCreateDto
    {
        public required string TmId { get; set; } // Will be converted to ObjectId
        public required LocationDto Location { get; set; }
        public string City { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public required EarthquakeLevelDto DD1 { get; set; }
        public required EarthquakeLevelDto DD2 { get; set; }
        public required EarthquakeLevelDto DD3 { get; set; }
        public EarthquakeLevelDto? EarthquakeScenario { get; set; }
        public required PollutionDto Pollution { get; set; }
        public required FireHazardDto FireHazard { get; set; }
        public required SecurityHazardDto SecurityHazard { get; set; }
        public required NoiseHazardDto NoiseHazard { get; set; }
        public required AvalancheHazardDto AvalancheHazard { get; set; }
        public required LandslideHazardDto LandslideHazard { get; set; }
        public required RockFallHazardDto RockFallHazard { get; set; }
        public required FloodHazardDto FloodHazard { get; set; }
        public required TsunamiHazardDto TsunamiHazard { get; set; }
        public required SoilDto Soil { get; set; }
    }
}