namespace TeiasMongoAPI.Services.DTOs.Response.Region
{
    public class RegionSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public int RegionId { get; set; }
        public string Headquarters { get; set; } = string.Empty;
        public int CityCount { get; set; }
    }
}