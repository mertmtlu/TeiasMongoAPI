namespace TeiasMongoAPI.Services.DTOs.Request
{
    // Separate DTOs for creating blocks directly (not nested in Building)
    public class BlockCreateDto
    {
        public string BuildingId { get; set; } = string.Empty; // If blocks are associated with buildings
        public string ID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public required string ModelingType { get; set; } // "Concrete" or "Masonry"
        public double XAxisLength { get; set; }
        public double YAxisLength { get; set; }
        public required Dictionary<int, double> StoreyHeight { get; set; } = new();

        // Concrete-specific properties (only used if ModelingType is "Concrete")
        public double? CompressiveStrengthOfConcrete { get; set; }
        public double? YieldStrengthOfSteel { get; set; }
        public double? TransverseReinforcementSpacing { get; set; }
        public double? ReinforcementRatio { get; set; }
        public bool? HookExists { get; set; }
        public bool? IsStrengthened { get; set; }

        // Masonry-specific properties (only used if ModelingType is "Masonry")
        public List<string>? UnitTypeList { get; set; }
    }

    public class BlockUpdateDto
    {
        public string? ID { get; set; }
        public string? Name { get; set; }
        public double? XAxisLength { get; set; }
        public double? YAxisLength { get; set; }
        public Dictionary<int, double>? StoreyHeight { get; set; }

        // Concrete-specific properties
        public double? CompressiveStrengthOfConcrete { get; set; }
        public double? YieldStrengthOfSteel { get; set; }
        public double? TransverseReinforcementSpacing { get; set; }
        public double? ReinforcementRatio { get; set; }
        public bool? HookExists { get; set; }
        public bool? IsStrengthened { get; set; }

        // Masonry-specific properties
        public List<string>? UnitTypeList { get; set; }

        // Note: ModelingType should not be updatable as it determines the block type
    }
}