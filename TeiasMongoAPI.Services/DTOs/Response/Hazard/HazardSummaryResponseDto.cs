namespace TeiasMongoAPI.Services.DTOs.Response.Hazard
{
    public class HazardSummaryResponseDto
    {
        public double FireHazardScore { get; set; }
        public double SecurityHazardScore { get; set; }
        public double NoiseHazardScore { get; set; }
        public double AvalancheHazardScore { get; set; }
        public double LandslideHazardScore { get; set; }
        public double RockFallHazardScore { get; set; }
        public double FloodHazardScore { get; set; }
        public double TsunamiHazardScore { get; set; }
        public double OverallRiskScore { get; set; }
        public string HighestRiskType { get; set; } = string.Empty;
    }
}