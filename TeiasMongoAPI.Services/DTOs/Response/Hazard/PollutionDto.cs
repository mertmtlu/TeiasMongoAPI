using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.DTOs.Response.Hazard
{
    public class PollutionDto
    {
        public LocationDto PollutantLocation { get; set; } = null!;
        public int PollutantNo { get; set; }
        public string PollutantSource { get; set; } = string.Empty;
        public double PollutantDistance { get; set; }
        public string PollutantLevel { get; set; } = string.Empty; // String representation of Level enum
    }
}