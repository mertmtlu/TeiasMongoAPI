using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Services.DTOs.Request
{
    public class BuildingUpdateDto
    {
        public string? Name { get; set; }
        public BuildingType? Type { get; set; }
        public bool? InScopeOfMETU { get; set; }
        public List<BlockDto>? Blocks { get; set; }
        public string? ReportName { get; set; }

        // Note: TmId and BuildingTMID should not be updatable
    }
}