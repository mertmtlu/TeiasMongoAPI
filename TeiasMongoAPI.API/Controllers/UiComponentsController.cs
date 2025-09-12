using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
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
    public class UiComponentsController : BaseController
    {
        private readonly IUiComponentService _uiComponentService;

        public UiComponentsController(
            IUiComponentService uiComponentService,
            ILogger<UiComponentsController> logger)
            : base(logger)
        {
            _uiComponentService = uiComponentService;
        }

        #region Basic CRUD Operations

        /// <summary>
        /// Get all UI components with pagination
        /// </summary>
        [HttpGet]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UiComponentListDto>>>> GetAll(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetAllAsync(pagination, cancellationToken);
            }, "Get all UI components");
        }

        /// <summary>
        /// Get UI component by ID
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<UiComponentDetailDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetByIdAsync(id, cancellationToken);
            }, $"Get UI component {id}");
        }

        /// <summary>
        /// Create new UI component for a specific program version
        /// </summary>
        [HttpPost("programs/{programId}/versions/{versionId}")]
        [RequirePermission(UserPermissions.CreateComponents)]
        [AuditLog("CreateUiComponent")]
        public async Task<ActionResult<ApiResponse<UiComponentDto>>> Create(
            string programId,
            string versionId,
            [FromBody] UiComponentCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var versionObjectIdResult = ParseObjectId(versionId, "versionId");
            if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<UiComponentDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.CreateAsync(programId, versionId, dto, CurrentUserId, cancellationToken);
            }, $"Create UI component for program {programId}, version {versionId}");
        }

        /// <summary>
        /// Update UI component
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("UpdateUiComponent")]
        public async Task<ActionResult<ApiResponse<UiComponentDto>>> Update(
            string id,
            [FromBody] UiComponentUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<UiComponentDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.UpdateAsync(id, dto, cancellationToken);
            }, $"Update UI component {id}");
        }

        /// <summary>
        /// Delete UI component
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.DeleteComponents)]
        [AuditLog("DeleteUiComponent")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.DeleteAsync(id, cancellationToken);
            }, $"Delete UI component {id}");
        }

        #endregion

        #region Version-specific Component Operations

        /// <summary>
        /// Get UI components for a specific program version
        /// </summary>
        [HttpGet("programs/{programId}/versions/{versionId}")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UiComponentListDto>>>> GetByProgramVersion(
            string programId,
            string versionId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var versionObjectIdResult = ParseObjectId(versionId, "versionId");
            if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetByProgramVersionAsync(programId, versionId, pagination, cancellationToken);
            }, $"Get UI components for program {programId}, version {versionId}");
        }

        /// <summary>
        /// Copy components from one version to another
        /// </summary>
        [HttpPost("programs/{programId}/versions/{fromVersionId}/copy-to/{toVersionId}")]
        [RequirePermission(UserPermissions.CreateComponents)]
        [AuditLog("CopyUiComponents")]
        public async Task<ActionResult<ApiResponse<List<UiComponentListDto>>>> CopyComponentsToNewVersion(
            string programId,
            string fromVersionId,
            string toVersionId,
            [FromBody] List<string>? componentNames = null,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var fromVersionObjectIdResult = ParseObjectId(fromVersionId, "fromVersionId");
            if (fromVersionObjectIdResult.Result != null) return fromVersionObjectIdResult.Result!;

            var toVersionObjectIdResult = ParseObjectId(toVersionId, "toVersionId");
            if (toVersionObjectIdResult.Result != null) return toVersionObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.CopyComponentsToNewVersionAsync(programId, fromVersionId, toVersionId, componentNames, cancellationToken);
            }, $"Copy components from version {fromVersionId} to {toVersionId}");
        }

        /// <summary>
        /// Copy a component to a different program version
        /// </summary>
        [HttpPost("{componentId}/copy-to/programs/{targetProgramId}/versions/{targetVersionId}")]
        [RequirePermission(UserPermissions.CreateComponents)]
        [AuditLog("CopyUiComponentToVersion")]
        public async Task<ActionResult<ApiResponse<bool>>> CopyComponentToVersion(
            string componentId,
            string targetProgramId,
            string targetVersionId,
            [FromQuery] string? newName = null,
            CancellationToken cancellationToken = default)
        {
            var componentObjectIdResult = ParseObjectId(componentId, "componentId");
            if (componentObjectIdResult.Result != null) return componentObjectIdResult.Result!;

            var targetProgramObjectIdResult = ParseObjectId(targetProgramId, "targetProgramId");
            if (targetProgramObjectIdResult.Result != null) return targetProgramObjectIdResult.Result!;

            var targetVersionObjectIdResult = ParseObjectId(targetVersionId, "targetVersionId");
            if (targetVersionObjectIdResult.Result != null) return targetVersionObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.CopyComponentToVersionAsync(componentId, targetProgramId, targetVersionId, newName, cancellationToken);
            }, $"Copy component {componentId} to program {targetProgramId}, version {targetVersionId}");
        }

        /// <summary>
        /// Get available components for a program version
        /// </summary>
        [HttpGet("programs/{programId}/versions/{versionId}/available")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UiComponentListDto>>>> GetAvailableForProgramVersion(
            string programId,
            string versionId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var versionObjectIdResult = ParseObjectId(versionId, "versionId");
            if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetAvailableForProgramVersionAsync(programId, versionId, pagination, cancellationToken);
            }, $"Get available components for program {programId}, version {versionId}");
        }

        #endregion

        #region Component Filtering and Discovery

        /// <summary>
        /// Advanced UI component search
        /// </summary>
        [HttpPost("search")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UiComponentListDto>>>> Search(
            [FromBody] UiComponentSearchDto searchDto,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.SearchAsync(searchDto, pagination, cancellationToken);
            }, "Search UI components");
        }

        /// <summary>
        /// Get UI components by program
        /// </summary>
        [HttpGet("programs/{programId}")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UiComponentListDto>>>> GetByProgram(
            string programId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetByProgramIdAsync(programId, pagination, cancellationToken);
            }, $"Get UI components by program {programId}");
        }

        /// <summary>
        /// Get UI components by type
        /// </summary>
        [HttpGet("by-type/{type}")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UiComponentListDto>>>> GetByType(
            string type,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetByTypeAsync(type, pagination, cancellationToken);
            }, $"Get UI components by type {type}");
        }

        /// <summary>
        /// Get UI components by creator
        /// </summary>
        [HttpGet("by-creator/{creatorId}")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UiComponentListDto>>>> GetByCreator(
            string creatorId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(creatorId, "creatorId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetByCreatorAsync(creatorId, pagination, cancellationToken);
            }, $"Get UI components by creator {creatorId}");
        }

        /// <summary>
        /// Get UI components by status
        /// </summary>
        [HttpGet("by-status/{status}")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UiComponentListDto>>>> GetByStatus(
            string status,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetByStatusAsync(status, pagination, cancellationToken);
            }, $"Get UI components by status {status}");
        }

        #endregion

        #region Component Lifecycle Management

        /// <summary>
        /// Update UI component status
        /// </summary>
        [HttpPut("{id}/status")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("UpdateUiComponentStatus")]
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
                return await _uiComponentService.UpdateStatusAsync(id, status, cancellationToken);
            }, $"Update status for UI component {id}");
        }

        /// <summary>
        /// Activate UI component
        /// </summary>
        [HttpPost("{id}/activate")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("ActivateUiComponent")]
        public async Task<ActionResult<ApiResponse<bool>>> ActivateComponent(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.ActivateComponentAsync(id, cancellationToken);
            }, $"Activate UI component {id}");
        }

        /// <summary>
        /// Deactivate UI component
        /// </summary>
        [HttpPost("{id}/deactivate")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("DeactivateUiComponent")]
        public async Task<ActionResult<ApiResponse<bool>>> DeactivateComponent(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.DeactivateComponentAsync(id, cancellationToken);
            }, $"Deactivate UI component {id}");
        }

        /// <summary>
        /// Deprecate UI component
        /// </summary>
        [HttpPost("{id}/deprecate")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("DeprecateUiComponent")]
        public async Task<ActionResult<ApiResponse<bool>>> DeprecateComponent(
            string id,
            [FromBody] string reason,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(reason))
            {
                return ValidationError<bool>("Deprecation reason is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.DeprecateComponentAsync(id, reason, cancellationToken);
            }, $"Deprecate UI component {id}");
        }

        #endregion

        #region Component Bundle Management

        /// <summary>
        /// Get UI component bundle
        /// </summary>
        [HttpGet("{id}/bundle")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<UiComponentBundleDto>>> GetComponentBundle(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetComponentBundleAsync(id, cancellationToken);
            }, $"Get bundle for UI component {id}");
        }

        /// <summary>
        /// Upload UI component bundle (assets will be stored in version-specific storage)
        /// </summary>
        [HttpPost("{id}/upload-bundle")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("UploadUiComponentBundle")]
        public async Task<ActionResult<ApiResponse<bool>>> UploadComponentBundle(
            string id,
            [FromBody] UiComponentBundleUploadDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.UploadComponentBundleAsync(id, dto, cancellationToken);
            }, $"Upload bundle for UI component {id}");
        }

        /// <summary>
        /// Update UI component assets
        /// </summary>
        [HttpPut("{id}/assets")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("UpdateUiComponentAssets")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateComponentAssets(
            string id,
            [FromBody] List<UiComponentAssetDto> assets,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (assets == null || !assets.Any())
            {
                return ValidationError<bool>("At least one asset is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.UpdateComponentAssetsAsync(id, assets, cancellationToken);
            }, $"Update assets for UI component {id}");
        }

        /// <summary>
        /// Get UI component assets (retrieved from version-specific storage)
        /// </summary>
        [HttpGet("{id}/assets")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<List<UiComponentAssetDto>>>> GetComponentAssets(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetComponentAssetsAsync(id, cancellationToken);
            }, $"Get assets for UI component {id}");
        }

        #endregion

        #region Component Configuration and Schema

        /// <summary>
        /// Get UI component configuration
        /// </summary>
        [HttpGet("{id}/configuration")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<UiComponentConfigDto>>> GetComponentConfiguration(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetComponentConfigurationAsync(id, cancellationToken);
            }, $"Get configuration for UI component {id}");
        }

        /// <summary>
        /// Update UI component configuration
        /// </summary>
        [HttpPut("{id}/configuration")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("UpdateUiComponentConfiguration")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateComponentConfiguration(
            string id,
            [FromBody] UiComponentConfigUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.UpdateComponentConfigurationAsync(id, dto, cancellationToken);
            }, $"Update configuration for UI component {id}");
        }

        /// <summary>
        /// Get UI component schema
        /// </summary>
        [HttpGet("{id}/schema")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<UiComponentSchemaDto>>> GetComponentSchema(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetComponentSchemaAsync(id, cancellationToken);
            }, $"Get schema for UI component {id}");
        }

        /// <summary>
        /// Update UI component schema
        /// </summary>
        [HttpPut("{id}/schema")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("UpdateUiComponentSchema")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateComponentSchema(
            string id,
            [FromBody] UiComponentSchemaUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.UpdateComponentSchemaAsync(id, dto, cancellationToken);
            }, $"Update schema for UI component {id}");
        }

        /// <summary>
        /// Validate UI component schema
        /// </summary>
        [HttpPost("{id}/validate-schema")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<bool>>> ValidateComponentSchema(
            string id,
            [FromBody] object testData,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (testData == null)
            {
                return ValidationError<bool>("Test data is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.ValidateComponentSchemaAsync(id, testData, cancellationToken);
            }, $"Validate schema for UI component {id}");
        }

        #endregion

        #region Component Usage and Integration

        /// <summary>
        /// Get UI component usage
        /// </summary>
        [HttpGet("{id}/usage")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<List<UiComponentUsageDto>>>> GetComponentUsage(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetComponentUsageAsync(id, cancellationToken);
            }, $"Get usage for UI component {id}");
        }

        /// <summary>
        /// Get program version component mappings
        /// </summary>
        [HttpGet("programs/{programId}/versions/{versionId}/mappings")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<List<ProgramComponentMappingDto>>>> GetProgramVersionComponentMappings(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var versionObjectIdResult = ParseObjectId(versionId, "versionId");
            if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetProgramVersionComponentMappingsAsync(programId, versionId, cancellationToken);
            }, $"Get component mappings for program {programId}, version {versionId}");
        }

        /// <summary>
        /// Map component to program version
        /// </summary>
        [HttpPost("programs/{programId}/versions/{versionId}/map")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("MapComponentToProgramVersion")]
        public async Task<ActionResult<ApiResponse<bool>>> MapComponentToProgramVersion(
            string programId,
            string versionId,
            [FromBody] UiComponentMappingDto dto,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var versionObjectIdResult = ParseObjectId(versionId, "versionId");
            if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.MapComponentToProgramVersionAsync(programId, versionId, dto, cancellationToken);
            }, $"Map component to program {programId}, version {versionId}");
        }

        /// <summary>
        /// Unmap component from program version
        /// </summary>
        [HttpDelete("programs/{programId}/versions/{versionId}/unmap/{componentId}")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("UnmapComponentFromProgramVersion")]
        public async Task<ActionResult<ApiResponse<bool>>> UnmapComponentFromProgramVersion(
            string programId,
            string versionId,
            string componentId,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var versionObjectIdResult = ParseObjectId(versionId, "versionId");
            if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

            var componentObjectIdResult = ParseObjectId(componentId, "componentId");
            if (componentObjectIdResult.Result != null) return componentObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.UnmapComponentFromProgramVersionAsync(programId, versionId, componentId, cancellationToken);
            }, $"Unmap component {componentId} from program {programId}, version {versionId}");
        }

        #endregion

        #region Component Discovery and Recommendations

        /// <summary>
        /// Get recommended components for program version
        /// </summary>
        [HttpGet("programs/{programId}/versions/{versionId}/recommendations")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<List<UiComponentRecommendationDto>>>> GetRecommendedComponents(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var versionObjectIdResult = ParseObjectId(versionId, "versionId");
            if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetRecommendedComponentsAsync(programId, versionId, cancellationToken);
            }, $"Get recommended components for program {programId}, version {versionId}");
        }

        /// <summary>
        /// Search compatible components
        /// </summary>
        [HttpPost("search-compatible")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<List<UiComponentListDto>>>> SearchCompatibleComponents(
            [FromBody] UiComponentCompatibilitySearchDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<List<UiComponentListDto>>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.SearchCompatibleComponentsAsync(dto, cancellationToken);
            }, "Search compatible components");
        }

        #endregion

        #region Component Validation

        /// <summary>
        /// Validate component name uniqueness for program version
        /// </summary>
        [HttpPost("programs/{programId}/versions/{versionId}/validate-name")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<bool>>> ValidateNameUniqueForVersion(
            string programId,
            string versionId,
            [FromBody] string name,
            [FromQuery] string? excludeId = null,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var versionObjectIdResult = ParseObjectId(versionId, "versionId");
            if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(name))
            {
                return ValidationError<bool>("Name is required");
            }

            // Validate excludeId if provided
            if (!string.IsNullOrEmpty(excludeId))
            {
                var excludeObjectIdResult = ParseObjectId(excludeId, "excludeId");
                if (excludeObjectIdResult.Result != null) return excludeObjectIdResult.Result!;
            }

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.ValidateNameUniqueForVersionAsync(programId, versionId, name, excludeId, cancellationToken);
            }, $"Validate name uniqueness for program {programId}, version {versionId}: {name}");
        }

        /// <summary>
        /// Validate component definition for program version
        /// </summary>
        [HttpPost("programs/{programId}/versions/{versionId}/validate-definition")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<UiComponentValidationResult>>> ValidateComponentDefinition(
            string programId,
            string versionId,
            [FromBody] UiComponentCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var versionObjectIdResult = ParseObjectId(versionId, "versionId");
            if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<UiComponentValidationResult>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.ValidateComponentDefinitionAsync(programId, versionId, dto, cancellationToken);
            }, $"Validate component definition for program {programId}, version {versionId}");
        }

        #endregion

        #region Component Categories and Tags

        /// <summary>
        /// Get available component types
        /// </summary>
        [HttpGet("types")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetAvailableComponentTypes(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetAvailableComponentTypesAsync(cancellationToken);
            }, "Get available component types");
        }

        /// <summary>
        /// Get component categories
        /// </summary>
        [HttpGet("categories")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<List<UiComponentCategoryDto>>>> GetComponentCategories(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.GetComponentCategoriesAsync(cancellationToken);
            }, "Get component categories");
        }

        /// <summary>
        /// Add tags to component
        /// </summary>
        [HttpPost("{id}/tags")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("AddUiComponentTags")]
        public async Task<ActionResult<ApiResponse<bool>>> AddComponentTags(
            string id,
            [FromBody] List<string> tags,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (tags == null || !tags.Any())
            {
                return ValidationError<bool>("At least one tag is required");
            }

            // Remove empty or whitespace tags
            tags = tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();

            if (!tags.Any())
            {
                return ValidationError<bool>("At least one valid tag is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.AddComponentTagsAsync(id, tags, cancellationToken);
            }, $"Add tags to UI component {id}");
        }

        /// <summary>
        /// Remove tags from component
        /// </summary>
        [HttpDelete("{id}/tags")]
        [RequirePermission(UserPermissions.UpdateComponents)]
        [AuditLog("RemoveUiComponentTags")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveComponentTags(
            string id,
            [FromBody] List<string> tags,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (tags == null || !tags.Any())
            {
                return ValidationError<bool>("At least one tag is required");
            }

            // Remove empty or whitespace tags
            tags = tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();

            if (!tags.Any())
            {
                return ValidationError<bool>("At least one valid tag is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await _uiComponentService.RemoveComponentTagsAsync(id, tags, cancellationToken);
            }, $"Remove tags from UI component {id}");
        }

        #endregion

        #region Additional Utility Methods

        /// <summary>
        /// Get current user's accessible components
        /// </summary>
        [HttpGet("my-components")]
        [RequirePermission(UserPermissions.ViewComponents)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UiComponentListDto>>>> GetMyComponents(
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

                return await _uiComponentService.GetByCreatorAsync(userId, pagination, cancellationToken);
            }, "Get my UI components");
        }

        /// <summary>
        /// Clone component to a specific program version
        /// </summary>
        [HttpPost("{id}/clone-to/programs/{targetProgramId}/versions/{targetVersionId}")]
        [RequirePermission(UserPermissions.CreateComponents)]
        [AuditLog("CloneUiComponent")]
        public async Task<ActionResult<ApiResponse<UiComponentDto>>> CloneComponent(
            string id,
            string targetProgramId,
            string targetVersionId,
            [FromBody] string? newName = null,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var targetProgramObjectIdResult = ParseObjectId(targetProgramId, "targetProgramId");
            if (targetProgramObjectIdResult.Result != null) return targetProgramObjectIdResult.Result!;

            var targetVersionObjectIdResult = ParseObjectId(targetVersionId, "targetVersionId");
            if (targetVersionObjectIdResult.Result != null) return targetVersionObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                // Get the original component
                var originalComponent = await _uiComponentService.GetByIdAsync(id, cancellationToken);

                // Create the clone with the original component data
                var cloneDto = new UiComponentCreateDto
                {
                    Name = newName ?? $"{originalComponent.Name}_Copy",
                    Description = originalComponent.Description,
                    Type = originalComponent.Type,
                    Configuration = originalComponent.Configuration != null ? originalComponent.Configuration.ToJson() : string.Empty,
                    Schema = originalComponent.Schema != null ? originalComponent.Schema.ToJson() : string.Empty,
                    Tags = originalComponent.Tags?.ToList() ?? new List<string>()
                };

                // Create the cloned component in the target program version
                var clonedComponent = await _uiComponentService.CreateAsync(targetProgramId, targetVersionId, cloneDto, CurrentUserId, cancellationToken);

                // Copy assets using the service's copy method
                await _uiComponentService.CopyComponentToVersionAsync(id, targetProgramId, targetVersionId, newName, cancellationToken);

                return clonedComponent;
            }, $"Clone UI component {id} to program {targetProgramId}, version {targetVersionId}");
        }

        ///// <summary>
        ///// Export component definition with assets
        ///// </summary>
        //[HttpGet("{id}/export")]
        //[RequirePermission(UserPermissions.ViewComponents)]
        //public async Task<ActionResult<ApiResponse<object>>> ExportComponent(
        //    string id,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(id);
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    return await ExecuteAsync(async () =>
        //    {
        //        var component = await _uiComponentService.GetByIdAsync(id, cancellationToken);
        //        var bundle = await _uiComponentService.GetComponentBundleAsync(id, cancellationToken);
        //        var assets = await _uiComponentService.GetComponentAssetsAsync(id, cancellationToken);

        //        return new
        //        {
        //            Component = component,
        //            Bundle = bundle,
        //            Assets = assets,
        //            ExportedAt = DateTime.UtcNow,
        //            ExportedBy = CurrentUsername,
        //            StorageVersion = "v2", // Indicates new version-specific storage
        //            ProgramId = component.ProgramId,
        //            VersionId = component.VersionId
        //        };
        //    }, $"Export UI component {id}");
        //}

        #endregion
    }
}