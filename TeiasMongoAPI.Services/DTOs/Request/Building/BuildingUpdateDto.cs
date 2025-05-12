using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Services.DTOs.Request.Building
{
    public class BuildingUpdateDto
    {
        public string? TmId { get; set; }
        public int? BuildingTMID { get; set; }
        public string? Name { get; set; }
        public BuildingType? Type { get; set; }
        public bool? InScopeOfMETU { get; set; }
        public string? ReportName { get; set; }
    }
}