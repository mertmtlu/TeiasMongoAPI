namespace TeiasMongoAPI.Services.DTOs.Response.RemoteApp
{
    public class RemoteAppListDto
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public required string Url { get; set; }
        public bool IsPublic { get; set; }
        public required string Creator { get; set; }
        public string Status { get; set; } = "active";
        public DateTime CreatedAt { get; set; }
    }
}