using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Base;
using TeiasMongoAPI.Core.Models.Block;

namespace TeiasMongoAPI.Core.Models.KeyModels
{
    public class Building : AEntityBase
    {
        public required ObjectId TmID { get; set; }
        public int BuildingTMID { get; set; }
        public string Name { get; set; } = string.Empty;
        public BuildingType Type { get; set; }
        public bool InScopeOfMETU { get; set; }
        public List<ABlock> Blocks { get; set; } = new();
        public string ReportName { get; set; } = string.Empty;
        //public ReportInput ReportInput { get; set; }

        [BsonIgnore]
        public int Code
        {
            get
            {
                int code = Type switch
                {
                    BuildingType.Control => 1,
                    BuildingType.Switchyard => 2,
                    BuildingType.Security => 10,
                    _ => 1
                };

                return code;
            }
        }
        [BsonIgnore]
        public int BKS
        {
            get
            {
                switch (Type)
                {
                    case BuildingType.Control: return 3;
                    default: return 1;
                }
            }
        }
    }

    public enum BuildingType
    {
        Control,
        Security,
        Switchyard
    }
}
