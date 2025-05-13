namespace TeiasMongoAPI.Services.DTOs.Response.Hazard
{
    public abstract class BaseHazardResponseDto
    {
        public double Score { get; set; }
        public string Level { get; set; } = string.Empty; // String representation of Level enum
        public Dictionary<string, int> EliminationCosts { get; set; } = new();
        public bool PreviousIncidentOccurred { get; set; }
        public string PreviousIncidentDescription { get; set; } = string.Empty;
        public double DistanceToInventory { get; set; }
    }
}