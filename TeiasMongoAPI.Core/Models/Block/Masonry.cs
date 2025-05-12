namespace TeiasMongoAPI.Core.Models.Block
{
    public class Masonry : ABlock
    {
        public override ModelingType ModelingType => ModelingType.Masonry;
        public List<MasonryUnitType> UnitTypeList = new();
    }
}
