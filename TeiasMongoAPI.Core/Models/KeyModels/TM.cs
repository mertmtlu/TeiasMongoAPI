using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Base;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Core.Models.Hazard;
using TeiasMongoAPI.Core.Models.TMRelatedProperties;

namespace TeiasMongoAPI.Core.Models.KeyModels
{
    public class TM : AEntityBase
    {
        public required ObjectId RegionID { get; set; }
        public int TmID { get; set; }
        public required string Name { get; set; }
        public TMType Type { get; set; } = TMType.Default;
        public TMState State { get; set; } = TMState.Active;
        public required List<int> Voltages { get; set; }
        [BsonIgnore] public int MaxVoltage { get => Voltages.Max(); }
        public DateTime ProvisionalAcceptanceDate { get; set; } = new();
        public required Location Location { get; set; }
        public string City { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public EarthquakeLevel? DD1 { get; set; } = null;
        public EarthquakeLevel? DD2 { get; set; } = null;
        public EarthquakeLevel? DD3 { get; set; } = null;
        public EarthquakeLevel? EarthquakeScenario { get; set; } = null;
        public Pollution? Pollution { get; set; } = null;
        public FireHazard? FireHazard { get; set; } = null;
        public SecurityHazard? SecurityHazard { get; set; } = null;
        public NoiseHazard? NoiseHazard { get; set; } = null;
        public AvalancheHazard? AvalancheHazard { get; set; } = null;
        public LandslideHazard? LandslideHazard { get; set; } = null;
        public RockFallHazard? RockFallHazard { get; set; } = null;
        public FloodHazard? FloodHazard { get; set; } = null;
        public TsunamiHazard? TsunamiHazard { get; set; } = null;
        public Soil? Soil { get; set; } = null;
    }

    public enum TMType
    {
        Default,
        GIS,
    }

    public enum TMState
    {
        Active,
        Inactive,
    }
}
