using TeiasMongoAPI.Services.DTOs.Response.Client;

namespace TeiasMongoAPI.Services.DTOs.Response.User
{
    public class UserDetailDto : UserDto
    {
        public List<string> Permissions { get; set; } = new();
        public List<string> AssignedClientIds { get; set; } = new();
        public DateTime? ModifiedDate { get; set; }
        public List<ClientSummaryResponseDto> AssignedClients { get; set; } = new();
    }
}