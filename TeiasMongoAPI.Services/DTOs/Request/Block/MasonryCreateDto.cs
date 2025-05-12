using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Block;

namespace TeiasMongoAPI.Services.DTOs.Request.Block
{
    public class MasonryCreateDto
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

        public List<MasonryUnitType>? UnitTypeList { get; set; }
    }
}