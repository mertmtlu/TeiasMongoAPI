namespace TeiasMongoAPI.Services.DTOs.Response
{
    public class TMDto
    {
        public string Id { get; set; } = string.Empty;
        public string RegionId { get; set; } = string.Empty;
        public int TMId { get; set; }  // Maps to Id in the domain model
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Default, GIS
        public string State { get; set; } = string.Empty; // Active, Inactive
        public List<int> Voltages { get; set; } = new();
        public int MaxVoltage { get; set; } // Calculated property
        public DateOnly ProvisionalAcceptanceDate { get; set; }
        public LocationDto Location { get; set; } = new();
        public string City { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public EarthquakeLevelDto DD1 { get; set; } = new();
        public EarthquakeLevelDto DD2 { get; set; } = new();
        public EarthquakeLevelDto DD3 { get; set; } = new();
        public EarthquakeLevelDto? EarthquakeScenario { get; set; }
        public PollutionDto Pollution { get; set; } = new();
        public FireHazardDto FireHazard { get; set; } = new();
        public SecurityHazardDto SecurityHazard { get; set; } = new();
        public NoiseHazardDto NoiseHazard { get; set; } = new();
        public AvalancheHazardDto AvalancheHazard { get; set; } = new();
        public LandslideHazardDto LandslideHazard { get; set; } = new();
        public RockFallHazardDto RockFallHazard { get; set; } = new();
        public FloodHazardDto FloodHazard { get; set; } = new();
        public TsunamiHazardDto TsunamiHazard { get; set; } = new();
        public SoilDto Soil { get; set; } = new();
        public DateTime CreatedDate { get; set; }

        // Navigation property
        public RegionDto? Region { get; set; }
    }
}