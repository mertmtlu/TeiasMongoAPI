using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Region;

namespace TeiasMongoAPI.Services.DTOs.Response.Client
{
    public class ClientDetailResponseDto : ClientResponseDto
    {
        public int RegionCount { get; set; }
        public List<RegionSummaryResponseDto> Regions { get; set; } = new();
        public AuditInfoResponseDto AuditInfo { get; set; } = null!;
    }
}