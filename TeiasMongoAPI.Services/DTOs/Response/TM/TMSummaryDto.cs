namespace TeiasMongoAPI.Services.DTOs.Response.TM
{
    public class TMSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public int TMId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public int MaxVoltage { get; set; }
    }
}