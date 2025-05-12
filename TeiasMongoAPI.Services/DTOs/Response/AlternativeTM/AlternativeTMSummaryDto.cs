using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.DTOs.Response.AlternativeTM
{
    public class AlternativeTMSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public LocationDto Location { get; set; } = null!;
        public string City { get; set; } = string.Empty;
        public double OverallRiskScore { get; set; }
    }
}