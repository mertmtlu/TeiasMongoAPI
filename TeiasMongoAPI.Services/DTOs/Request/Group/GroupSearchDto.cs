namespace TeiasMongoAPI.Services.DTOs.Request.Group
{
    public class GroupSearchDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? CreatedBy { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
        public bool? HasMembers { get; set; }
        public int? MinMemberCount { get; set; }
        public int? MaxMemberCount { get; set; }
    }
}