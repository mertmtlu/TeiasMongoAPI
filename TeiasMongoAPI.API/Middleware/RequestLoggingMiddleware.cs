using System.Diagnostics;
using System.Text;

namespace TeiasMongoAPI.API.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString();

            // Add request ID to response headers
            context.Response.Headers.Add("X-Request-Id", requestId);

            // Log request
            await LogRequest(context, requestId);

            // Copy original response body stream
            var originalBodyStream = context.Response.Body;

            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await _next(context);
            }
            finally
            {
                // Log response
                await LogResponse(context, requestId, stopwatch.ElapsedMilliseconds, responseBody);

                // Copy the contents of the new memory stream to the original stream
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }

        private async Task LogRequest(HttpContext context, string requestId)
        {
            var request = context.Request;
            var requestBody = string.Empty;

            if (request.ContentLength > 0 && request.Body.CanSeek)
            {
                request.EnableBuffering();

                using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                request.Body.Position = 0;
            }

            var logMessage = new StringBuilder();
            logMessage.AppendLine($"Request {requestId}:");
            logMessage.AppendLine($"  Method: {request.Method}");
            logMessage.AppendLine($"  Path: {request.Path}");
            logMessage.AppendLine($"  QueryString: {request.QueryString}");
            logMessage.AppendLine($"  Headers: {string.Join(", ", request.Headers.Select(h => $"{h.Key}={h.Value}"))}");
            logMessage.AppendLine($"  RemoteIP: {context.Connection.RemoteIpAddress}");

            if (!string.IsNullOrEmpty(requestBody) && requestBody.Length < 1000) // Only log small bodies
            {
                logMessage.AppendLine($"  Body: {requestBody}");
            }

            _logger.LogInformation(logMessage.ToString());
        }

        private async Task LogResponse(HttpContext context, string requestId, long elapsedMs, MemoryStream responseBody)
        {
            var response = context.Response;
            responseBody.Seek(0, SeekOrigin.Begin);
            var responseText = await new StreamReader(responseBody).ReadToEndAsync();
            responseBody.Seek(0, SeekOrigin.Begin);

            var logMessage = new StringBuilder();
            logMessage.AppendLine($"Response {requestId}:");
            logMessage.AppendLine($"  StatusCode: {response.StatusCode}");
            logMessage.AppendLine($"  Duration: {elapsedMs}ms");

            if (responseText.Length < 1000) // Only log small bodies
            {
                logMessage.AppendLine($"  Body: {responseText}");
            }

            if (response.StatusCode >= 400)
            {
                _logger.LogError(logMessage.ToString());
            }
            else
            {
                _logger.LogInformation(logMessage.ToString());
            }
        }
    }

    /// <summary>
    /// Extension method to add the request logging middleware to the pipeline
    /// </summary>
    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}