using TeiasMongoAPI.Core.Models.Common;

namespace TeiasMongoAPI.Services.DTOs.Response.Icon
{
    public class IconStatsResponseDto
    {
        public required IconEntityType EntityType { get; set; }
        public long TotalCount { get; set; }
    }
}