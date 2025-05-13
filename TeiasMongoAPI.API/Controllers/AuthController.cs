using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Auth;
using TeiasMongoAPI.Services.DTOs.Response.Auth;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    public class AuthController : BaseController
    {
        private readonly IAuthenticationService _authenticationService;

        public AuthController(
            IAuthenticationService authenticationService,
            ILogger<AuthController> logger)
            : base(logger)
        {
            _authenticationService = authenticationService;
        }

        /// <summary>
        /// Authenticate user and return JWT token
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [RateLimit(requestsPerMinute: 5, identifier: "login")]
        [AuditLog("UserLogin")]
        public async Task<ActionResult<ApiResponse<AuthenticationResponseDto>>> Login(
            [FromBody] UserLoginDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<AuthenticationResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                var ipAddress = GetClientIpAddress();
                var result = await _authenticationService.LoginAsync(dto, ipAddress, cancellationToken);

                // Set refresh token in HTTP-only cookie for better security
                SetRefreshTokenCookie(result.RefreshToken);

                return result;
            }, "User login");
        }

        /// <summary>
        /// Register a new user
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [RateLimit(requestsPerMinute: 3, identifier: "register")]
        [AuditLog("UserRegistration")]
        public async Task<ActionResult<ApiResponse<AuthenticationResponseDto>>> Register(
            [FromBody] UserRegisterDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<AuthenticationResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                var result = await _authenticationService.RegisterAsync(dto, cancellationToken);

                // Set refresh token in HTTP-only cookie
                SetRefreshTokenCookie(result.RefreshToken);

                return result;
            }, "User registration");
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        [RateLimit(requestsPerMinute: 10, identifier: "refresh")]
        public async Task<ActionResult<ApiResponse<TokenResponseDto>>> RefreshToken(
            [FromBody] RefreshTokenDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<TokenResponseDto>();
            if (validationResult != null) return validationResult;

            // Try to get refresh token from cookie if not provided in body
            if (string.IsNullOrEmpty(dto.RefreshToken))
            {
                dto.RefreshToken = Request.Cookies["RefreshToken"] ?? string.Empty;
            }

            return await ExecuteAsync(async () =>
            {
                var ipAddress = GetClientIpAddress();
                var result = await _authenticationService.RefreshTokenAsync(dto, ipAddress, cancellationToken);

                // Update refresh token cookie
                SetRefreshTokenCookie(result.RefreshToken);

                return result;
            }, "Token refresh");
        }

        /// <summary>
        /// Logout user and revoke refresh token
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        [AuditLog("UserLogout")]
        public async Task<ActionResult<ApiResponse<bool>>> Logout(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                var refreshToken = Request.Cookies["RefreshToken"] ?? string.Empty;
                if (string.IsNullOrEmpty(refreshToken))
                {
                    // Try to get from Authorization header
                    var authHeader = Request.Headers["Authorization"].ToString();
                    if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                    {
                        refreshToken = authHeader.Substring(7);
                    }
                }

                if (string.IsNullOrEmpty(refreshToken))
                {
                    throw new InvalidOperationException("No refresh token found");
                }

                var ipAddress = GetClientIpAddress();
                var result = await _authenticationService.LogoutAsync(refreshToken, ipAddress, cancellationToken);

                // Clear refresh token cookie
                ClearRefreshTokenCookie();

                return result;
            }, "User logout");
        }

        /// <summary>
        /// Request password reset token
        /// </summary>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        [RateLimit(requestsPerMinute: 3, identifier: "forgot-password")]
        public async Task<ActionResult<ApiResponse<bool>>> ForgotPassword(
            [FromBody] UserPasswordResetRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                var result = await _authenticationService.RequestPasswordResetAsync(dto, cancellationToken);
                // Always return success for security (don't reveal if email exists)
                return true;
            }, "Password reset request");
        }

        /// <summary>
        /// Reset password using token
        /// </summary>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        [RateLimit(requestsPerMinute: 3, identifier: "reset-password")]
        [AuditLog("PasswordReset")]
        public async Task<ActionResult<ApiResponse<PasswordResetResponseDto>>> ResetPassword(
            [FromBody] UserPasswordResetDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<PasswordResetResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _authenticationService.ResetPasswordAsync(dto, cancellationToken);
            }, "Password reset");
        }

        /// <summary>
        /// Verify email address using token
        /// </summary>
        [HttpPost("verify-email")]
        [AllowAnonymous]
        [RateLimit(requestsPerMinute: 5, identifier: "verify-email")]
        public async Task<ActionResult<ApiResponse<bool>>> VerifyEmail(
            [FromQuery] string token,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(token))
            {
                return ValidationError<bool>("Invalid verification token");
            }

            return await ExecuteAsync(async () =>
            {
                return await _authenticationService.VerifyEmailAsync(token, cancellationToken);
            }, "Email verification");
        }

        /// <summary>
        /// Change password for authenticated user
        /// </summary>
        [HttpPost("change-password")]
        [Authorize]
        [AuditLog("PasswordChange")]
        public async Task<ActionResult<ApiResponse<bool>>> ChangePassword(
            [FromBody] UserPasswordChangeDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                var userId = CurrentUserId?.ToString();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                return await _authenticationService.ChangePasswordAsync(userId, dto, cancellationToken);
            }, "Password change");
        }

        /// <summary>
        /// Validate JWT token
        /// </summary>
        [HttpGet("validate-token")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<bool>>> ValidateToken(
            [FromQuery] string token,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(token))
            {
                return ValidationError<bool>("Token is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await _authenticationService.ValidateTokenAsync(token, cancellationToken);
            }, "Token validation");
        }

        /// <summary>
        /// Revoke a specific refresh token
        /// </summary>
        [HttpPost("revoke-token")]
        [Authorize]
        [RequireRole(UserRoles.Admin)]
        [AuditLog("TokenRevocation")]
        public async Task<ActionResult<ApiResponse<bool>>> RevokeToken(
            [FromBody] RevokeTokenDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                var ipAddress = GetClientIpAddress();
                return await _authenticationService.RevokeTokenAsync(dto.Token, ipAddress, cancellationToken);
            }, "Token revocation");
        }

        #region Private Methods

        private void SetRefreshTokenCookie(string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Use HTTPS in production
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7) // Match your token expiration
            };

            Response.Cookies.Append("RefreshToken", refreshToken, cookieOptions);
        }

        private void ClearRefreshTokenCookie()
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(-1)
            };

            Response.Cookies.Append("RefreshToken", string.Empty, cookieOptions);
        }

        #endregion
    }

    // Additional DTO for token revocation
    public class RevokeTokenDto
    {
        public required string Token { get; set; }
    }
}