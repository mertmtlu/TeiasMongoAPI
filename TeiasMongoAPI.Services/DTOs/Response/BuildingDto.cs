namespace TeiasMongoAPI.Services.DTOs.Response
{
    public class BuildingDto
    {
        public string Id { get; set; } = string.Empty;
        public string TmId { get; set; } = string.Empty;
        public int BuildingTMID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Control, Security, Switchyard
        public bool InScopeOfMETU { get; set; }
        public List<BlockDto> Blocks { get; set; } = new();
        public string ReportName { get; set; } = string.Empty;

        // Calculated properties
        public int Code { get; set; }
        public int BKS { get; set; }

        public DateTime CreatedDate { get; set; }

        // Navigation property
        public TMDto? TM { get; set; }
    }
}