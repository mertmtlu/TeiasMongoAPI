using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using System.Security.Claims;
using System.Text.Json;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.API.Middleware
{
    /// <summary>
    /// Middleware that validates JWT token version against the user's current token version in the database.
    /// This ensures that tokens are immediately invalidated when user roles, permissions, or status change.
    /// </summary>
    public class TokenVersionValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenVersionValidationMiddleware> _logger;
        private readonly IMemoryCache _cache;
        private const string CacheKeyPrefix = "token_version_";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public TokenVersionValidationMiddleware(
            RequestDelegate next,
            ILogger<TokenVersionValidationMiddleware> logger,
            IMemoryCache cache)
        {
            _next = next;
            _logger = logger;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork)
        {
            // Skip validation if user is not authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                await _next(context);
                return;
            }

            try
            {
                // Extract user ID and token version from JWT claims
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var tokenVersionClaim = context.User.FindFirst("token_version")?.Value;

                // If claims are missing, proceed (backward compatibility for tokens without version)
                if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(tokenVersionClaim))
                {
                    await _next(context);
                    return;
                }

                if (!ObjectId.TryParse(userIdClaim, out var userId))
                {
                    // Log warning only once per claim per minute to prevent spam
                    var logKey = $"invalid_userid_logged_{userIdClaim?.GetHashCode()}";
                    if (!_cache.TryGetValue(logKey, out bool _))
                    {
                        _logger.LogWarning("Invalid user ID format in JWT: {UserId}", userIdClaim);
                        _cache.Set(logKey, true, TimeSpan.FromMinutes(1));
                    }
                    await RespondWithUnauthorized(context, "Invalid token format");
                    return;
                }

                if (!int.TryParse(tokenVersionClaim, out var tokenVersion))
                {
                    // Log warning only once per user per minute to prevent spam
                    var logKey = $"invalid_version_logged_{userId}";
                    if (!_cache.TryGetValue(logKey, out bool _))
                    {
                        _logger.LogWarning("Invalid token version format in JWT for user {UserId}", userIdClaim);
                        _cache.Set(logKey, true, TimeSpan.FromMinutes(1));
                    }
                    await RespondWithUnauthorized(context, "Invalid token format");
                    return;
                }

                // Check cache first for performance
                var cacheKey = $"{CacheKeyPrefix}{userId}";

                if (!_cache.TryGetValue(cacheKey, out int currentTokenVersion))
                {
                    // Cache miss - fetch from database with timeout protection
                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                        cts.CancelAfter(TimeSpan.FromSeconds(5)); // 5 second timeout for DB query

                        var user = await unitOfWork.Users.GetByIdAsync(userId, cts.Token);

                        if (user == null)
                        {
                            // Log warning only once per user per minute to prevent spam
                            var logKey = $"user_not_found_logged_{userId}";
                            if (!_cache.TryGetValue(logKey, out bool _))
                            {
                                _logger.LogWarning("User not found for ID {UserId} during token validation", userIdClaim);
                                _cache.Set(logKey, true, TimeSpan.FromMinutes(1));
                            }
                            await RespondWithUnauthorized(context, "User not found");
                            return;
                        }

                        // Check if user is active
                        if (!user.IsActive)
                        {
                            // Log warning only once per user per minute to prevent spam
                            var logKey = $"inactive_user_logged_{userId}";
                            if (!_cache.TryGetValue(logKey, out bool _))
                            {
                                _logger.LogWarning("Inactive user {UserId} attempted to access with valid token", userIdClaim);
                                _cache.Set(logKey, true, TimeSpan.FromMinutes(1));
                            }
                            await RespondWithUnauthorized(context, "User account is not active");
                            return;
                        }

                        currentTokenVersion = user.TokenVersion;

                        // Cache the token version for future requests
                        _cache.Set(cacheKey, currentTokenVersion, CacheDuration);
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout - fail open to avoid blocking requests
                        _logger.LogWarning("Token validation timeout for user {UserId}, allowing request", userIdClaim);
                        await _next(context);
                        return;
                    }
                    catch (Exception dbEx)
                    {
                        // Database error - fail open but log once per minute to avoid spam
                        var errorKey = "db_error_logged";
                        if (!_cache.TryGetValue(errorKey, out bool _))
                        {
                            _logger.LogError(dbEx, "Database error during token validation - failing open");
                            _cache.Set(errorKey, true, TimeSpan.FromMinutes(1));
                        }
                        await _next(context);
                        return;
                    }
                }

                // Validate token version
                if (tokenVersion != currentTokenVersion)
                {
                    // Log warning only once per user per minute to prevent spam
                    var logKey = $"token_version_mismatch_logged_{userId}";
                    if (!_cache.TryGetValue(logKey, out bool _))
                    {
                        _logger.LogWarning(
                            "Token version mismatch for user {UserId}. Token: {TokenVersion}, Current: {CurrentVersion}",
                            userIdClaim, tokenVersion, currentTokenVersion);
                        _cache.Set(logKey, true, TimeSpan.FromMinutes(1));
                    }

                    await RespondWithUnauthorized(context, "Token has been invalidated. Please login again.");
                    return;
                }

                // Token is valid, proceed to next middleware
                await _next(context);
            }
            catch (Exception ex)
            {
                // Unexpected error - log once per minute to avoid spam
                var errorKey = "unexpected_error_logged";
                if (!_cache.TryGetValue(errorKey, out bool _))
                {
                    _logger.LogError(ex, "Unexpected error during token version validation - failing open");
                    _cache.Set(errorKey, true, TimeSpan.FromMinutes(1));
                }

                // Fail open for resilience
                await _next(context);
            }
        }

        private async Task RespondWithUnauthorized(HttpContext context, string message)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.ErrorResponse(
                message,
                new List<string> { "Please login again to continue." });

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
        }

        /// <summary>
        /// Invalidates the cached token version for a specific user.
        /// Should be called when user's TokenVersion is incremented.
        /// </summary>
        public static void InvalidateCache(IMemoryCache cache, string userId)
        {
            var cacheKey = $"{CacheKeyPrefix}{userId}";
            cache.Remove(cacheKey);
        }
    }

    /// <summary>
    /// Extension method to add the token version validation middleware to the pipeline
    /// </summary>
    public static class TokenVersionValidationMiddlewareExtensions
    {
        public static IApplicationBuilder UseTokenVersionValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenVersionValidationMiddleware>();
        }
    }
}
