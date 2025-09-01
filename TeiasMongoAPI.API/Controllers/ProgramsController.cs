using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.DTOs;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    /// <summary>
    /// Program management controller - handles program entity lifecycle and metadata only.
    /// File operations are handled via VersionsController commit process.
    /// Deployment operations are handled via VersionsController for specific versions.
    /// Execution operations are handled via ExecutionsController.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProgramsController : BaseController
    {
        private readonly IProgramService _programService;
        private readonly IPermissionService _permissionService;

        public ProgramsController(
            IProgramService programService,
            IPermissionService permissionService,
            ILogger<ProgramsController> logger)
            : base(logger)
        {
            _programService = programService;
            _permissionService = permissionService;
        }

        #region Basic CRUD Operations

        /// <summary>
        /// Get all programs with pagination
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<ProgramListDto>>>> GetAll(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            // New Permission Check
            if (CurrentUserId == null) return Unauthorized<PagedResponse<ProgramListDto>>();
            // End of New Permission Check

            return await ExecuteAsync(async () =>
            {
                return await _programService.GetAllAsync(pagination, CurrentUserId.Value, cancellationToken);
            }, "Get all programs");
        }

        /// <summary>
        /// Get program by ID with full details (excluding files - use VersionsController for files)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<ProgramDetailDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // New Permission Check
            if (CurrentUserId == null) return Unauthorized<ProgramDetailDto>();

            var canView = await _permissionService.CanViewProgramDetails(CurrentUserId.Value, objectIdResult.Value);
            if (!canView) return Forbidden<ProgramDetailDto>("Access denied");
            // End of New Permission Check

            return await ExecuteAsync(async () =>
            {
                return await _programService.GetByIdAsync(id, cancellationToken);
            }, $"Get program {id}");
        }

        /// <summary>
        /// Create new program entity
        /// Note: This creates the program metadata only. Use VersionsController commit process to add files.
        /// </summary>
        [HttpPost]
        [AuditLog("CreateProgram")]
        public async Task<ActionResult<ApiResponse<ProgramDto>>> Create(
            [FromBody] ProgramCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // New Permission Check
            if (CurrentUserId == null) return Unauthorized<ProgramDto>();

            var canCreate = await _permissionService.CanCreateProgram(CurrentUserId.Value);
            if (!canCreate) return Forbidden<ProgramDto>("Access denied");
            // End of New Permission Check

            var validationResult = ValidateModelState<ProgramDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.CreateAsync(dto, CurrentUserId, cancellationToken);
            }, "Create program");
        }

        /// <summary>
        /// Update program metadata
        /// Note: File changes should be done through VersionsController commit process
        /// </summary>
        [HttpPut("{id}")]
        [AuditLog("UpdateProgram")]
        public async Task<ActionResult<ApiResponse<ProgramDto>>> Update(
            string id,
            [FromBody] ProgramUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // New Permission Check
            if (CurrentUserId == null) return Unauthorized<ProgramDto>();

            var canEdit = await _permissionService.CanEditProgram(CurrentUserId.Value, objectIdResult.Value);
            if (!canEdit) return Forbidden<ProgramDto>("Access denied");
            // End of New Permission Check

            var validationResult = ValidateModelState<ProgramDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.UpdateAsync(id, dto, cancellationToken);
            }, $"Update program {id}");
        }

        /// <summary>
        /// Delete program
        /// Note: This will also trigger cleanup of associated versions and files through service layer
        /// </summary>
        [HttpDelete("{id}")]
        [AuditLog("DeleteProgram")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // New Permission Check
            if (CurrentUserId == null) return Unauthorized<bool>();

            // Reusing CanEditProgram as this covers all modification permissions
            var canDelete = await _permissionService.CanEditProgram(CurrentUserId.Value, objectIdResult.Value);
            if (!canDelete) return Forbidden<bool>("Access denied");
            // End of New Permission Check

            return await ExecuteAsync(async () =>
            {
                return await _programService.DeleteAsync(id, cancellationToken);
            }, $"Delete program {id}");
        }

        #endregion

        #region Program Discovery and Search

        /// <summary>
        /// Advanced program search with filtering
        /// </summary>
        [HttpPost("search")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ProgramListDto>>>> Search(
            [FromBody] ProgramSearchDto searchDto,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _programService.SearchAsync(searchDto, pagination, cancellationToken);
            }, "Search programs");
        }

        /// <summary>
        /// Get programs by creator
        /// </summary>
        [HttpGet("by-creator/{creatorId}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ProgramListDto>>>> GetByCreator(
            string creatorId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(creatorId, "creatorId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _programService.GetByCreatorAsync(creatorId, pagination, cancellationToken);
            }, $"Get programs by creator {creatorId}");
        }

        /// <summary>
        /// Get programs by status (draft, active, archived, etc.)
        /// </summary>
        [HttpGet("by-status/{status}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ProgramListDto>>>> GetByStatus(
            string status,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _programService.GetByStatusAsync(status, pagination, cancellationToken);
            }, $"Get programs by status {status}");
        }

        /// <summary>
        /// Get programs by type (web, console, api, etc.)
        /// </summary>
        [HttpGet("by-type/{type}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ProgramListDto>>>> GetByType(
            string type,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _programService.GetByTypeAsync(type, pagination, cancellationToken);
            }, $"Get programs by type {type}");
        }

        /// <summary>
        /// Get programs by programming language
        /// </summary>
        [HttpGet("by-language/{language}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ProgramListDto>>>> GetByLanguage(
            string language,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _programService.GetByLanguageAsync(language, pagination, cancellationToken);
            }, $"Get programs by language {language}");
        }

        /// <summary>
        /// Get programs accessible to current user based on permissions with aggregated data
        /// </summary>
        [HttpGet("user-accessible")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ProgramSummaryDto>>>> GetUserAccessiblePrograms(
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

                return await _programService.GetUserAccessibleProgramsAsync(userId, pagination, cancellationToken);
            }, "Get user accessible programs");
        }

        /// <summary>
        /// Get programs accessible to a group
        /// </summary>
        [HttpGet("group-accessible/{groupId}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ProgramListDto>>>> GetGroupAccessiblePrograms(
            string groupId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(groupId, "groupId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _programService.GetGroupAccessibleProgramsAsync(groupId, pagination, cancellationToken);
            }, $"Get programs accessible to group {groupId}");
        }

        #endregion

        #region Program Status Management

        /// <summary>
        /// Update program status (draft, active, archived, deprecated)
        /// </summary>
        [HttpPut("{id}/status")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("UpdateProgramStatus")]
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
                return await _programService.UpdateStatusAsync(id, status, cancellationToken);
            }, $"Update status for program {id}");
        }

        /// <summary>
        /// Update program's current version (must be an approved version)
        /// Note: This sets which version is considered "current" for execution
        /// </summary>
        [HttpPut("{id}/current-version")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("UpdateProgramCurrentVersion")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateCurrentVersion(
            string id,
            [FromBody] string versionId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var versionObjectIdResult = ParseObjectId(versionId, "versionId");
            if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _programService.UpdateCurrentVersionAsync(id, versionId, cancellationToken);
            }, $"Update current version for program {id}");
        }

        #endregion

        #region Permission Management

        /// <summary>
        /// Get all permissions for a program
        /// </summary>
        [HttpGet("{id}/permissions")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<List<ProgramPermissionDto>>>> GetProgramPermissions(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _programService.GetProgramPermissionsAsync(id, cancellationToken);
            }, $"Get permissions for program {id}");
        }

        /// <summary>
        /// Add user permission to program
        /// </summary>
        [HttpPost("{id}/permissions/users")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("AddProgramUserPermission")]
        public async Task<ActionResult<ApiResponse<ProgramDto>>> AddUserPermission(
            string id,
            [FromBody] ProgramUserPermissionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var validationResult = ValidateModelState<ProgramDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.AddUserPermissionAsync(id, dto, cancellationToken);
            }, $"Add user permission to program {id}");
        }

        /// <summary>
        /// Update user permission for program
        /// </summary>
        [HttpPut("{id}/permissions/users/{userId}")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("UpdateProgramUserPermission")]
        public async Task<ActionResult<ApiResponse<ProgramDto>>> UpdateUserPermission(
            string id,
            string userId,
            [FromBody] ProgramUserPermissionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var userObjectIdResult = ParseObjectId(userId, "userId");
            if (userObjectIdResult.Result != null) return userObjectIdResult.Result!;

            var validationResult = ValidateModelState<ProgramDto>();
            if (validationResult != null) return validationResult;

            // Ensure the userId in the DTO matches the route parameter
            dto.UserId = userId;

            return await ExecuteAsync(async () =>
            {
                return await _programService.UpdateUserPermissionAsync(id, dto, cancellationToken);
            }, $"Update user permission for program {id}");
        }

        /// <summary>
        /// Remove user permission from program
        /// </summary>
        [HttpDelete("{id}/permissions/users/{userId}")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("RemoveProgramUserPermission")]
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
                return await _programService.RemoveUserPermissionAsync(id, userId, cancellationToken);
            }, $"Remove user permission from program {id}");
        }

        /// <summary>
        /// Add group permission to program
        /// </summary>
        [HttpPost("{id}/permissions/groups")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("AddProgramGroupPermission")]
        public async Task<ActionResult<ApiResponse<ProgramDto>>> AddGroupPermission(
            string id,
            [FromBody] ProgramGroupPermissionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var validationResult = ValidateModelState<ProgramDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.AddGroupPermissionAsync(id, dto, cancellationToken);
            }, $"Add group permission to program {id}");
        }

        /// <summary>
        /// Update group permission for program
        /// </summary>
        [HttpPut("{id}/permissions/groups/{groupId}")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("UpdateProgramGroupPermission")]
        public async Task<ActionResult<ApiResponse<ProgramDto>>> UpdateGroupPermission(
            string id,
            string groupId,
            [FromBody] ProgramGroupPermissionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var groupObjectIdResult = ParseObjectId(groupId, "groupId");
            if (groupObjectIdResult.Result != null) return groupObjectIdResult.Result!;

            var validationResult = ValidateModelState<ProgramDto>();
            if (validationResult != null) return validationResult;

            // Ensure the groupId in the DTO matches the route parameter
            dto.GroupId = groupId;

            return await ExecuteAsync(async () =>
            {
                return await _programService.UpdateGroupPermissionAsync(id, dto, cancellationToken);
            }, $"Update group permission for program {id}");
        }

        /// <summary>
        /// Remove group permission from program
        /// </summary>
        [HttpDelete("{id}/permissions/groups/{groupId}")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("RemoveProgramGroupPermission")]
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
                return await _programService.RemoveGroupPermissionAsync(id, groupId, cancellationToken);
            }, $"Remove group permission from program {id}");
        }

        #endregion

        #region Deployment Status (Read-Only)

        /// <summary>
        /// Get current deployment status for program
        /// Note: For deploying specific versions, use VersionsController
        /// </summary>
        [HttpGet("{id}/deployment/status")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<ProgramDeploymentStatusDto>>> GetDeploymentStatus(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _programService.GetDeploymentStatusAsync(id, cancellationToken);
            }, $"Get deployment status for program {id}");
        }

        /// <summary>
        /// Get application logs for deployed program
        /// </summary>
        [HttpGet("{id}/logs")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetApplicationLogs(
            string id,
            [FromQuery] int lines = 100,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (lines <= 0)
            {
                return ValidationError<List<string>>("Lines must be greater than 0");
            }

            return await ExecuteAsync(async () =>
            {
                return await _programService.GetApplicationLogsAsync(id, lines, cancellationToken);
            }, $"Get application logs for program {id}");
        }

        /// <summary>
        /// Restart deployed application
        /// Note: This restarts the currently deployed version
        /// </summary>
        [HttpPost("{id}/restart")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("RestartApplication")]
        public async Task<ActionResult<ApiResponse<bool>>> RestartApplication(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _programService.RestartApplicationAsync(id, cancellationToken);
            }, $"Restart application for program {id}");
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate program name uniqueness
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
                return await _programService.ValidateNameUniqueAsync(name, excludeId, cancellationToken);
            }, $"Validate name uniqueness: {name}");
        }

        /// <summary>
        /// Validate user access to program
        /// </summary>
        [HttpGet("{id}/validate-access")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<bool>>> ValidateUserAccess(
            string id,
            [FromQuery] string requiredAccessLevel,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(requiredAccessLevel))
            {
                return ValidationError<bool>("Required access level is required");
            }

            return await ExecuteAsync(async () =>
            {
                var userId = CurrentUserId?.ToString();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                return await _programService.ValidateUserAccessAsync(id, userId, requiredAccessLevel, cancellationToken);
            }, $"Validate user access to program {id}");
        }

        #endregion
    }
}