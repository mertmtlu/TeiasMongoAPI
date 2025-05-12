using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.DTOs.Response.TM
{
    public class TMDto
    {
        public string Id { get; set; } = string.Empty;
        public string RegionId { get; set; } = string.Empty;
        public int TMId { get; set; } // The numeric ID
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Default" or "GIS"
        public string State { get; set; } = string.Empty; // "Active" or "Inactive"
        public List<int> Voltages { get; set; } = new();
        public int MaxVoltage { get; set; }
        public DateTime ProvisionalAcceptanceDate { get; set; }
        public LocationDto Location { get; set; } = null!;
        public AddressDto Address { get; set; } = null!;
    }
}