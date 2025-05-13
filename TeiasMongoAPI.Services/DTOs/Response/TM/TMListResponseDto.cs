namespace TeiasMongoAPI.Services.DTOs.Response.TM
{
    public class TMListResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public int TMId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RegionName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public List<int> Voltages { get; set; } = new();
        public string City { get; set; } = string.Empty;
        public int BuildingCount { get; set; }
    }
}