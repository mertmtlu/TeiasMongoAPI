namespace TeiasMongoAPI.Services.DTOs.Response.User
{
    public class UserListDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime? LastLoginDate { get; set; }
    }
}