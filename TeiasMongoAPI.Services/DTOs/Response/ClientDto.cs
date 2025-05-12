namespace TeiasMongoAPI.Services.DTOs.Response
{
    public class ClientDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Private" or "State"
        public DateTime CreatedDate { get; set; }
    }
}