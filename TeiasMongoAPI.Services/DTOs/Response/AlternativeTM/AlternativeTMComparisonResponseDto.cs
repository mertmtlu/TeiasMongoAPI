using TeiasMongoAPI.Services.DTOs.Request.Common;
using TeiasMongoAPI.Services.DTOs.Response.Hazard;

namespace TeiasMongoAPI.Services.DTOs.Response.AlternativeTM
{
    public class AlternativeTMComparisonResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public LocationRequestDto Location { get; set; } = null!;
        public AddressDto Address { get; set; } = null!;
        public HazardSummaryResponseDto HazardSummary { get; set; } = null!;
        public double DistanceFromOriginal { get; set; }
        public ComparisonScoreDto ComparisonScore { get; set; } = null!;
    }

    public class ComparisonScoreDto
    {
        public double EarthquakeImprovement { get; set; }
        public double HazardImprovement { get; set; }
        public double OverallImprovement { get; set; }
        public List<string> Advantages { get; set; } = new();
        public List<string> Disadvantages { get; set; } = new();
    }
}