using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Services.DTOs.Request.Search
{
    public class BuildingSearchDto
    {
        public string? Name { get; set; }
        public string? TmId { get; set; }
        public BuildingType? Type { get; set; }
        public bool? InScopeOfMETU { get; set; }
        public string? ReportName { get; set; }
    }
}