﻿using MongoDB.Driver;
using System.Net;
using System.Text.Json;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.API.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var errorId = Guid.NewGuid().ToString();
            _logger.LogError(exception, $"An error occurred. Error ID: {errorId}");

            context.Response.ContentType = "application/json";

            var response = exception switch
            {
                UnauthorizedAccessException => CreateErrorResponse(HttpStatusCode.Unauthorized, "Unauthorized access", errorId),
                KeyNotFoundException => CreateErrorResponse(HttpStatusCode.NotFound, exception.Message, errorId),
                ArgumentNullException => CreateErrorResponse(HttpStatusCode.BadRequest, exception.Message, errorId),
                ArgumentException => CreateErrorResponse(HttpStatusCode.BadRequest, exception.Message, errorId),
                InvalidOperationException => CreateErrorResponse(HttpStatusCode.BadRequest, exception.Message, errorId),
                MongoException => CreateErrorResponse(HttpStatusCode.InternalServerError, "Database error occurred", errorId),
                TimeoutException => CreateErrorResponse(HttpStatusCode.RequestTimeout, "Request timeout", errorId),
                NotImplementedException => CreateErrorResponse(HttpStatusCode.NotImplemented, "Feature not implemented", errorId),
                _ => CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred while processing your request", errorId)
            };

            context.Response.StatusCode = (int)response.StatusCode;

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response.ErrorResponse, jsonOptions));
        }

        private (HttpStatusCode StatusCode, ErrorResponse ErrorResponse) CreateErrorResponse(HttpStatusCode statusCode, string message, string errorId)
        {
            var errorResponse = new ErrorResponse
            {
                Message = message,
                ErrorCode = statusCode.ToString(),
                TraceId = errorId,
                Details = new List<string>()
            };

            if (_env.IsDevelopment())
            {
                errorResponse.Details.Add($"Error ID: {errorId}");
            }

            return (statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Extension method to add the global exception middleware to the pipeline
    /// </summary>
    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}