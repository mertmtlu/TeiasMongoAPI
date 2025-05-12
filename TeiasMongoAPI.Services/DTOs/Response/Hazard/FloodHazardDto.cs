namespace TeiasMongoAPI.Services.DTOs.Response.Hazard
{
    public class FloodHazardDto : BaseHazardDto
    {
        public string Incident { get; set; } = string.Empty;
        public string IncidentDescription { get; set; } = string.Empty;
        public string DrainageSystem { get; set; } = string.Empty;
        public string BasementFlooding { get; set; } = string.Empty;
        public string ExtremeEventCondition { get; set; } = string.Empty;
    }
}