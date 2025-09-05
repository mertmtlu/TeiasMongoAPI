namespace TeiasMongoAPI.Services.DTOs.Response.Group
{
    public class GroupDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool IsActive { get; set; }
        public int MemberCount { get; set; }
        public List<GroupMemberDto> Members { get; set; } = new();
        public object Metadata { get; set; } = new object();
    }
}