using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Block
{
    public class ConcreteCreateDto
    {
        [Required]
        [MaxLength(50)]
        public required string ID { get; set; }

        [Required]
        [MaxLength(200)]
        public required string Name { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public double XAxisLength { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public double YAxisLength { get; set; }

        [Required]
        [MinLength(1)]
        public required Dictionary<int, double> StoreyHeight { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public double CompressiveStrengthOfConcrete { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public double YieldStrengthOfSteel { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public double TransverseReinforcementSpacing { get; set; }

        [Required]
        [Range(0, 1)]
        public double ReinforcementRatio { get; set; }

        public bool HookExists { get; set; }

        public bool IsStrengthened { get; set; }
    }
}