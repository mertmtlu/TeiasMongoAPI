namespace TeiasMongoAPI.Services.DTOs.Response.Building
{
    public class BuildingSummaryResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public int BuildingTMID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int BlockCount { get; set; }
    }
}