namespace TeiasMongoAPI.Services.DTOs.Response.Client
{
    public class ClientListDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int RegionCount { get; set; }
        public int TotalTMCount { get; set; }
    }
}