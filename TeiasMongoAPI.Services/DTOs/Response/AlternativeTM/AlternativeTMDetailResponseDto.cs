using TeiasMongoAPI.Services.DTOs.Response.Hazard;
using TeiasMongoAPI.Services.DTOs.Response.TM;

namespace TeiasMongoAPI.Services.DTOs.Response.AlternativeTM
{
    public class AlternativeTMDetailResponseDto : AlternativeTMResponseDto
    {
        public TMSummaryResponseDto TM { get; set; } = null!;
        public PollutionResponseDto Pollution { get; set; } = null!;
        public FireHazardResponseDto FireHazard { get; set; } = null!;
        public SecurityHazardResponseDto SecurityHazard { get; set; } = null!;
        public NoiseHazardResponseDto NoiseHazard { get; set; } = null!;
        public AvalancheHazardResponseDto AvalancheHazard { get; set; } = null!;
        public LandslideHazardResponseDto LandslideHazard { get; set; } = null!;
        public RockFallHazardResponseDto RockFallHazard { get; set; } = null!;
        public FloodHazardResponseDto FloodHazard { get; set; } = null!;
        public TsunamiHazardResponseDto TsunamiHazard { get; set; } = null!;
        public SoilResponseDto Soil { get; set; } = null!;
        public HazardSummaryResponseDto HazardSummary { get; set; } = null!;
    }
}