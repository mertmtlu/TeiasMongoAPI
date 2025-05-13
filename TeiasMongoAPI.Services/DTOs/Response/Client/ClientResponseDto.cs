namespace TeiasMongoAPI.Services.DTOs.Response.Client
{
    public class ClientResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Private" or "State"
    }
}