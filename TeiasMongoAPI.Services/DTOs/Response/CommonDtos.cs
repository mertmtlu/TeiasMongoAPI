namespace TeiasMongoAPI.Services.DTOs.Response
{
    // Location DTO
    public class LocationDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    // Earthquake Level DTO
    public class EarthquakeLevelDto
    {
        public double PGA { get; set; }
        public double PGV { get; set; }
        public double Ss { get; set; }
        public double S1 { get; set; }
        public double Sds { get; set; }
        public double Sd1 { get; set; }
    }

    // Pollution DTO
    public class PollutionDto
    {
        public LocationDto PollutantLocation { get; set; } = new();
        public int PollutantNo { get; set; }
        public string PollutantSource { get; set; } = string.Empty;
        public double PollutantDistance { get; set; }
        public string PollutantLevel { get; set; } = string.Empty; // VeryLow, Low, Medium, High
    }

    // Soil DTO
    public class SoilDto
    {
        public bool HasSoilStudyReport { get; set; }
        public DateTime SoilStudyReportDate { get; set; }
        public string SoilClassDataSource { get; set; } = string.Empty;
        public string GeotechnicalReport { get; set; } = string.Empty;
        public string Results { get; set; } = string.Empty;
        public int DrillHoleCount { get; set; }
        public string SoilClassTDY2007 { get; set; } = string.Empty; // Z1, Z2, Z3, Z4
        public string SoilClassTBDY2018 { get; set; } = string.Empty; // ZA, ZB, ZC, ZD, ZE
        public string FinalDecisionOnOldData { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string NewSoilClassDataReport { get; set; } = string.Empty;
        public string NewLiquefactionRiskDataReport { get; set; } = string.Empty;
        public string GeotechnicalReportMTV { get; set; } = string.Empty;
        public string LiquefactionRiskGeotechnicalReport { get; set; } = string.Empty;
        public double DistanceToActiveFaultKm { get; set; }
        public string FinalSoilClassification { get; set; } = string.Empty;
        public double SoilVS30 { get; set; }
        public string StructureType { get; set; } = string.Empty;
        public string VASS { get; set; } = string.Empty;
        public bool LiquefactionRisk { get; set; }
    }
}