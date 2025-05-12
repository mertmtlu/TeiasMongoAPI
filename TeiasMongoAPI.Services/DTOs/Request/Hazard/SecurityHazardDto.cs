using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Core.Models.Hazard;

namespace TeiasMongoAPI.Services.DTOs.Request.Hazard
{
    public class SecurityHazardDto
    {
        [Range(0, 1)]
        public double Score { get; set; }

        public Level Level { get; set; }

        public Dictionary<string, int>? EliminationCosts { get; set; }

        [Required]
        public bool PreviousIncidentOccurred { get; set; }

        public string? PreviousIncidentDescription { get; set; }

        [Required]
        public double DistanceToInventory { get; set; }

        public bool HasSecuritySystem { get; set; }
        public int SecuritySystemScore { get; set; }
        public int EGMRiskLevel { get; set; }
        public int EGMRiskLevelScore { get; set; }
        public PerimeterWallType PerimeterFenceType { get; set; }
        public int PerimeterWallTypeScore { get; set; }
        public WallCondition WallCondition { get; set; }
        public int WallConditionScore { get; set; }
        public bool HasCCTV { get; set; }
        public int CCTVConditionScore { get; set; }
        public int IEMDistance { get; set; }
        public int IEMDistanceScore { get; set; }
    }
}