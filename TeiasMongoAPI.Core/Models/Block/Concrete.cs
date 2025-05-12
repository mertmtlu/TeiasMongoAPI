using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Core.Models.Block
{
    public class Concrete : ABlock
    {
        public override ModelingType ModelingType => ModelingType.Concrete;

        public double CompressiveStrengthOfConcrete { get; set; }
        public double YieldStrengthOfSteel { get; set; }
        public double TransverseReinforcementSpacing { get; set; }
        [Range(0, 1)] public double ReinforcementRatio { get; set; }
        public bool HookExists { get; set; }
        public bool IsStrengthened { get; set; }
    }
}
