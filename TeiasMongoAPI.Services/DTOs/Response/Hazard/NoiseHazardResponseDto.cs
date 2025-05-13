using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.DTOs.Response.Hazard
{
    public class NoiseHazardResponseDto : BaseHazardResponseDto
    {
        public Dictionary<string, double> NoiseMeasurementsForBuildings { get; set; } = new();
        public Dictionary<LocationResponseDto, double> NoiseMeasurementsForCoordinates { get; set; } = new();
        public bool ResidentialArea { get; set; }
        public bool Exists { get; set; }
        public bool ExtremeNoise { get; set; }
        public string ExtremeNoiseDescription { get; set; } = string.Empty;
    }
}