namespace TeiasMongoAPI.Services.DTOs.Response.User
{
    public class UserProfileDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
    }
}