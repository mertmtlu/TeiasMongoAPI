using TeiasMongoAPI.Services.DTOs.Response.Hazard;
using TeiasMongoAPI.Services.DTOs.Response.TM;

namespace TeiasMongoAPI.Services.DTOs.Response.AlternativeTM
{
    public class AlternativeTMDetailDto : AlternativeTMDto
    {
        public TMSummaryDto TM { get; set; } = null!;
        public PollutionDto Pollution { get; set; } = null!;
        public FireHazardDto FireHazard { get; set; } = null!;
        public SecurityHazardDto SecurityHazard { get; set; } = null!;
        public NoiseHazardDto NoiseHazard { get; set; } = null!;
        public AvalancheHazardDto AvalancheHazard { get; set; } = null!;
        public LandslideHazardDto LandslideHazard { get; set; } = null!;
        public RockFallHazardDto RockFallHazard { get; set; } = null!;
        public FloodHazardDto FloodHazard { get; set; } = null!;
        public TsunamiHazardDto TsunamiHazard { get; set; } = null!;
        public SoilDto Soil { get; set; } = null!;
        public HazardSummaryDto HazardSummary { get; set; } = null!;
    }
}