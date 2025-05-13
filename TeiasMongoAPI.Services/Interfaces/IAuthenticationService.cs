using TeiasMongoAPI.Services.DTOs.Request.Auth;
using TeiasMongoAPI.Services.DTOs.Response.Auth;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IAuthenticationService
    {
        Task<AuthenticationResponseDto> LoginAsync(UserLoginDto dto, string? ipAddress = null, CancellationToken cancellationToken = default);
        Task<TokenResponseDto> RefreshTokenAsync(RefreshTokenDto dto, string? ipAddress = null, CancellationToken cancellationToken = default);
        Task<bool> LogoutAsync(string refreshToken, string? ipAddress = null, CancellationToken cancellationToken = default);
        Task<bool> RevokeTokenAsync(string token, string? ipAddress = null, CancellationToken cancellationToken = default);
        Task<AuthenticationResponseDto> RegisterAsync(UserRegisterDto dto, CancellationToken cancellationToken = default);
        Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);
        Task<bool> RequestPasswordResetAsync(UserPasswordResetRequestDto dto, CancellationToken cancellationToken = default);
        Task<PasswordResetResponseDto> ResetPasswordAsync(UserPasswordResetDto dto, CancellationToken cancellationToken = default);
        Task<bool> ChangePasswordAsync(string userId, UserPasswordChangeDto dto, CancellationToken cancellationToken = default);
        Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    }
}