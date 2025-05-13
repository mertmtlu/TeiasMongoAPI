using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Hazard;
using TeiasMongoAPI.Services.DTOs.Response.TM;

namespace TeiasMongoAPI.Services.DTOs.Response.AlternativeTM
{
    public class AlternativeTMResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string TmId { get; set; } = string.Empty;
        public LocationResponseDto Location { get; set; } = null!;
        public AddressResponseDto Address { get; set; } = null!;
        public EarthquakeLevelResponseDto DD1 { get; set; } = null!;
        public EarthquakeLevelResponseDto DD2 { get; set; } = null!;
        public EarthquakeLevelResponseDto DD3 { get; set; } = null!;
        public EarthquakeLevelResponseDto? EarthquakeScenario { get; set; }
    }
}