namespace TeiasMongoAPI.Services.DTOs.Response.Building
{
    public class BuildingResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string TmId { get; set; } = string.Empty;
        public int BuildingTMID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Control", "Security", "Switchyard"
        public bool InScopeOfMETU { get; set; }
        public string ReportName { get; set; } = string.Empty;
        public int Code { get; set; } // Computed based on type
        public int BKS { get; set; } // Computed based on type
    }
}