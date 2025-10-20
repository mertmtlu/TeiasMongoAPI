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
        public List<RemoteAppPermissionDto> Permissions { get; set; } = new();
        public string? DefaultUsername { get; set; }
        public string? DefaultPassword { get; set; }
        public string? SsoUrl { get; set; }
    }
}