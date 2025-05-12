using TeiasMongoAPI.Core.Models.TMRelatedProperties;

namespace TeiasMongoAPI.Services.DTOs.Request.Common
{
    public class SoilDto
    {
        public bool HasSoilStudyReport { get; set; }
        public DateTime? SoilStudyReportDate { get; set; }
        public string? SoilClassDataSource { get; set; }
        public string? GeotechnicalReport { get; set; }
        public string? Results { get; set; }
        public int DrillHoleCount { get; set; }
        public TDY2007SoilClass? SoilClassTDY2007 { get; set; }
        public TBDY2018SoilClass? SoilClassTBDY2018 { get; set; }
        public TBDY2018SoilClass? FinalDecisionOnOldData { get; set; }
        public string? Notes { get; set; }
        public string? NewSoilClassDataReport { get; set; }
        public string? NewLiquefactionRiskDataReport { get; set; }
        public string? GeotechnicalReportMTV { get; set; }
        public string? LiquefactionRiskGeotechnicalReport { get; set; }
        public double DistanceToActiveFaultKm { get; set; }
        public TBDY2018SoilClass? FinalSoilClassification { get; set; }
        public double SoilVS30 { get; set; }
        public string? StructureType { get; set; }
        public string? VASS { get; set; }
        public bool LiquefactionRisk { get; set; }
    }
}