namespace TeiasMongoAPI.Services.DTOs.Response.Hazard
{
    public class SecurityHazardResponseDto : BaseHazardResponseDto
    {
        public bool HasSecuritySystem { get; set; }
        public int SecuritySystemScore { get; set; }
        public int EGMRiskLevel { get; set; }
        public int EGMRiskLevelScore { get; set; }
        public string PerimeterFenceType { get; set; } = string.Empty; // String representation of PerimeterWallType enum
        public int PerimeterWallTypeScore { get; set; }
        public string WallCondition { get; set; } = string.Empty; // String representation of WallCondition enum
        public int WallConditionScore { get; set; }
        public bool HasCCTV { get; set; }
        public int CCTVConditionScore { get; set; }
        public int IEMDistance { get; set; }
        public int IEMDistanceScore { get; set; }
    }
}