using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Hazard;
using TeiasMongoAPI.Services.DTOs.Response.TM;

namespace TeiasMongoAPI.Services.DTOs.Response.AlternativeTM
{
    public class AlternativeTMDto
    {
        public string Id { get; set; } = string.Empty;
        public string TmId { get; set; } = string.Empty;
        public LocationDto Location { get; set; } = null!;
        public AddressDto Address { get; set; } = null!;
        public EarthquakeLevelDto DD1 { get; set; } = null!;
        public EarthquakeLevelDto DD2 { get; set; } = null!;
        public EarthquakeLevelDto DD3 { get; set; } = null!;
        public EarthquakeLevelDto? EarthquakeScenario { get; set; }
    }
}