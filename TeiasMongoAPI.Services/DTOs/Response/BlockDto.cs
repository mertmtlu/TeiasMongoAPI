namespace TeiasMongoAPI.Services.DTOs.Response
{
    // Base Block DTO
    public abstract class BlockDto
    {
        public string Id { get; set; } = string.Empty;
        public string BlockId { get; set; } = string.Empty; // Maps to ID property in domain
        public string Name { get; set; } = string.Empty;
        public string ModelingType { get; set; } = string.Empty; // "Concrete" or "Masonry"
        public double XAxisLength { get; set; }
        public double YAxisLength { get; set; }
        public Dictionary<int, double> StoreyHeight { get; set; } = new();

        // Calculated properties
        public double LongLength { get; set; }
        public double ShortLength { get; set; }
        public double TotalHeight { get; set; }

        public DateTime CreatedDate { get; set; }
    }

    // Concrete Block DTO
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

    // Masonry Block DTO
    public class MasonryBlockDto : BlockDto
    {
        public List<string> UnitTypeList { get; set; } = new();

        public MasonryBlockDto()
        {
            ModelingType = "Masonry";
        }
    }
}