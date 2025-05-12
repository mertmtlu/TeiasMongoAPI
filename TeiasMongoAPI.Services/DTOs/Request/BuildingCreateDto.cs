using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Services.DTOs.Request
{
    public class BuildingCreateDto
    {
        public required string TmId { get; set; } // Will be converted to ObjectId
        public int BuildingTMID { get; set; }
        public string Name { get; set; } = string.Empty;
        public BuildingType Type { get; set; }
        public bool InScopeOfMETU { get; set; }
        public List<BlockDto> Blocks { get; set; } = new();
        public string ReportName { get; set; } = string.Empty;
    }

    // Base Block DTO for creation
    public abstract class BlockDto
    {
        public string ID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ModelingType { get; set; } = string.Empty; // "Concrete" or "Masonry"
        public double XAxisLength { get; set; }
        public double YAxisLength { get; set; }
        public required Dictionary<int, double> StoreyHeight { get; set; } = new();
    }

    public class ConcreteBlockDto : BlockDto
    {
        public double CompressiveStrengthOfConcrete { get; set; }
        public double YieldStrengthOfSteel { get; set; }
        public double TransverseReinforcementSpacing { get; set; }
        public double ReinforcementRatio { get; set; }
        public bool HookExists { get; set; }
        public bool IsStrengthened { get; set; }

        public ConcreteBlockDto()
        {
            ModelingType = "Concrete";
        }
    }

    public class MasonryBlockDto : BlockDto
    {
        public List<string> UnitTypeList { get; set; } = new(); // Simplified for DTO

        public MasonryBlockDto()
        {
            ModelingType = "Masonry";
        }
    }
}