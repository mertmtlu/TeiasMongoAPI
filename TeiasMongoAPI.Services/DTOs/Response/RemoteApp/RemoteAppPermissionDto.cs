namespace TeiasMongoAPI.Services.DTOs.Response.RemoteApp
{
    public class RemoteAppPermissionDto
    {
        public string Type { get; set; } = string.Empty; // "user" or "group"
        public string Id { get; set; } = string.Empty; // User ID or Group ID
        public string Name { get; set; } = string.Empty; // User name or Group name
        public string AccessLevel { get; set; } = string.Empty;
    }
}
