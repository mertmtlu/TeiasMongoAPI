using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Core.Models.Block
{
    public class Masonry : ABlock
    {
        public override ModelingType ModelingType => ModelingType.Masonry;
        public List<MasonryUnitType> UnitTypeList = new();
    }
}
