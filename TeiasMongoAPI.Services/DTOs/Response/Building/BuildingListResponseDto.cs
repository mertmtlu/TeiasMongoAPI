namespace TeiasMongoAPI.Services.DTOs.Response.Building
{
    public class BuildingListResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string TmName { get; set; } = string.Empty;
        public int BuildingTMID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool InScopeOfMETU { get; set; }
        public int BlockCount { get; set; }
        public string ReportName { get; set; } = string.Empty;
    }
}