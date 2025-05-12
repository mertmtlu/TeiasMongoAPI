namespace TeiasMongoAPI.Services.DTOs.Response.Auth
{
    public class PasswordResetResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}