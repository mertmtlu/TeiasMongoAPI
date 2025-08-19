using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.RemoteApp;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.RemoteApp;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    /// <summary>
    /// Remote applications management controller - handles remote website connections
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RemoteAppsController : BaseController
    {
        private readonly IRemoteAppService _remoteAppService;

        public RemoteAppsController(
            IRemoteAppService remoteAppService,
            ILogger<RemoteAppsController> logger)
            : base(logger)
        {
            _remoteAppService = remoteAppService;
        }

        #region Basic CRUD Operations

        /// <summary>
        /// Get all remote apps with pagination
        /// </summary>
        [HttpGet]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RemoteAppListDto>>>> GetAll(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.GetAllAsync(pagination, cancellationToken);
            }, "Get all remote apps");
        }

        /// <summary>
        /// Get remote app by ID with full details
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<RemoteAppDetailDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.GetByIdAsync(id, cancellationToken);
            }, $"Get remote app {id}");
        }

        /// <summary>
        /// Create new remote app
        /// </summary>
        [HttpPost]
        [RequirePermission(UserPermissions.CreatePrograms)]
        [AuditLog("CreateRemoteApp")]
        public async Task<ActionResult<ApiResponse<RemoteAppDto>>> Create(
            [FromBody] RemoteAppCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            var validationResult = ValidateModelState<RemoteAppDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                var creatorId = CurrentUserId?.ToString();
                if (string.IsNullOrEmpty(creatorId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                return await _remoteAppService.CreateAsync(dto, creatorId, cancellationToken);
            }, "Create remote app");
        }

        /// <summary>
        /// Update remote app
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("UpdateRemoteApp")]
        public async Task<ActionResult<ApiResponse<RemoteAppDto>>> Update(
            string id,
            [FromBody] RemoteAppUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var validationResult = ValidateModelState<RemoteAppDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.UpdateAsync(id, dto, cancellationToken);
            }, $"Update remote app {id}");
        }

        /// <summary>
        /// Delete remote app
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.DeletePrograms)]
        [AuditLog("DeleteRemoteApp")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.DeleteAsync(id, cancellationToken);
            }, $"Delete remote app {id}");
        }

        #endregion

        #region Discovery and Search

        /// <summary>
        /// Get remote apps by creator
        /// </summary>
        [HttpGet("by-creator/{creatorId}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RemoteAppListDto>>>> GetByCreator(
            string creatorId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(creatorId, "creatorId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.GetByCreatorAsync(creatorId, pagination, cancellationToken);
            }, $"Get remote apps by creator {creatorId}");
        }

        /// <summary>
        /// Get remote apps by status (active, inactive, etc.)
        /// </summary>
        [HttpGet("by-status/{status}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RemoteAppListDto>>>> GetByStatus(
            string status,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.GetByStatusAsync(status, pagination, cancellationToken);
            }, $"Get remote apps by status {status}");
        }

        /// <summary>
        /// Get remote apps accessible to current user (public apps + assigned private apps)
        /// </summary>
        [HttpGet("user-accessible")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RemoteAppListDto>>>> GetUserAccessibleApps(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                var userId = CurrentUserId?.ToString();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                return await _remoteAppService.GetUserAccessibleAppsAsync(userId, pagination, cancellationToken);
            }, "Get user accessible remote apps");
        }

        /// <summary>
        /// Get public remote apps
        /// </summary>
        [HttpGet("public")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RemoteAppListDto>>>> GetPublicApps(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.GetPublicAppsAsync(pagination, cancellationToken);
            }, "Get public remote apps");
        }

        #endregion

        #region User Assignment Management

        /// <summary>
        /// Assign user to remote app
        /// </summary>
        [HttpPost("{id}/users")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("AssignUserToRemoteApp")]
        public async Task<ActionResult<ApiResponse<bool>>> AssignUser(
            string id,
            [FromBody] RemoteAppUserAssignmentDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var userObjectIdResult = ParseObjectId(dto.UserId, "userId");
            if (userObjectIdResult.Result != null) return userObjectIdResult.Result!;

            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.AssignUserAsync(id, dto.UserId, cancellationToken);
            }, $"Assign user {dto.UserId} to remote app {id}");
        }

        /// <summary>
        /// Unassign user from remote app
        /// </summary>
        [HttpDelete("{id}/users/{userId}")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("UnassignUserFromRemoteApp")]
        public async Task<ActionResult<ApiResponse<bool>>> UnassignUser(
            string id,
            string userId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var userObjectIdResult = ParseObjectId(userId, "userId");
            if (userObjectIdResult.Result != null) return userObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.UnassignUserAsync(id, userId, cancellationToken);
            }, $"Unassign user {userId} from remote app {id}");
        }

        /// <summary>
        /// Check if user is assigned to remote app
        /// </summary>
        [HttpGet("{id}/users/{userId}/assigned")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<bool>>> IsUserAssigned(
            string id,
            string userId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var userObjectIdResult = ParseObjectId(userId, "userId");
            if (userObjectIdResult.Result != null) return userObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.IsUserAssignedAsync(id, userId, cancellationToken);
            }, $"Check if user {userId} is assigned to remote app {id}");
        }

        #endregion

        #region Status Management

        /// <summary>
        /// Update remote app status (active, inactive, maintenance)
        /// </summary>
        [HttpPut("{id}/status")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("UpdateRemoteAppStatus")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateStatus(
            string id,
            [FromBody] string status,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(status))
            {
                return ValidationError<bool>("Status is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.UpdateStatusAsync(id, status, cancellationToken);
            }, $"Update status for remote app {id}");
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate remote app name uniqueness
        /// </summary>
        [HttpPost("validate-name")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<bool>>> ValidateNameUnique(
            [FromBody] string name,
            [FromQuery] string? excludeId = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return ValidationError<bool>("Name is required");
            }

            // Validate excludeId if provided
            if (!string.IsNullOrEmpty(excludeId))
            {
                var objectIdResult = ParseObjectId(excludeId, "excludeId");
                if (objectIdResult.Result != null) return objectIdResult.Result!;
            }

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.ValidateNameUniqueAsync(name, excludeId, cancellationToken);
            }, $"Validate name uniqueness: {name}");
        }

        #endregion
    }
}