using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    /// <summary>
    /// Demo Showcase controller - manages video demos for programs, workflows, and remote apps
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    public class DemoShowcaseController : BaseController
    {
        private readonly IDemoShowcaseService _demoShowcaseService;

        public DemoShowcaseController(
            IDemoShowcaseService demoShowcaseService,
            ILogger<DemoShowcaseController> logger)
            : base(logger)
        {
            _demoShowcaseService = demoShowcaseService;
        }

        #region Public Endpoints

        /// <summary>
        /// Get all demo showcases with nested 3-level grouping structure (Tab -> PrimaryGroup -> SecondaryGroup)
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<PublicDemoShowcaseResponse>), 200)]
        public async Task<ActionResult<ApiResponse<PublicDemoShowcaseResponse>>> GetPublicShowcase(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.GetPublicDemoShowcaseAsync(cancellationToken);
            }, "Get public demo showcase");
        }

        /// <summary>
        /// Get public UI component schema/configuration for a specific app
        /// </summary>
        [HttpGet("ui-component/{appId}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<UiComponentResponseDto>), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<UiComponentResponseDto>>> GetPublicUiComponent(
            string appId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.GetPublicUiComponentAsync(appId, cancellationToken);
            }, $"Get public UI component for app {appId}");
        }

        /// <summary>
        /// Execute a public app with provided inputs (uses system user)
        /// </summary>
        [HttpPost("execute/{appId}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<ExecutionResponseDto>), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<ExecutionResponseDto>>> ExecutePublicApp(
            string appId,
            [FromBody] ExecutionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var validationResult = ValidateModelState<ExecutionResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.ExecutePublicAppAsync(appId, request, cancellationToken);
            }, $"Execute public app {appId}");
        }

        /// <summary>
        /// Get all demo showcases with associated app details (public - legacy)
        /// </summary>
        [HttpGet("legacy")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<DemoShowcasePublicDto>>>> GetAllLegacy(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.GetAllPublicAsync(cancellationToken);
            }, "Get all demo showcases (legacy)");
        }

        #endregion

        #region Public Execution Monitoring

        /// <summary>
        /// Get public execution status and details
        /// </summary>
        [HttpGet("execution/{executionId}/status")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<PublicExecutionDetailDto>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<PublicExecutionDetailDto>>> GetPublicExecutionStatus(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.GetPublicExecutionDetailAsync(executionId, cancellationToken);
            }, $"Get public execution status {executionId}");
        }

        /// <summary>
        /// Get public execution logs
        /// </summary>
        [HttpGet("execution/{executionId}/logs")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<PublicExecutionLogsDto>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<PublicExecutionLogsDto>>> GetPublicExecutionLogs(
            string executionId,
            [FromQuery] int lines = 100,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.GetPublicExecutionLogsAsync(executionId, lines, cancellationToken);
            }, $"Get public execution logs {executionId}");
        }

        /// <summary>
        /// List public execution output files
        /// </summary>
        [HttpGet("execution/{executionId}/files")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<PublicExecutionFilesDto>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<PublicExecutionFilesDto>>> GetPublicExecutionFiles(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.GetPublicExecutionFilesAsync(executionId, cancellationToken);
            }, $"Get public execution files {executionId}");
        }

        /// <summary>
        /// Download a single output file from public execution
        /// </summary>
        [HttpGet("execution/{executionId}/files/download/{*filePath}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<VersionFileDetailDto>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<ApiResponse<VersionFileDetailDto>>> DownloadPublicExecutionFile(
            string executionId,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.DownloadPublicExecutionFileAsync(executionId, filePath, cancellationToken);
            }, $"Download public execution file {executionId}/{filePath}");
        }

        #endregion

        #region Admin Endpoints

        /// <summary>
        /// Get all demo showcases for admin (raw data)
        /// </summary>
        [HttpGet("admin")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        public async Task<ActionResult<ApiResponse<List<DemoShowcaseDto>>>> GetAllAdmin(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.GetAllAdminAsync(cancellationToken);
            }, "Get all demo showcases for admin");
        }

        /// <summary>
        /// Create a new demo showcase entry
        /// </summary>
        [HttpPost]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("CreateDemoShowcase")]
        public async Task<ActionResult<ApiResponse<DemoShowcaseDto>>> Create(
            [FromBody] DemoShowcaseCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            var validationResult = ValidateModelState<DemoShowcaseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.CreateAsync(dto, cancellationToken);
            }, "Create demo showcase");
        }

        /// <summary>
        /// Update a demo showcase entry
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("UpdateDemoShowcase")]
        public async Task<ActionResult<ApiResponse<DemoShowcaseDto>>> Update(
            string id,
            [FromBody] DemoShowcaseUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var validationResult = ValidateModelState<DemoShowcaseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.UpdateAsync(id, dto, cancellationToken);
            }, $"Update demo showcase {id}");
        }

        /// <summary>
        /// Delete a demo showcase entry
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("DeleteDemoShowcase")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.DeleteAsync(id, cancellationToken);
            }, $"Delete demo showcase {id}");
        }

        /// <summary>
        /// Upload a video file and return its path
        /// </summary>
        [HttpPost("upload-video")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [RequestSizeLimit(524_288_000)]  // 500 MB
        [DisableRequestSizeLimit]
        public async Task<ActionResult<ApiResponse<VideoUploadResponseDto>>> UploadVideo(
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
            {
                return ValidationError<VideoUploadResponseDto>("No file uploaded");
            }

            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.UploadVideoAsync(file, cancellationToken);
            }, "Upload demo video");
        }

        /// <summary>
        /// Get all available apps for admin dropdown selector
        /// </summary>
        [HttpGet("available-apps")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<AvailableAppsDto>>> GetAvailableApps(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _demoShowcaseService.GetAvailableAppsAsync(cancellationToken);
            }, "Get available apps");
        }

        #endregion
    }
}
