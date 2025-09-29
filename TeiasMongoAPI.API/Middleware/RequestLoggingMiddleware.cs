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

            context.Response.Headers.Add("X-Request-Id", requestId);

            await LogRequest(context, requestId);

            // OnStarting registers a callback that executes just before response headers are sent.
            // This is the correct place to log response metadata without interfering with the body stream.
            context.Response.OnStarting(() =>
            {
                stopwatch.Stop();
                var elapsedMs = stopwatch.ElapsedMilliseconds;

                // We move the LogResponse logic here.
                // NOTE: We cannot read the response body here because it hasn't been written yet.
                // This is a necessary trade-off to support streaming file downloads.
                LogResponseHeaders(context, requestId, elapsedMs);

                return Task.CompletedTask;
            });

            await _next(context);
        }

        private async Task LogRequest(HttpContext context, string requestId)
        {
            var request = context.Request;
            var requestBody = string.Empty;

            // This request buffering logic is fine.
            if (request.ContentLength > 0)
            {
                request.EnableBuffering();
                using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                request.Body.Position = 0; // Rewind the stream so the controller can read it.
            }

            var logMessage = new StringBuilder();
            logMessage.AppendLine($"Request {requestId}:");
            logMessage.AppendLine($"  Method: {request.Method}");
            logMessage.AppendLine($"  Path: {request.Path}");
            logMessage.AppendLine($"  QueryString: {request.QueryString}");
            logMessage.AppendLine($"  Headers: {string.Join(", ", request.Headers.Select(h => $"{h.Key}={h.Value}"))}");
            logMessage.AppendLine($"  RemoteIP: {context.Connection.RemoteIpAddress}");

            if (!string.IsNullOrEmpty(requestBody) && requestBody.Length < 2000) // Increased limit slightly
            {
                logMessage.AppendLine($"  Body: {requestBody}");
            }

            _logger.LogInformation(logMessage.ToString());
        }

        // New method to only log headers and metadata, not the body.
        private void LogResponseHeaders(HttpContext context, string requestId, long elapsedMs)
        {
            var response = context.Response;

            var logMessage = new StringBuilder();
            logMessage.AppendLine($"Response {requestId}:");
            logMessage.AppendLine($"  StatusCode: {response.StatusCode}");
            logMessage.AppendLine($"  Duration: {elapsedMs}ms");
            logMessage.AppendLine($"  ContentType: {response.ContentType}");

            // Your check for file streams is still useful to decide if we should even *expect* a body in the logs.
            var contentType = response.ContentType?.ToLowerInvariant();
            var isFileStream = !string.IsNullOrEmpty(contentType) && (
                contentType.Contains("application/zip") ||
                contentType.Contains("application/octet-stream") ||
                contentType.Contains("application/pdf")
            );

            if (isFileStream)
            {
                // This message is accurate; we are skipping because it's a stream we can't buffer.
                logMessage.AppendLine($"  Body: Skipping response body logging for file stream.");
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

    // Your extension method remains the same
    public static class RequestLoggingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggingMiddleware>();
        }
    }
}