using TeiasMongoAPI.Services.DTOs.Response.User;

namespace TeiasMongoAPI.Services.DTOs.Response.Auth
{
    public class AuthenticationResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public UserDto User { get; set; } = null!;
    }
}