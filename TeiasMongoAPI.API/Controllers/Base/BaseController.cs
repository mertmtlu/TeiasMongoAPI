using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.Security.Claims;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.API.Controllers.Base
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public abstract class BaseController : ControllerBase
    {
        protected readonly ILogger _logger;

        protected BaseController(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region User Context

        /// <summary>
        /// Gets the current user's ID from the JWT claims
        /// </summary>
        protected ObjectId? CurrentUserId
        {
            get
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return ObjectId.TryParse(userIdClaim, out var objectId) ? objectId : null;
            }
        }

        /// <summary>
        /// Gets the current user's username from the JWT claims
        /// </summary>
        protected string? CurrentUsername => User.FindFirst(ClaimTypes.Name)?.Value;

        /// <summary>
        /// Gets the current user's email from the JWT claims
        /// </summary>
        protected string? CurrentUserEmail => User.FindFirst(ClaimTypes.Email)?.Value;

        /// <summary>
        /// Gets the current user's roles
        /// </summary>
        protected IEnumerable<string> CurrentUserRoles => User.FindAll(ClaimTypes.Role).Select(c => c.Value);

        /// <summary>
        /// Gets the current user's permissions
        /// </summary>
        protected IEnumerable<string> CurrentUserPermissions => User.FindAll("permission").Select(c => c.Value);

        /// <summary>
        /// Checks if the current user has a specific role
        /// </summary>
        protected bool IsInRole(string role) => User.IsInRole(role);

        /// <summary>
        /// Checks if the current user has a specific permission
        /// </summary>
        protected bool HasPermission(string permission) => CurrentUserPermissions.Contains(permission);

        #endregion

        #region Response Helpers

        /// <summary>
        /// Returns a successful response with data
        /// </summary>
        protected ActionResult<ApiResponse<T>> Success<T>(T data, string message = "Success")
        {
            _logger.LogInformation($"Successful response: {message}");
            return Ok(ApiResponse<T>.SuccessResponse(data, message));
        }

        /// <summary>
        /// Returns a paginated successful response
        /// </summary>
        protected ActionResult<ApiResponse<PagedResponse<T>>> SuccessPaged<T>(PagedResponse<T> data, string message = "Success")
        {
            _logger.LogInformation($"Successful paginated response: {message}");
            return Ok(ApiResponse<PagedResponse<T>>.SuccessResponse(data, message));
        }

        /// <summary>
        /// Returns a created response (201)
        /// </summary>
        protected ActionResult<ApiResponse<T>> Created<T>(T data, string location, string message = "Created successfully")
        {
            _logger.LogInformation($"Resource created: {message}");
            var response = ApiResponse<T>.SuccessResponse(data, message);
            return base.Created(location, response);
        }

        /// <summary>
        /// Returns a no content response (204)
        /// </summary>
        protected ActionResult NoContentResult(string message = "Operation completed successfully")
        {
            _logger.LogInformation($"No content response: {message}");
            return NoContent();
        }

        /// <summary>
        /// Returns an error response
        /// </summary>
        protected ActionResult<ApiResponse<T>> Error<T>(string message, List<string>? errors = null, int statusCode = 400)
        {
            _logger.LogError($"Error response: {message}. Errors: {string.Join(", ", errors ?? new List<string>())}");
            var response = ApiResponse<T>.ErrorResponse(message, errors);
            return StatusCode(statusCode, response);
        }

        /// <summary>
        /// Returns a not found response (404)
        /// </summary>
        protected ActionResult<ApiResponse<T>> NotFound<T>(string message = "Resource not found")
        {
            _logger.LogWarning($"Not found: {message}");
            return Error<T>(message, null, 404);
        }

        /// <summary>
        /// Returns an unauthorized response (401)
        /// </summary>
        protected ActionResult<ApiResponse<T>> Unauthorized<T>(string message = "Unauthorized access")
        {
            _logger.LogWarning($"Unauthorized: {message}");
            return Error<T>(message, null, 401);
        }

        /// <summary>
        /// Returns a forbidden response (403)
        /// </summary>
        protected ActionResult<ApiResponse<T>> Forbidden<T>(string message = "Access denied")
        {
            _logger.LogWarning($"Forbidden: {message}");
            return Error<T>(message, null, 403);
        }

        /// <summary>
        /// Returns a conflict response (409)
        /// </summary>
        protected ActionResult<ApiResponse<T>> Conflict<T>(string message = "Resource conflict")
        {
            _logger.LogWarning($"Conflict: {message}");
            return Error<T>(message, null, 409);
        }

        /// <summary>
        /// Returns a validation error response (422)
        /// </summary>
        protected ActionResult<ApiResponse<T>> ValidationError<T>(string message = "Validation failed", List<string>? errors = null)
        {
            _logger.LogWarning($"Validation error: {message}");
            return Error<T>(message, errors, 422);
        }

        #endregion

        #region Validation Helpers

        /// <summary>
        /// Validates the model state and returns appropriate response
        /// </summary>
        protected ActionResult<ApiResponse<T>>? ValidateModelState<T>()
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return ValidationError<T>("Invalid input data", errors);
            }
            return null;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Parses and validates an ObjectId from string
        /// </summary>
        protected ActionResult<ObjectId>? ParseObjectId(string id, string parameterName = "id")
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                _logger.LogWarning($"Invalid ObjectId format: {id} for parameter: {parameterName}");
                return BadRequest(ApiResponse<object>.ErrorResponse($"Invalid {parameterName} format", new List<string> { $"'{id}' is not a valid ObjectId" }));
            }
            return objectId;
        }

        /// <summary>
        /// Gets the client IP address
        /// </summary>
        protected string? GetClientIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        /// <summary>
        /// Gets request headers
        /// </summary>
        protected string? GetRequestHeader(string headerName)
        {
            return HttpContext.Request.Headers[headerName].FirstOrDefault();
        }

        #endregion

        #region Exception Handling

        /// <summary>
        /// Handles exceptions and returns appropriate responses
        /// </summary>
        protected ActionResult<ApiResponse<T>> HandleException<T>(Exception ex, string operation)
        {
            var errorId = Guid.NewGuid().ToString();
            _logger.LogError(ex, $"Error during {operation}. ErrorId: {errorId}");

            var message = ex switch
            {
                UnauthorizedAccessException => "Unauthorized access",
                KeyNotFoundException => ex.Message,
                InvalidOperationException => ex.Message,
                ArgumentException => ex.Message,
                _ => "An error occurred while processing your request"
            };

            var statusCode = ex switch
            {
                UnauthorizedAccessException => 401,
                KeyNotFoundException => 404,
                InvalidOperationException => 400,
                ArgumentException => 400,
                _ => 500
            };

            return Error<T>(message, new List<string> { $"Error ID: {errorId}" }, statusCode);
        }

        #endregion

        #region Async Helpers

        /// <summary>
        /// Executes an async operation with error handling
        /// </summary>
        protected async Task<ActionResult<ApiResponse<T>>> ExecuteAsync<T>(Func<Task<T>> operation, string operationName)
        {
            try
            {
                _logger.LogInformation($"Executing operation: {operationName}");
                var result = await operation();
                return Success(result);
            }
            catch (Exception ex)
            {
                return HandleException<T>(ex, operationName);
            }
        }

        /// <summary>
        /// Executes an async operation that returns ActionResult with error handling
        /// </summary>
        protected async Task<ActionResult<ApiResponse<T>>> ExecuteActionAsync<T>(Func<Task<ActionResult<ApiResponse<T>>>> operation, string operationName)
        {
            try
            {
                _logger.LogInformation($"Executing operation: {operationName}");
                return await operation();
            }
            catch (Exception ex)
            {
                return HandleException<T>(ex, operationName);
            }
        }

        #endregion
    }
}