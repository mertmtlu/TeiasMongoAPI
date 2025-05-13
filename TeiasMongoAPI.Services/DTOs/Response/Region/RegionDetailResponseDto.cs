using TeiasMongoAPI.Services.DTOs.Response.Client;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.TM;

namespace TeiasMongoAPI.Services.DTOs.Response.Region
{
    public class RegionDetailResponseDto : RegionResponseDto
    {
        public ClientSummaryResponseDto Client { get; set; } = null!;
        public int TMCount { get; set; }
        public int ActiveTMCount { get; set; }
        public List<TMSummaryResponseDto> TMs { get; set; } = new();
        public AuditInfoResponseDto AuditInfo { get; set; } = null!;
    }
}