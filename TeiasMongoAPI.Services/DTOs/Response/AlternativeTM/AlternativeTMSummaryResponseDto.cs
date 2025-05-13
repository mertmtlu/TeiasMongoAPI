using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.DTOs.Response.AlternativeTM
{
    public class AlternativeTMSummaryResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public LocationResponseDto Location { get; set; } = null!;
        public string City { get; set; } = string.Empty;
        public double OverallRiskScore { get; set; }
    }
}