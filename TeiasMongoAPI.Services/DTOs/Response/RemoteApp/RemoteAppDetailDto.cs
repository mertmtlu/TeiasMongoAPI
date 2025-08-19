namespace TeiasMongoAPI.Services.DTOs.Response.RemoteApp
{
    public class RemoteAppDetailDto
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public required string Url { get; set; }
        public bool IsPublic { get; set; }
        public required string Creator { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public string Status { get; set; } = "active";
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public List<RemoteAppAssignedUserDto> AssignedUsers { get; set; } = new();
    }

    public class RemoteAppAssignedUserDto
    {
        public required string UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}