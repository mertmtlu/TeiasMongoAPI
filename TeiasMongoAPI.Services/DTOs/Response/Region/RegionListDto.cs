namespace TeiasMongoAPI.Services.DTOs.Response.Region
{
    public class RegionListDto
    {
        public string Id { get; set; } = string.Empty;
        public int RegionId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string Headquarters { get; set; } = string.Empty;
        public List<string> Cities { get; set; } = new();
        public int TMCount { get; set; }
        public int ActiveTMCount { get; set; }
    }
}