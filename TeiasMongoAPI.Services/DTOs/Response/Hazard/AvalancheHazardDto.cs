using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.DTOs.Response.Hazard
{
    public class AvalancheHazardDto : BaseHazardDto
    {
        public string Incident { get; set; } = string.Empty;
        public string IncidentDescription { get; set; } = string.Empty;
        public double SnowDepth { get; set; }
        public LocationDto FirstHillLocation { get; set; } = null!;
        public double ElevationDifference { get; set; }
    }
}