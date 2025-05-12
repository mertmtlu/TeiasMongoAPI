using TeiasMongoAPI.Core.Models.Hazard.MongoAPI.Models.Hazards;

namespace TeiasMongoAPI.Core.Models.Hazard
{
    public class SecurityHazard : AHazard<SecurityEliminationMethod>
    {
        public bool HasSecuritySystem { get; set; }
        public int SecuritySystemScore { get; set; }
        public int EGMRiskLevel { get; set; }
        public int EGMRiskLevelScore { get; set; }
        public PerimeterWallType PerimeterFenceType { get; set; } = PerimeterWallType.None;
        public int PerimeterWallTypeScore { get; set; }
        public WallCondition WallCondition { get; set; } = WallCondition.None;
        public int WallConditionScore { get; set; }
        public bool HasCCTV { get; set; }
        public int CCTVConditionScore { get; set; }
        public int IEMDistance { get; set; }
        public int IEMDistanceScore { get; set; }
    }

    public enum PerimeterWallType
    {
        None,
        Concrete,
        WireMesh
    }

    public enum WallCondition
    {
        None,
        Solid,
        Unstable
    }
}
