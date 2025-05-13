using TeiasMongoAPI.Services.DTOs.Response.Region;
using TeiasMongoAPI.Services.DTOs.Response.TM;

namespace TeiasMongoAPI.Services.DTOs.Response.User
{
    public class UserDetailDto : UserDto
    {
        public List<string> Permissions { get; set; } = new();
        public List<string> AssignedRegionIds { get; set; } = new();
        public List<string> AssignedTMIds { get; set; } = new();
        public DateTime? ModifiedDate { get; set; }
        public List<RegionSummaryResponseDto> AssignedRegions { get; set; } = new();
        public List<TMSummaryResponseDto> AssignedTMs { get; set; } = new();
    }
}