using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Block;

namespace TeiasMongoAPI.Services.DTOs.Request.Block
{
    public class MasonryUpdateDto
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

        public List<MasonryUnitType>? UnitTypeList { get; set; }
    }
}