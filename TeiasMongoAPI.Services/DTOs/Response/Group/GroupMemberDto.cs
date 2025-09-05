namespace TeiasMongoAPI.Services.DTOs.Response.Group
{
    public class GroupMemberDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public bool IsActive { get; set; }
    }
}