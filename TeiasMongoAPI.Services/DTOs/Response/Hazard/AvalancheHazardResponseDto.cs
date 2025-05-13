using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.DTOs.Response.Hazard
{
    public class AvalancheHazardResponseDto : BaseHazardResponseDto
    {
        public string Incident { get; set; } = string.Empty;
        public string IncidentDescription { get; set; } = string.Empty;
        public double SnowDepth { get; set; }
        public LocationResponseDto FirstHillLocation { get; set; } = null!;
        public double ElevationDifference { get; set; }
    }
}