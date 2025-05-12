namespace TeiasMongoAPI.Services.DTOs.Response
{
    // Base Hazard DTO
    public abstract class HazardDto
    {
        public double Score { get; set; }
        public string Level { get; set; } = string.Empty; // VeryLow, Low, Medium, High
        public Dictionary<string, int> EliminationCosts { get; set; } = new();
        public bool PreviousIncidentOccurred { get; set; }
        public string PreviousIncidentDescription { get; set; } = string.Empty;
        public double DistanceToInventory { get; set; }
    }

    // Fire Hazard DTO
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

    // Security Hazard DTO
    public class SecurityHazardDto : HazardDto
    {
        public bool HasSecuritySystem { get; set; }
        public int SecuritySystemScore { get; set; }
        public int EGMRiskLevel { get; set; }
        public int EGMRiskLevelScore { get; set; }
        public string PerimeterFenceType { get; set; } = string.Empty; // None, Concrete, WireMesh
        public int PerimeterWallTypeScore { get; set; }
        public string WallCondition { get; set; } = string.Empty; // None, Solid, Unstable
        public int WallConditionScore { get; set; }
        public bool HasCCTV { get; set; }
        public int CCTVConditionScore { get; set; }
        public int IEMDistance { get; set; }
        public int IEMDistanceScore { get; set; }
    }

    // Noise Hazard DTO
    public class NoiseHazardDto : HazardDto
    {
        public Dictionary<string, double> NoiseMeasurementsForBuildings { get; set; } = new();
        public Dictionary<string, double> NoiseMeasurementsForCoordinates { get; set; } = new(); // Serialized location as key
        public bool ResidentialArea { get; set; }
        public bool Exists { get; set; }
        public bool ExtremeNoise { get; set; }
        public string ExtremeNoiseDescription { get; set; } = string.Empty;
    }

    // Avalanche Hazard DTO
    public class AvalancheHazardDto : HazardDto
    {
        public string Incident { get; set; } = string.Empty;
        public string IncidentDescription { get; set; } = string.Empty;
        public double SnowDepth { get; set; }
        public LocationDto FirstHillLocation { get; set; } = new();
        public double ElevationDifference { get; set; }
    }

    // Landslide Hazard DTO
    public class LandslideHazardDto : HazardDto
    {
    }

    // RockFall Hazard DTO
    public class RockFallHazardDto : HazardDto
    {
    }

    // Flood Hazard DTO
    public class FloodHazardDto : HazardDto
    {
        public string Incident { get; set; } = string.Empty;
        public string IncidentDescription { get; set; } = string.Empty;
        public string DrainageSystem { get; set; } = string.Empty;
        public string BasementFlooding { get; set; } = string.Empty;
        public string ExtremeEventCondition { get; set; } = string.Empty;
    }

    // Tsunami Hazard DTO
    public class TsunamiHazardDto : HazardDto
    {
    }
}