namespace TeiasMongoAPI.Services.DTOs.Response.Common
{
    public class AuditInfoDto
    {
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}