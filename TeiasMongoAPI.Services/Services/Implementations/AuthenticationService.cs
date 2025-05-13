using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Auth;
using TeiasMongoAPI.Services.DTOs.Response.Auth;
using TeiasMongoAPI.Services.DTOs.Response.User;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;
using TeiasMongoAPI.Services.Security;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class AuthenticationService : BaseService, IAuthenticationService
    {
        private readonly IConfiguration _configuration;
        private readonly IUserService _userService;
        private readonly IPasswordHashingService _passwordHashingService;

        public AuthenticationService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IConfiguration configuration,
            IUserService userService,
            IPasswordHashingService passwordHashingService)
            : base(unitOfWork, mapper)
        {
            _configuration = configuration;
            _userService = userService;
            _passwordHashingService = passwordHashingService;
        }

        public async Task<AuthenticationResponseDto> LoginAsync(UserLoginDto dto, string? ipAddress = null, CancellationToken cancellationToken = default)
        {
            var user = await _unitOfWork.Users.GetByEmailOrUsernameAsync(dto.UsernameOrEmail, cancellationToken);

            if (user == null || !_passwordHashingService.VerifyPassword(dto.Password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Invalid username/email or password.");
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("User account is not active.");
            }

            if (!user.IsEmailVerified)
            {
                throw new UnauthorizedAccessException("Email not verified. Please verify your email first.");
            }

            // Generate tokens
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken(ipAddress);

            // Save refresh token
            await _unitOfWork.Users.UpdateRefreshTokenAsync(user._ID, refreshToken, cancellationToken);

            // Update last login
            await _unitOfWork.Users.UpdateLastLoginAsync(user._ID, DateTime.UtcNow, cancellationToken);

            return new AuthenticationResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresAt = refreshToken.Expires,
                TokenType = "Bearer",
                User = _mapper.Map<UserDto>(user)
            };
        }

        public async Task<TokenResponseDto> RefreshTokenAsync(RefreshTokenDto dto, string? ipAddress = null, CancellationToken cancellationToken = default)
        {
            var user = await _unitOfWork.Users.GetByRefreshTokenAsync(dto.RefreshToken, cancellationToken);

            if (user == null)
            {
                throw new UnauthorizedAccessException("Invalid token.");
            }

            var refreshToken = user.RefreshTokens.Single(x => x.Token == dto.RefreshToken);

            if (!refreshToken.IsActive)
            {
                throw new UnauthorizedAccessException("Invalid token.");
            }

            // Rotate refresh token
            var newRefreshToken = GenerateRefreshToken(ipAddress);
            refreshToken.Revoked = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;
            refreshToken.ReplacedByToken = newRefreshToken.Token;

            await _unitOfWork.Users.UpdateRefreshTokenAsync(user._ID, newRefreshToken, cancellationToken);

            // Generate new access token
            var accessToken = GenerateAccessToken(user);

            return new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken.Token,
                ExpiresAt = newRefreshToken.Expires,
                TokenType = "Bearer"
            };
        }

        public async Task<bool> LogoutAsync(string refreshToken, string? ipAddress = null, CancellationToken cancellationToken = default)
        {
            var user = await _unitOfWork.Users.GetByRefreshTokenAsync(refreshToken, cancellationToken);

            if (user == null)
            {
                return false;
            }

            var token = user.RefreshTokens.Single(x => x.Token == refreshToken);

            if (!token.IsActive)
            {
                return false;
            }

            // Revoke the token
            return await _unitOfWork.Users.RevokeRefreshTokenAsync(user._ID, refreshToken, ipAddress ?? "Unknown", cancellationToken);
        }

        public async Task<bool> RevokeTokenAsync(string token, string? ipAddress = null, CancellationToken cancellationToken = default)
        {
            var user = await _unitOfWork.Users.GetByRefreshTokenAsync(token, cancellationToken);

            if (user == null)
            {
                return false;
            }

            return await _unitOfWork.Users.RevokeRefreshTokenAsync(user._ID, token, ipAddress ?? "Unknown", cancellationToken);
        }

        public async Task<AuthenticationResponseDto> RegisterAsync(UserRegisterDto dto, CancellationToken cancellationToken = default)
        {
            // Use UserService to create the user
            var createdUserDto = await _userService.CreateAsync(dto, cancellationToken);

            // Get the full user object
            var user = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(createdUserDto.Id), cancellationToken);

            // Generate tokens
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken(null);

            // Save refresh token
            await _unitOfWork.Users.UpdateRefreshTokenAsync(user._ID, refreshToken, cancellationToken);

            return new AuthenticationResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresAt = refreshToken.Expires,
                TokenType = "Bearer",
                User = createdUserDto
            };
        }

        public async Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
        {
            var user = await _unitOfWork.Users.GetByEmailVerificationTokenAsync(token, cancellationToken);

            if (user == null)
            {
                throw new InvalidOperationException("Invalid verification token.");
            }

            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.ModifiedDate = DateTime.UtcNow;

            return await _unitOfWork.Users.UpdateAsync(user._ID, user, cancellationToken);
        }

        public async Task<bool> RequestPasswordResetAsync(UserPasswordResetRequestDto dto, CancellationToken cancellationToken = default)
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(dto.Email, cancellationToken);

            if (user == null)
            {
                // Don't reveal that the user doesn't exist
                return true;
            }

            var resetToken = GeneratePasswordResetToken();
            var expiryDate = DateTime.UtcNow.AddHours(1); // Token expires in 1 hour

            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpiry = expiryDate;
            user.ModifiedDate = DateTime.UtcNow;

            var result = await _unitOfWork.Users.UpdateAsync(user._ID, user, cancellationToken);

            // TODO: Send password reset email with the token

            return result;
        }

        public async Task<PasswordResetResponseDto> ResetPasswordAsync(UserPasswordResetDto dto, CancellationToken cancellationToken = default)
        {
            // Validate new password complexity
            if (!_passwordHashingService.IsPasswordComplex(dto.NewPassword))
            {
                return new PasswordResetResponseDto
                {
                    Success = false,
                    Message = PasswordRequirements.GetPasswordPolicy()
                };
            }

            var user = await _unitOfWork.Users.GetByPasswordResetTokenAsync(dto.ResetToken, cancellationToken);

            if (user == null)
            {
                return new PasswordResetResponseDto
                {
                    Success = false,
                    Message = "Invalid or expired reset token."
                };
            }

            user.PasswordHash = _passwordHashingService.HashPassword(dto.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            user.ModifiedDate = DateTime.UtcNow;

            // Revoke all refresh tokens for security
            await _unitOfWork.Users.RevokeAllUserRefreshTokensAsync(user._ID, "Password reset", cancellationToken);

            var success = await _unitOfWork.Users.UpdateAsync(user._ID, user, cancellationToken);

            return new PasswordResetResponseDto
            {
                Success = success,
                Message = success ? "Password reset successfully." : "Failed to reset password."
            };
        }

        public async Task<bool> ChangePasswordAsync(string userId, UserPasswordChangeDto dto, CancellationToken cancellationToken = default)
        {
            // Validate new password complexity
            if (!_passwordHashingService.IsPasswordComplex(dto.NewPassword))
            {
                throw new InvalidOperationException(PasswordRequirements.GetPasswordPolicy());
            }

            var objectId = ParseObjectId(userId);
            var user = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {userId} not found.");
            }

            // Verify current password
            if (!_passwordHashingService.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Current password is incorrect.");
            }

            user.PasswordHash = _passwordHashingService.HashPassword(dto.NewPassword);
            user.ModifiedDate = DateTime.UtcNow;

            return await _unitOfWork.Users.UpdateAsync(objectId, user, cancellationToken);
        }

        public async Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured"));

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return await Task.FromResult(true);
            }
            catch
            {
                return await Task.FromResult(false);
            }
        }

        private string GenerateAccessToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured"));

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user._ID.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email)
            };

            // Add roles as claims
            foreach (var role in user.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Add permissions as claims
            foreach (var permission in user.Permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:ExpiresInMinutes"] ?? "15")),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private RefreshToken GenerateRefreshToken(string? ipAddress)
        {
            var randomBytes = RandomNumberGenerator.GetBytes(64);
            return new RefreshToken
            {
                Token = Convert.ToBase64String(randomBytes),
                Expires = DateTime.UtcNow.AddDays(7),
                Created = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
        }

        private string GeneratePasswordResetToken()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(randomBytes);
        }
    }
}