namespace TeiasMongoAPI.Services.DTOs.Response.TM
{
    public class SoilDto
    {
        public bool HasSoilStudyReport { get; set; }
        public DateTime? SoilStudyReportDate { get; set; }
        public string SoilClassDataSource { get; set; } = string.Empty;
        public string GeotechnicalReport { get; set; } = string.Empty;
        public string Results { get; set; } = string.Empty;
        public int DrillHoleCount { get; set; }
        public string? SoilClassTDY2007 { get; set; } // String representation of enum
        public string? SoilClassTBDY2018 { get; set; } // String representation of enum
        public string? FinalDecisionOnOldData { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string NewSoilClassDataReport { get; set; } = string.Empty;
        public string NewLiquefactionRiskDataReport { get; set; } = string.Empty;
        public string GeotechnicalReportMTV { get; set; } = string.Empty;
        public string LiquefactionRiskGeotechnicalReport { get; set; } = string.Empty;
        public double DistanceToActiveFaultKm { get; set; }
        public string? FinalSoilClassification { get; set; }
        public double SoilVS30 { get; set; }
        public string StructureType { get; set; } = string.Empty;
        public string VASS { get; set; } = string.Empty;
        public bool LiquefactionRisk { get; set; }
    }
}