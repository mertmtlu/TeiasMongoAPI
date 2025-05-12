using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Core.Models.Hazard.MongoAPI.Models.Hazards;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Models.Hazard
{
    public class NoiseHazard : AHazard<NoiseEliminationMethod>
    {
        public Dictionary<BuildingType, double> NoiseMeasurementsForBuildings { get; set; } = new();
        public Dictionary<Location, double> NoiseMeasurementsForCoordinates { get; set; } = new();
        public bool ResidentialArea { get; set; }
        public bool Exists { get; set; } // Noise hazard exists
        public bool ExtremeNoise { get; set; }
        public string ExtremeNoiseDescription { get; set; } = string.Empty;
    }
}
