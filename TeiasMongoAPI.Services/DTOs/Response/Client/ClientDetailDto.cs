using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Region;

namespace TeiasMongoAPI.Services.DTOs.Response.Client
{
    public class ClientDetailDto : ClientDto
    {
        public int RegionCount { get; set; }
        public List<RegionSummaryDto> Regions { get; set; } = new();
        public AuditInfoDto AuditInfo { get; set; } = null!;
    }
}