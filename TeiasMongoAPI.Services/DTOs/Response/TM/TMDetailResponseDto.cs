using TeiasMongoAPI.Services.DTOs.Request.Common;
using TeiasMongoAPI.Services.DTOs.Request.Hazard;
using TeiasMongoAPI.Services.DTOs.Response.AlternativeTM;
using TeiasMongoAPI.Services.DTOs.Response.Building;
using TeiasMongoAPI.Services.DTOs.Response.Region;

namespace TeiasMongoAPI.Services.DTOs.Response.TM
{
    public class TMDetailResponseDto : TMResponseDto
    {
        public RegionSummaryResponseDto Region { get; set; } = null!;
        public EarthquakeLevelResponseDto DD1 { get; set; } = null!;
        public EarthquakeLevelResponseDto DD2 { get; set; } = null!;
        public EarthquakeLevelResponseDto DD3 { get; set; } = null!;
        public EarthquakeLevelResponseDto? EarthquakeScenario { get; set; }
        public PollutionDto Pollution { get; set; } = null!;
        public FireHazardDto FireHazard { get; set; } = null!;
        public SecurityHazardDto SecurityHazard { get; set; } = null!;
        public NoiseHazardDto NoiseHazard { get; set; } = null!;
        public AvalancheHazardDto AvalancheHazard { get; set; } = null!;
        public LandslideHazardDto LandslideHazard { get; set; } = null!;
        public RockFallHazardDto RockFallHazard { get; set; } = null!;
        public FloodHazardDto FloodHazard { get; set; } = null!;
        public TsunamiHazardDto TsunamiHazard { get; set; } = null!;
        public SoilResponseDto Soil { get; set; } = null!;
        public int BuildingCount { get; set; }
        public List<BuildingSummaryResponseDto> Buildings { get; set; } = new();
        public List<AlternativeTMSummaryResponseDto> AlternativeTMs { get; set; } = new();
    }
}