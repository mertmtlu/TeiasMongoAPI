using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Services.DTOs.Request.Search
{
    public class TMSearchDto
    {
        public string? Name { get; set; }
        public string? RegionId { get; set; }
        public TMType? Type { get; set; }
        public TMState? State { get; set; }
        public List<int>? Voltages { get; set; }
        public string? City { get; set; }
        public string? County { get; set; }
        public int? MaxVoltage { get; set; }
        public DateOnly? ProvisionalAcceptanceDateFrom { get; set; }
        public DateOnly? ProvisionalAcceptanceDateTo { get; set; }
    }
}