using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Services.DTOs.Request
{
    public class TMCreateDto
    {
        public required string RegionId { get; set; } // Will be converted to ObjectId
        public int Id { get; set; }
        public required string Name { get; set; }
        public TMType Type { get; set; } = TMType.Default;
        public TMState State { get; set; } = TMState.Active;
        public required List<int> Voltages { get; set; }
        public DateOnly ProvisionalAcceptanceDate { get; set; }
        public required LocationDto Location { get; set; }
        public string City { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public required EarthquakeLevelDto DD1 { get; set; }
        public required EarthquakeLevelDto DD2 { get; set; }
        public required EarthquakeLevelDto DD3 { get; set; }
        public EarthquakeLevelDto? EarthquakeScenario { get; set; }
        public required PollutionDto Pollution { get; set; }
        public required FireHazardDto FireHazard { get; set; }
        public required SecurityHazardDto SecurityHazard { get; set; }
        public required NoiseHazardDto NoiseHazard { get; set; }
        public required AvalancheHazardDto AvalancheHazard { get; set; }
        public required LandslideHazardDto LandslideHazard { get; set; }
        public required RockFallHazardDto RockFallHazard { get; set; }
        public required FloodHazardDto FloodHazard { get; set; }
        public required TsunamiHazardDto TsunamiHazard { get; set; }
        public required SoilDto Soil { get; set; }
    }

    // Common DTOs
    public class LocationDto
    {
        public required double Latitude { get; set; }
        public required double Longitude { get; set; }
    }

    public class EarthquakeLevelDto
    {
        public double PGA { get; set; }
        public double PGV { get; set; }
        public double Ss { get; set; }
        public double S1 { get; set; }
        public double Sds { get; set; }
        public double Sd1 { get; set; }
    }

    public class PollutionDto
    {
        public required LocationDto PollutantLocation { get; set; }
        public required int PollutantNo { get; set; }
        public string PollutantSource { get; set; } = string.Empty;
        public double PollutantDistance { get; set; }
        public string PollutantLevel { get; set; } = "Low"; // Will be mapped to enum
    }

    // Base Hazard DTO
    public abstract class HazardDto
    {
        public double Score { get; set; }
        public string Level { get; set; } = "Low"; // Will be mapped to enum
        public required bool PreviousIncidentOccurred { get; set; }
        public string PreviousIncidentDescription { get; set; } = string.Empty;
        public required double DistanceToInventory { get; set; }
    }

    public class FireHazardDto : HazardDto
    {
        public bool FireSystem { get; set; }
        public bool AfforestationCondition { get; set; }
        public string? ForestType { get; set; }
        public bool StubbleBurning { get; set; }
        public bool ExternalFireIncident { get; set; }
        public string ExternalFireIncidentDescription { get; set; } = string.Empty;
        public bool NearbyGasStation { get; set; }
        public double DistanceToNearbyGasStation { get; set; }
        public bool HasIndustrialFireDanger { get; set; }
        public int IndustrialFireExposedFacade { get; set; }
        public bool ForestFireDanger { get; set; }
        public double DistanceToClosestForest { get; set; }
        public string VegetationType { get; set; } = string.Empty;
    }

    public class SecurityHazardDto : HazardDto
    {
        public bool HasSecuritySystem { get; set; }
        public int SecuritySystemScore { get; set; }
        public int EGMRiskLevel { get; set; }
        public int EGMRiskLevelScore { get; set; }
        public string PerimeterFenceType { get; set; } = "None"; // Will be mapped to enum
        public int PerimeterWallTypeScore { get; set; }
        public string WallCondition { get; set; } = "None"; // Will be mapped to enum
        public int WallConditionScore { get; set; }
        public bool HasCCTV { get; set; }
        public int CCTVConditionScore { get; set; }
        public int IEMDistance { get; set; }
        public int IEMDistanceScore { get; set; }
    }

    public class NoiseHazardDto : HazardDto
    {
        public Dictionary<string, double> NoiseMeasurementsForBuildings { get; set; } = new();
        public Dictionary<LocationDto, double> NoiseMeasurementsForCoordinates { get; set; } = new();
        public bool ResidentialArea { get; set; }
        public bool Exists { get; set; }
        public bool ExtremeNoise { get; set; }
        public string ExtremeNoiseDescription { get; set; } = string.Empty;
    }

    public class AvalancheHazardDto : HazardDto
    {
        public string Incident { get; set; } = string.Empty;
        public string IncidentDescription { get; set; } = string.Empty;
        public double SnowDepth { get; set; }
        public required LocationDto FirstHillLocation { get; set; }
        public double ElevationDifference { get; set; }
    }

    public class LandslideHazardDto : HazardDto
    {
    }

    public class RockFallHazardDto : HazardDto
    {
    }

    public class FloodHazardDto : HazardDto
    {
        public string Incident { get; set; } = string.Empty;
        public string IncidentDescription { get; set; } = string.Empty;
        public string DrainageSystem { get; set; } = string.Empty;
        public string BasementFlooding { get; set; } = string.Empty;
        public string ExtremeEventCondition { get; set; } = string.Empty;
    }

    public class TsunamiHazardDto : HazardDto
    {
    }

    public class SoilDto
    {
        public bool HasSoilStudyReport { get; set; }
        public DateOnly SoilStudyReportDate { get; set; }
        public string SoilClassDataSource { get; set; } = string.Empty;
        public string GeotechnicalReport { get; set; } = string.Empty;
        public string Results { get; set; } = string.Empty;
        public int DrillHoleCount { get; set; }
        public string SoilClassTDY2007 { get; set; } = "Z1"; // Will be mapped to enum
        public string SoilClassTBDY2018 { get; set; } = "ZA"; // Will be mapped to enum
        public string FinalDecisionOnOldData { get; set; } = "ZA"; // Will be mapped to enum
        public string Notes { get; set; } = string.Empty;
        public string NewSoilClassDataReport { get; set; } = string.Empty;
        public string NewLiquefactionRiskDataReport { get; set; } = string.Empty;
        public string GeotechnicalReportMTV { get; set; } = string.Empty;
        public string LiquefactionRiskGeotechnicalReport { get; set; } = string.Empty;
        public double DistanceToActiveFaultKm { get; set; }
        public string FinalSoilClassification { get; set; } = "ZA"; // Will be mapped to enum
        public double SoilVS30 { get; set; }
        public string StructureType { get; set; } = string.Empty;
        public string VASS { get; set; } = string.Empty;
        public bool LiquefactionRisk { get; set; }
    }
}