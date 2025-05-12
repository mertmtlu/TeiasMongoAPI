using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Core.Models.Hazard.MongoAPI.Models.Hazards;

namespace TeiasMongoAPI.Core.Models.Hazard
{
    public class AvalancheHazard : AHazard<AvalancheEliminationMethod>
    {
        //public string Measure {  get; set; } = string.Empty;
        //public string MeasureDescription { get; set; } = string.Empty;
        public string Incident { get; set; } = string.Empty;
        public string IncidentDescription { get; set; } = string.Empty;
        public double SnowDepth { get; set; }
        public required Location FirstHillLocation { get; set; }
        public double ElevationDifference { get; set; } // TODO: Can be calculated automatically

    }
}
