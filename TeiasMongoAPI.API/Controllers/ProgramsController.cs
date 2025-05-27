using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProgramsController : BaseController
    {
        private readonly IProgramService _programService;

        public ProgramsController(
            IProgramService programService,
            ILogger<ProgramsController> logger)
            : base(logger)
        {
            _programService = programService;
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
            return await ExecuteAsync(async () =>
            {
                return await _programService.GetAllAsync(pagination, cancellationToken);
            }, "Get all programs");
        }

        /// <summary>
        /// Get program by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<ProgramDetailDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _programService.GetByIdAsync(id, cancellationToken);
            }, $"Get program {id}");
        }

        /// <summary>
        /// Create new program
        /// </summary>
        [HttpPost]
        [AuditLog("CreateProgram")]
        public async Task<ActionResult<ApiResponse<ProgramDto>>> Create(
            [FromBody] ProgramCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<ProgramDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.CreateAsync(dto, cancellationToken);
            }, "Create program");
        }

        /// <summary>
        /// Update program
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

            // Validate model state
            var validationResult = ValidateModelState<ProgramDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.UpdateAsync(id, dto, cancellationToken);
            }, $"Update program {id}");
        }

        /// <summary>
        /// Delete program
        /// </summary>
        [HttpDelete("{id}")]
        [AuditLog("DeleteProgram")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _programService.DeleteAsync(id, cancellationToken);
            }, $"Delete program {id}");
        }

        #endregion

        #region Program Management

        /// <summary>
        /// Advanced program search
        /// </summary>
        [HttpPost("search")]
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
        /// Get programs by status
        /// </summary>
        [HttpGet("by-status/{status}")]
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
        /// Get programs by language
        /// </summary>
        [HttpGet("by-language/{language}")]
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
        /// Get programs accessible to current user
        /// </summary>
        [HttpGet("user-accessible")]
        public async Task<ActionResult<ApiResponse<PagedResponse<ProgramListDto>>>> GetUserAccessiblePrograms(
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
        /// Update program status
        /// </summary>
        [HttpPut("{id}/status")]
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
        /// Update program current version
        /// </summary>
        [HttpPut("{id}/current-version")]
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
        /// Add user permission to program
        /// </summary>
        [HttpPost("{id}/users")]
        [AuditLog("AddProgramUserPermission")]
        public async Task<ActionResult<ApiResponse<ProgramDto>>> AddUserPermission(
            string id,
            [FromBody] ProgramUserPermissionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ProgramDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.AddUserPermissionAsync(id, dto, cancellationToken);
            }, $"Add user permission to program {id}");
        }

        /// <summary>
        /// Remove user permission from program
        /// </summary>
        [HttpDelete("{id}/users/{userId}")]
        [AuditLog("RemoveProgramUserPermission")]
        [RequirePermission(UserPermissions.DeletePrograms)]
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
        /// Update user permission for program
        /// </summary>
        [HttpPut("{id}/users/{userId}")]
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

            // Validate model state
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
        /// Add group permission to program
        /// </summary>
        [HttpPost("{id}/groups")]
        [AuditLog("AddProgramGroupPermission")]
        public async Task<ActionResult<ApiResponse<ProgramDto>>> AddGroupPermission(
            string id,
            [FromBody] ProgramGroupPermissionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ProgramDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.AddGroupPermissionAsync(id, dto, cancellationToken);
            }, $"Add group permission to program {id}");
        }

        /// <summary>
        /// Remove group permission from program
        /// </summary>
        [HttpDelete("{id}/groups/{groupId}")]
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

        /// <summary>
        /// Get program permissions
        /// </summary>
        [HttpGet("{id}/permissions")]
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

        #endregion

        #region File Management

        /// <summary>
        /// Upload files to program
        /// </summary>
        [HttpPost("{id}/files")]
        [AuditLog("UploadProgramFiles")]
        public async Task<ActionResult<ApiResponse<bool>>> UploadFiles(
            string id,
            [FromBody] List<ProgramFileUploadDto> files,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (files == null || !files.Any())
            {
                return ValidationError<bool>("At least one file is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await _programService.UploadFilesAsync(id, files, cancellationToken);
            }, $"Upload files to program {id}");
        }

        /// <summary>
        /// Get program files
        /// </summary>
        [HttpGet("{id}/files")]
        public async Task<ActionResult<ApiResponse<List<ProgramFileDto>>>> GetFiles(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _programService.GetFilesAsync(id, cancellationToken);
            }, $"Get files for program {id}");
        }

        /// <summary>
        /// Get program file content
        /// </summary>
        [HttpGet("{id}/files/{*filePath}")]
        public async Task<ActionResult<ApiResponse<ProgramFileContentDto>>> GetFileContent(
            string id,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ValidationError<ProgramFileContentDto>("File path is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await _programService.GetFileContentAsync(id, filePath, cancellationToken);
            }, $"Get file content for program {id}, file {filePath}");
        }

        /// <summary>
        /// Update program file
        /// </summary>
        [HttpPut("{id}/files/{*filePath}")]
        [AuditLog("UpdateProgramFile")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateFile(
            string id,
            string filePath,
            [FromBody] ProgramFileUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ValidationError<bool>("File path is required");
            }

            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.UpdateFileAsync(id, filePath, dto, cancellationToken);
            }, $"Update file for program {id}, file {filePath}");
        }

        /// <summary>
        /// Delete program file
        /// </summary>
        [HttpDelete("{id}/files/{*filePath}")]
        [AuditLog("DeleteProgramFile")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteFile(
            string id,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ValidationError<bool>("File path is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await _programService.DeleteFileAsync(id, filePath, cancellationToken);
            }, $"Delete file for program {id}, file {filePath}");
        }

        #endregion

        #region Deployment Operations

        /// <summary>
        /// Deploy pre-built application
        /// </summary>
        [HttpPost("{id}/deploy/prebuilt")]
        [AuditLog("DeployPreBuiltApp")]
        public async Task<ActionResult<ApiResponse<ProgramDeploymentDto>>> DeployPreBuiltApp(
            string id,
            [FromBody] ProgramDeploymentRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ProgramDeploymentDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.DeployPreBuiltAppAsync(id, dto, cancellationToken);
            }, $"Deploy pre-built app for program {id}");
        }

        /// <summary>
        /// Deploy static site
        /// </summary>
        [HttpPost("{id}/deploy/static")]
        [AuditLog("DeployStaticSite")]
        public async Task<ActionResult<ApiResponse<ProgramDeploymentDto>>> DeployStaticSite(
            string id,
            [FromBody] ProgramDeploymentRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ProgramDeploymentDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.DeployStaticSiteAsync(id, dto, cancellationToken);
            }, $"Deploy static site for program {id}");
        }

        /// <summary>
        /// Deploy container application
        /// </summary>
        [HttpPost("{id}/deploy/container")]
        [AuditLog("DeployContainerApp")]
        public async Task<ActionResult<ApiResponse<ProgramDeploymentDto>>> DeployContainerApp(
            string id,
            [FromBody] ProgramDeploymentRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ProgramDeploymentDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.DeployContainerAppAsync(id, dto, cancellationToken);
            }, $"Deploy container app for program {id}");
        }

        /// <summary>
        /// Get deployment status
        /// </summary>
        [HttpGet("{id}/deployment/status")]
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
        /// Restart application
        /// </summary>
        [HttpPost("{id}/restart")]
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

        /// <summary>
        /// Get application logs
        /// </summary>
        [HttpGet("{id}/logs")]
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
        /// Update deployment configuration
        /// </summary>
        [HttpPut("{id}/deployment/config")]
        [AuditLog("UpdateDeploymentConfig")]
        public async Task<ActionResult<ApiResponse<ProgramDto>>> UpdateDeploymentConfig(
            string id,
            [FromBody] ProgramDeploymentConfigDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ProgramDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _programService.UpdateDeploymentConfigAsync(id, dto, cancellationToken);
            }, $"Update deployment config for program {id}");
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate program name uniqueness
        /// </summary>
        [HttpPost("validate-name")]
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