namespace TeiasMongoAPI.Services.DTOs.Request.Search
{
    public class RegionSearchDto
    {
        public string? ClientId { get; set; }
        public int? Id { get; set; }
        public string? City { get; set; }
        public string? Headquarters { get; set; }
    }
}