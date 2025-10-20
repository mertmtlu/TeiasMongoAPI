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
        /// Get remote apps by current user
        /// </summary>
        [HttpGet("by-current-user")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RemoteAppListDto>>>> GetByCurrentUser(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.GetByCreatorAsync(CurrentUserId.ToString(), pagination, cancellationToken);
            }, $"Get remote apps by creator {CurrentUserId}");
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

        #region Permission Management

        /// <summary>
        /// Get all permissions for a remote app
        /// </summary>
        [HttpGet("{id}/permissions")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<List<RemoteAppPermissionDto>>>> GetRemoteAppPermissions(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.GetRemoteAppPermissionsAsync(id, cancellationToken);
            }, $"Get permissions for remote app {id}");
        }

        /// <summary>
        /// Add user permission to remote app
        /// </summary>
        [HttpPost("{id}/permissions/users")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("AddRemoteAppUserPermission")]
        public async Task<ActionResult<ApiResponse<RemoteAppDto>>> AddUserPermission(
            string id,
            [FromBody] RemoteAppUserPermissionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var userObjectIdResult = ParseObjectId(dto.UserId, "userId");
            if (userObjectIdResult.Result != null) return userObjectIdResult.Result!;

            var validationResult = ValidateModelState<RemoteAppDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.AddUserPermissionAsync(id, dto, cancellationToken);
            }, $"Add user permission to remote app {id}");
        }

        /// <summary>
        /// Update user permission for remote app
        /// </summary>
        [HttpPut("{id}/permissions/users/{userId}")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("UpdateRemoteAppUserPermission")]
        public async Task<ActionResult<ApiResponse<RemoteAppDto>>> UpdateUserPermission(
            string id,
            string userId,
            [FromBody] RemoteAppUserPermissionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var userObjectIdResult = ParseObjectId(userId, "userId");
            if (userObjectIdResult.Result != null) return userObjectIdResult.Result!;

            var validationResult = ValidateModelState<RemoteAppDto>();
            if (validationResult != null) return validationResult;

            // Ensure userId in URL matches userId in DTO
            if (dto.UserId != userId)
            {
                return ValidationError<RemoteAppDto>("User ID in URL does not match User ID in request body");
            }

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.UpdateUserPermissionAsync(id, dto, cancellationToken);
            }, $"Update user permission for remote app {id}");
        }

        /// <summary>
        /// Remove user permission from remote app
        /// </summary>
        [HttpDelete("{id}/permissions/users/{userId}")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("RemoveRemoteAppUserPermission")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveUserPermission(
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
                return await _remoteAppService.RemoveUserPermissionAsync(id, userId, cancellationToken);
            }, $"Remove user permission from remote app {id}");
        }

        /// <summary>
        /// Add group permission to remote app
        /// </summary>
        [HttpPost("{id}/permissions/groups")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("AddRemoteAppGroupPermission")]
        public async Task<ActionResult<ApiResponse<RemoteAppDto>>> AddGroupPermission(
            string id,
            [FromBody] RemoteAppGroupPermissionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var validationResult = ValidateModelState<RemoteAppDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.AddGroupPermissionAsync(id, dto, cancellationToken);
            }, $"Add group permission to remote app {id}");
        }

        /// <summary>
        /// Update group permission for remote app
        /// </summary>
        [HttpPut("{id}/permissions/groups/{groupId}")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("UpdateRemoteAppGroupPermission")]
        public async Task<ActionResult<ApiResponse<RemoteAppDto>>> UpdateGroupPermission(
            string id,
            string groupId,
            [FromBody] RemoteAppGroupPermissionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var groupObjectIdResult = ParseObjectId(groupId, "groupId");
            if (groupObjectIdResult.Result != null) return groupObjectIdResult.Result!;

            var validationResult = ValidateModelState<RemoteAppDto>();
            if (validationResult != null) return validationResult;

            // Ensure groupId in URL matches groupId in DTO
            if (dto.GroupId != groupId)
            {
                return ValidationError<RemoteAppDto>("Group ID in URL does not match Group ID in request body");
            }

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.UpdateGroupPermissionAsync(id, dto, cancellationToken);
            }, $"Update group permission for remote app {id}");
        }

        /// <summary>
        /// Remove group permission from remote app
        /// </summary>
        [HttpDelete("{id}/permissions/groups/{groupId}")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("RemoveRemoteAppGroupPermission")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveGroupPermission(
            string id,
            string groupId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var groupObjectIdResult = ParseObjectId(groupId, "groupId");
            if (groupObjectIdResult.Result != null) return groupObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _remoteAppService.RemoveGroupPermissionAsync(id, groupId, cancellationToken);
            }, $"Remove group permission from remote app {id}");
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

        #region Launch

        /// <summary>
        /// Launch remote app - returns launch URL in response DTO
        /// </summary>
        [HttpGet("{id}/launch")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<RemoteAppLaunchDto>>> Launch(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                var userId = CurrentUserId?.ToString();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                var launchUrl = await _remoteAppService.GetLaunchUrlAsync(id, userId, cancellationToken);
                return new RemoteAppLaunchDto { RedirectUrl = launchUrl };
            }, $"Launch remote app {id}");
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