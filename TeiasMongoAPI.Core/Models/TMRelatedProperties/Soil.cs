namespace TeiasMongoAPI.Core.Models.TMRelatedProperties
{
    public class Soil
    {
        public bool HasSoilStudyReport { get; set; }
        public DateOnly SoilStudyReportDate { get; set; }
        public string SoilClassDataSource { get; set; } = string.Empty;
        public string GeotechnicalReport { get; set; } = string.Empty;
        public string Results { get; set; } = string.Empty;
        public int DrillHoleCount { get; set; }
        public TDY2007SoilClass SoilClassTDY2007 { get; set; }
        public TBDY2018SoilClass SoilClassTBDY2018 { get; set; }
        public TBDY2018SoilClass FinalDecisionOnOldData { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string NewSoilClassDataReport { get; set; } = string.Empty;
        public string NewLiquefactionRiskDataReport { get; set; } = string.Empty;
        public string GeotechnicalReportMTV { get; set; } = string.Empty;
        public string LiquefactionRiskGeotechnicalReport { get; set; } = string.Empty;
        public double DistanceToActiveFaultKm { get; set; }
        public TBDY2018SoilClass FinalSoilClassification { get; set; }
        public double SoilVS30 { get; set; }
        public string StructureType { get; set; } = string.Empty;
        public string VASS { get; set; } = string.Empty;
        public bool LiquefactionRisk { get; set; }
    }

    // Enums for the soil classification types
    public enum TDY2007SoilClass
    {
        Z1,
        Z2,
        Z3,
        Z4
    }

    public enum TBDY2018SoilClass
    {
        ZA,
        ZB,
        ZC,
        ZD,
        ZE
    }
}
