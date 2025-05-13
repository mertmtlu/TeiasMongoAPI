namespace TeiasMongoAPI.Services.DTOs.Response.Region
{
    public class RegionResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public int RegionId { get; set; } // The numeric ID (previously "Id" in the model)
        public List<string> Cities { get; set; } = new();
        public string Headquarters { get; set; } = string.Empty;
    }
}