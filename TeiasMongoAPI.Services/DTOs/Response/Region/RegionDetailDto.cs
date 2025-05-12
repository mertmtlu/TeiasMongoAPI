using TeiasMongoAPI.Services.DTOs.Response.Client;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.TM;

namespace TeiasMongoAPI.Services.DTOs.Response.Region
{
    public class RegionDetailDto : RegionDto
    {
        public ClientSummaryDto Client { get; set; } = null!;
        public int TMCount { get; set; }
        public int ActiveTMCount { get; set; }
        public List<TMSummaryDto> TMs { get; set; } = new();
        public AuditInfoDto AuditInfo { get; set; } = null!;
    }
}