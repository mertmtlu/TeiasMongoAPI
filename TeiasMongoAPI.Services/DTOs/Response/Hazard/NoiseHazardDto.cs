using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.DTOs.Response.Hazard
{
    public class NoiseHazardDto : BaseHazardDto
    {
        public Dictionary<string, double> NoiseMeasurementsForBuildings { get; set; } = new();
        public Dictionary<LocationDto, double> NoiseMeasurementsForCoordinates { get; set; } = new();
        public bool ResidentialArea { get; set; }
        public bool Exists { get; set; }
        public bool ExtremeNoise { get; set; }
        public string ExtremeNoiseDescription { get; set; } = string.Empty;
    }
}