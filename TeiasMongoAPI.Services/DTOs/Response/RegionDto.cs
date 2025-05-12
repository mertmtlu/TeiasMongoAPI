namespace TeiasMongoAPI.Services.DTOs.Response
{
    public class RegionDto
    {
        public string Id { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public int RegionNumber { get; set; }  // Maps to Id in the domain model
        public List<string> Cities { get; set; } = new();
        public string Headquarters { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }

        // Navigation property
        public ClientDto? Client { get; set; }
    }
}