namespace TeiasMongoAPI.Services.DTOs.Response.Common
{
    public class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public List<string> Details { get; set; } = new();
        public string ErrorCode { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string TraceId { get; set; } = string.Empty;
    }
}