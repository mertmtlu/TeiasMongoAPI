using TeiasMongoAPI.Services.DTOs.Response.Block;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.TM;

namespace TeiasMongoAPI.Services.DTOs.Response.Building
{
    public class BuildingDetailDto : BuildingDto
    {
        public TMSummaryDto TM { get; set; } = null!;
        public List<BlockDto> Blocks { get; set; } = new();
        public int BlockCount { get; set; }
        public AuditInfoDto AuditInfo { get; set; } = null!;
    }
}