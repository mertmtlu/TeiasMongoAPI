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
        public int Id { get; set; }
        public required string Name { get; set; }
        public TMType Type { get; set; } = TMType.Default;
        public TMState State { get; set; } = TMState.Active;
        public required List<int> Voltages { get; set; }
        [BsonIgnore] public int MaxVoltage { get => Voltages.Max(); }
        public DateOnly ProvisionalAcceptanceDate { get; set; } = new DateOnly();
        public required Location Location { get; set; }
        public string City { get; set; } = string.Empty;
        public string County { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public required EarthquakeLevel DD1 { get; set; }
        public required EarthquakeLevel DD2 { get; set; }
        public required EarthquakeLevel DD3 { get; set; }
        public EarthquakeLevel? EarthquakeScenario { get; set; } = null;
        public required Pollution Pollution { get; set; }
        public required FireHazard FireHazard { get; set; }
        public required SecurityHazard SecurityHazard { get; set; }
        public required NoiseHazard NoiseHazard { get; set; }
        public required AvalancheHazard AvalancheHazard { get; set; }
        public required LandslideHazard LandslideHazard { get; set; }
        public required RockFallHazard RockFallHazard { get; set; }
        public required FloodHazard FloodHazard { get; set; }
        public required TsunamiHazard TsunamiHazard { get; set; }
        public required Soil Soil { get; set; }
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
