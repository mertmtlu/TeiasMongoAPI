using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using System.Linq;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.API.Attributes
{
    /// <summary>
    /// Rate limiting attribute to prevent API abuse
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class RateLimitAttribute : ActionFilterAttribute
    {
        private readonly int _requestsPerMinute;
        private readonly string _identifier;

        public RateLimitAttribute(int requestsPerMinute = 60, string identifier = "default")
        {
            _requestsPerMinute = requestsPerMinute;
            _identifier = identifier;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var cache = context.HttpContext.RequestServices.GetService<IMemoryCache>();
            if (cache == null)
            {
                await next();
                return;
            }

            var key = GenerateKey(context);
            var cacheKey = $"rate_limit_{key}_{_identifier}";
            var requestCount = 0;

            if (cache.TryGetValue(cacheKey, out int count))
            {
                requestCount = count;
            }

            if (requestCount >= _requestsPerMinute)
            {
                context.Result = new ObjectResult(ApiResponse<object>.ErrorResponse(
                    "Rate limit exceeded",
                    new List<string> { $"Maximum {_requestsPerMinute} requests per minute exceeded" }))
                {
                    StatusCode = 429 // Too Many Requests
                };

                // Add rate limit headers
                context.HttpContext.Response.Headers["X-RateLimit-Limit"] = _requestsPerMinute.ToString();
                context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = "0";
                context.HttpContext.Response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString();

                return;
            }

            // Increment counter
            requestCount++;
            cache.Set(cacheKey, requestCount, TimeSpan.FromMinutes(1));

            // Add rate limit headers
            context.HttpContext.Response.Headers["X-RateLimit-Limit"] = _requestsPerMinute.ToString();
            context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = (_requestsPerMinute - requestCount).ToString();
            context.HttpContext.Response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString();

            await next();
        }

        private string GenerateKey(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;

            // If authenticated, use user ID
            if (user.Identity?.IsAuthenticated == true)
            {
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return $"user_{userId}";
            }

            // Otherwise, use IP address
            var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return $"ip_{ipAddress}";
        }
    }

    /// <summary>
    /// API key authentication attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class ApiKeyAuthAttribute : ActionFilterAttribute
    {
        private const string ApiKeyHeaderName = "X-API-Key";

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
            {
                context.Result = new UnauthorizedObjectResult(ApiResponse<object>.ErrorResponse(
                    "API Key required",
                    new List<string> { $"Missing {ApiKeyHeaderName} header" }));
                return;
            }

            var configuration = context.HttpContext.RequestServices.GetService<IConfiguration>();
            var validApiKeys = configuration?.GetSection("ApiKeys").Get<string[]>() ?? Array.Empty<string>();

            if (!validApiKeys.Contains(extractedApiKey.ToString()))
            {
                context.Result = new UnauthorizedObjectResult(ApiResponse<object>.ErrorResponse(
                    "Invalid API Key",
                    new List<string> { "The provided API key is invalid" }));
                return;
            }

            await next();
        }
    }

    /// <summary>
    /// Request timing attribute for performance monitoring
    /// </summary>
    public class TimingAttribute : ActionFilterAttribute
    {
        private readonly string _metricName;
        private readonly int _warningThresholdMs;

        public TimingAttribute(string metricName, int warningThresholdMs = 1000)
        {
            _metricName = metricName;
            _warningThresholdMs = warningThresholdMs;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var result = await next();

            stopwatch.Stop();
            var executionTime = stopwatch.ElapsedMilliseconds;

            // Add timing header
            context.HttpContext.Response.Headers[$"X-{_metricName}-Time"] = $"{executionTime}ms";

            // Log warning if threshold exceeded
            if (executionTime > _warningThresholdMs)
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<TimingAttribute>>();
                logger?.LogWarning(
                    "Slow operation detected: {MetricName} took {Duration}ms (threshold: {Threshold}ms)",
                    _metricName, executionTime, _warningThresholdMs);
            }
        }
    }

    /// <summary>
    /// Audit logging attribute for sensitive operations
    /// </summary>
    public class AuditLogAttribute : ActionFilterAttribute
    {
        private readonly string _operationType;

        public AuditLogAttribute(string operationType)
        {
            _operationType = operationType;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            var userName = user.FindFirst(ClaimTypes.Name)?.Value ?? "Anonymous";
            var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var requestId = context.HttpContext.Items["RequestId"]?.ToString() ?? Guid.NewGuid().ToString();

            var logger = context.HttpContext.RequestServices.GetService<ILogger<AuditLogAttribute>>();

            // Log operation start
            logger?.LogInformation(
                "AUDIT_START: Operation: {Operation} | User: {UserId}:{UserName} | IP: {IP} | RequestId: {RequestId}",
                _operationType, userId, userName, ipAddress, requestId);

            var result = await next();

            // Log operation result
            var statusCode = (result.Result as ObjectResult)?.StatusCode ?? 200;
            var isSuccess = statusCode >= 200 && statusCode < 300;

            if (isSuccess)
            {
                logger?.LogInformation(
                    "AUDIT_SUCCESS: Operation: {Operation} | User: {UserId}:{UserName} | IP: {IP} | RequestId: {RequestId} | Status: {StatusCode}",
                    _operationType, userId, userName, ipAddress, requestId, statusCode);
            }
            else
            {
                logger?.LogWarning(
                    "AUDIT_FAILURE: Operation: {Operation} | User: {UserId}:{UserName} | IP: {IP} | RequestId: {RequestId} | Status: {StatusCode}",
                    _operationType, userId, userName, ipAddress, requestId, statusCode);
            }
        }
    }
}