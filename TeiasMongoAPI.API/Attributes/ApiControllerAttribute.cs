using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Diagnostics;
using System.Security.Claims;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.API.Attributes
{
    /// <summary>
    /// Custom API controller attribute that adds common behavior to all API controllers
    /// </summary>
    public class ApiControllerAttribute : ControllerAttribute, IApiBehaviorMetadata, IAsyncActionFilter
    {
        private const string TimingKey = "ActionTiming";
        private const string RequestIdKey = "RequestId";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // Before action execution
            OnActionExecuting(context);

            // Execute the action
            var executedContext = await next();

            // After action execution
            OnActionExecuted(executedContext);
        }

        private void OnActionExecuting(ActionExecutingContext context)
        {
            // 1. Generate and store request ID
            var requestId = context.HttpContext.Items[RequestIdKey]?.ToString() ?? Guid.NewGuid().ToString();
            context.HttpContext.Items[RequestIdKey] = requestId;
            context.HttpContext.Response.Headers["X-Request-Id"] = requestId;

            // 2. Start timing for performance monitoring
            var stopwatch = Stopwatch.StartNew();
            context.HttpContext.Items[TimingKey] = stopwatch;

            // 3. Validate API version (if needed)
            var apiVersion = context.HttpContext.Request.Headers["X-API-Version"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiVersion) && apiVersion != "1.0")
            {
                context.Result = new BadRequestObjectResult(ApiResponse<object>.ErrorResponse(
                    "Unsupported API version",
                    new List<string> { $"Version {apiVersion} is not supported. Use version 1.0" }));
                return;
            }

            // 4. Log action execution start
            var logger = context.HttpContext.RequestServices.GetService<ILogger<ApiControllerAttribute>>();
            var user = context.HttpContext.User;
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            var userName = user.FindFirst(ClaimTypes.Name)?.Value ?? "Anonymous";
            var action = $"{context.Controller.GetType().Name}.{context.ActionDescriptor.DisplayName}";

            logger?.LogInformation(
                "Action starting: {Action} | User: {UserId}:{UserName} | RequestId: {RequestId} | IP: {IP}",
                action, userId, userName, requestId,
                context.HttpContext.Connection.RemoteIpAddress?.ToString());

            // 5. Validate request size (prevent large payload attacks)
            var contentLength = context.HttpContext.Request.ContentLength;
            if (contentLength > 10 * 1024 * 1024) // 10MB limit
            {
                context.Result = new ObjectResult(ApiResponse<object>.ErrorResponse(
                    "Request too large",
                    new List<string> { "Request body exceeds maximum allowed size of 10MB" }))
                {
                    StatusCode = 413 // Payload Too Large
                };
                return;
            }

            // 6. Input sanitization for string parameters
            foreach (var argument in context.ActionArguments)
            {
                if (argument.Value is string stringValue)
                {
                    // Basic XSS prevention
                    context.ActionArguments[argument.Key] = stringValue
                        .Replace("<script>", "&lt;script&gt;")
                        .Replace("</script>", "&lt;/script&gt;")
                        .Trim();
                }
            }

            // 7. Add correlation ID for distributed tracing
            var correlationId = context.HttpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                               ?? Guid.NewGuid().ToString();
            context.HttpContext.Items["CorrelationId"] = correlationId;
            context.HttpContext.Response.Headers["X-Correlation-Id"] = correlationId;

            // 8. Check maintenance mode
            var configuration = context.HttpContext.RequestServices.GetService<IConfiguration>();
            var isMaintenanceMode = configuration?["MaintenanceMode"] == "true";
            if (isMaintenanceMode && !IsMaintenanceExempt(context))
            {
                context.Result = new ObjectResult(ApiResponse<object>.ErrorResponse(
                    "Service Unavailable",
                    new List<string> { "The system is currently under maintenance. Please try again later." }))
                {
                    StatusCode = 503 // Service Unavailable
                };
                return;
            }
        }

        private void OnActionExecuted(ActionExecutedContext context)
        {
            // 1. Calculate execution time
            var stopwatch = context.HttpContext.Items[TimingKey] as Stopwatch;
            stopwatch?.Stop();
            var executionTime = stopwatch?.ElapsedMilliseconds ?? 0;

            // 2. Add standard response headers
            context.HttpContext.Response.Headers["X-Response-Time"] = $"{executionTime}ms";
            context.HttpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.HttpContext.Response.Headers["X-Frame-Options"] = "DENY";
            context.HttpContext.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            context.HttpContext.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // 3. Add cache headers based on action
            AddCacheHeaders(context);

            // 4. Log action execution completed
            var logger = context.HttpContext.RequestServices.GetService<ILogger<ApiControllerAttribute>>();
            var requestId = context.HttpContext.Items[RequestIdKey]?.ToString();
            var action = $"{context.Controller.GetType().Name}.{context.ActionDescriptor.DisplayName}";
            var statusCode = (context.Result as ObjectResult)?.StatusCode
                           ?? (context.Result as StatusCodeResult)?.StatusCode
                           ?? 200;

            logger?.LogInformation(
                "Action completed: {Action} | RequestId: {RequestId} | Duration: {Duration}ms | StatusCode: {StatusCode}",
                action, requestId, executionTime, statusCode);

            // 5. Add performance warning for slow requests
            if (executionTime > 1000) // More than 1 second
            {
                logger?.LogWarning(
                    "Slow request detected: {Action} took {Duration}ms | RequestId: {RequestId}",
                    action, executionTime, requestId);
            }

            // 6. Track metrics (you could send these to Application Insights or similar)
            TrackMetrics(context, executionTime);

            // 7. Handle API deprecation warnings
            AddDeprecationWarnings(context);

            // 8. Audit sensitive operations
            AuditSensitiveOperations(context);
        }

        private void AddCacheHeaders(ActionExecutedContext context)
        {
            // Skip if response already has cache headers
            if (context.HttpContext.Response.Headers.ContainsKey("Cache-Control"))
                return;

            var method = context.HttpContext.Request.Method;
            var path = context.HttpContext.Request.Path.ToString().ToLower();

            // Cache GET requests for certain endpoints
            if (method == "GET")
            {
                if (path.Contains("/lookup") || path.Contains("/static"))
                {
                    // Cache for 1 hour
                    context.HttpContext.Response.Headers["Cache-Control"] = "public, max-age=3600";
                }
                else if (path.Contains("/list") || path.Contains("/search"))
                {
                    // Cache for 5 minutes
                    context.HttpContext.Response.Headers["Cache-Control"] = "public, max-age=300";
                }
                else
                {
                    // Default: no cache
                    context.HttpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                }
            }
            else
            {
                // Never cache non-GET requests
                context.HttpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            }
        }

        private void TrackMetrics(ActionExecutedContext context, long executionTime)
        {
            // This is where you'd send metrics to your monitoring system
            var logger = context.HttpContext.RequestServices.GetService<ILogger<ApiControllerAttribute>>();
            var action = $"{context.Controller.GetType().Name}.{context.ActionDescriptor.DisplayName}";
            var method = context.HttpContext.Request.Method;
            var statusCode = (context.Result as ObjectResult)?.StatusCode ?? 200;

            // Log as structured data for better analysis
            logger?.LogInformation("API_METRIC: {Action} {Method} {StatusCode} {Duration}ms",
                action, method, statusCode, executionTime);
        }

        private void AddDeprecationWarnings(ActionExecutedContext context)
        {
            // Check if the action or controller has deprecation attributes
            var actionDescriptor = context.ActionDescriptor;
            var isDeprecated = actionDescriptor.EndpointMetadata
                .Any(m => m.GetType().Name.Contains("Obsolete"));

            if (isDeprecated)
            {
                context.HttpContext.Response.Headers["X-API-Deprecation-Warning"] =
                    "This endpoint is deprecated and will be removed in a future version";
                context.HttpContext.Response.Headers["X-API-Deprecation-Date"] =
                    DateTime.UtcNow.AddMonths(6).ToString("yyyy-MM-dd");
            }
        }

        private void AuditSensitiveOperations(ActionExecutedContext context)
        {
            var method = context.HttpContext.Request.Method;
            var path = context.HttpContext.Request.Path.ToString().ToLower();
            var user = context.HttpContext.User;
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = user.FindFirst(ClaimTypes.Name)?.Value;

            // Audit sensitive operations
            var sensitiveOperations = new[]
            {
                ("DELETE", "/api/users"),
                ("PUT", "/api/users"),
                ("POST", "/api/clients"),
                ("DELETE", "/api/clients"),
                ("PUT", "/api/regions"),
                ("DELETE", "/api/regions")
            };

            var isSensitive = sensitiveOperations.Any(op =>
                method == op.Item1 && path.StartsWith(op.Item2));

            if (isSensitive)
            {
                var logger = context.HttpContext.RequestServices.GetService<ILogger<ApiControllerAttribute>>();
                var requestId = context.HttpContext.Items[RequestIdKey]?.ToString();
                var statusCode = (context.Result as ObjectResult)?.StatusCode ?? 200;

                logger?.LogWarning(
                    "AUDIT: Sensitive operation performed | User: {UserId}:{UserName} | " +
                    "Method: {Method} | Path: {Path} | RequestId: {RequestId} | Status: {StatusCode}",
                    userId, userName, method, path, requestId, statusCode);
            }
        }

        private bool IsMaintenanceExempt(ActionExecutingContext context)
        {
            // Allow health checks and admin endpoints during maintenance
            var path = context.HttpContext.Request.Path.ToString().ToLower();
            return path.Contains("/health") || path.Contains("/admin/maintenance");
        }
    }

    /// <summary>
    /// Custom filter for handling validation errors globally
    /// </summary>
    public class ValidationErrorFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToList()
                    );

                var response = new ValidationErrorResponse
                {
                    FieldErrors = errors
                };

                context.Result = new BadRequestObjectResult(response);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // No post-action processing needed for validation
        }
    }
}