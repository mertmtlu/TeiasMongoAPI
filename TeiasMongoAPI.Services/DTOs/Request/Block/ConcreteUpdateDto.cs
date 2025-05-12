using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Block
{
    public class ConcreteUpdateDto
    {
        [MaxLength(50)]
        public string? ID { get; set; }

        [MaxLength(200)]
        public string? Name { get; set; }

        [Range(0, double.MaxValue)]
        public double? XAxisLength { get; set; }

        [Range(0, double.MaxValue)]
        public double? YAxisLength { get; set; }

        public Dictionary<int, double>? StoreyHeight { get; set; }

        [Range(0, double.MaxValue)]
        public double? CompressiveStrengthOfConcrete { get; set; }

        [Range(0, double.MaxValue)]
        public double? YieldStrengthOfSteel { get; set; }

        [Range(0, double.MaxValue)]
        public double? TransverseReinforcementSpacing { get; set; }

        [Range(0, 1)]
        public double? ReinforcementRatio { get; set; }

        public bool? HookExists { get; set; }

        public bool? IsStrengthened { get; set; }
    }
}