using TeiasMongoAPI.Services.DTOs.Response.Block;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.TM;

namespace TeiasMongoAPI.Services.DTOs.Response.Building
{
    public class BuildingDetailResponseDto : BuildingResponseDto
    {
        public TMSummaryResponseDto TM { get; set; } = null!;
        public List<BlockResponseDto> Blocks { get; set; } = new();
        public int BlockCount { get; set; }
        public AuditInfoResponseDto AuditInfo { get; set; } = null!;
    }
}