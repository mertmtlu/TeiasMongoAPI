using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FilesController : BaseController
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IProgramService _programService;
        private readonly IVersionService _versionService;

        public FilesController(
            IFileStorageService fileStorageService,
            IProgramService programService,
            IVersionService versionService,
            ILogger<FilesController> logger)
            : base(logger)
        {
            _fileStorageService = fileStorageService;
            _programService = programService;
            _versionService = versionService;
        }

        #region Version File Operations

        /// <summary>
        /// Store files for a specific program version
        /// </summary>
        [HttpPost("programs/{programId}/versions/{versionId}/upload")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("StoreVersionFiles")]
        public async Task<ActionResult<ApiResponse<List<FileStorageResult>>>> StoreVersionFiles(
            string programId,
            string versionId,
            [FromBody] List<VersionFileCreateDto> files,
            CancellationToken cancellationToken = default)
        {
            // Validate programId
            var programIdResult = ParseObjectId(programId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            // Validate versionId
            var versionIdResult = ParseObjectId(versionId, "versionId");
            if (versionIdResult.Result != null) return versionIdResult.Result!;

            if (files == null || !files.Any())
            {
                return ValidationError<List<FileStorageResult>>("At least one file is required");
            }

            // Validate model state
            var validationResult = ValidateModelState<List<FileStorageResult>>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                // Verify program and version exist and are accessible
                await _programService.GetByIdAsync(programId, cancellationToken);
                var version = await _versionService.GetByIdAsync(versionId, cancellationToken);

                // Only allow file uploads to pending versions
                if (version.Status != "pending")
                {
                    throw new InvalidOperationException("Can only upload files to pending versions");
                }

                return await _fileStorageService.StoreFilesAsync(programId, versionId, files, cancellationToken);
            }, $"Store {files.Count} files for program {programId}, version {versionId}");
        }

        /// <summary>
        /// List all files for a specific program version
        /// </summary>
        [HttpGet("programs/{programId}/versions/{versionId}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<List<VersionFileDto>>>> ListVersionFiles(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            // Validate programId
            var programIdResult = ParseObjectId(programId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            // Validate versionId
            var versionIdResult = ParseObjectId(versionId, "versionId");
            if (versionIdResult.Result != null) return versionIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                // Verify program and version exist and are accessible
                await _programService.GetByIdAsync(programId, cancellationToken);
                await _versionService.GetByIdAsync(versionId, cancellationToken);

                return await _fileStorageService.ListVersionFilesAsync(programId, versionId, cancellationToken);
            }, $"List files for program {programId}, version {versionId}");
        }

        /// <summary>
        /// Get file content for a specific program version
        /// </summary>
        [HttpGet("programs/{programId}/versions/{versionId}/files/{*filePath}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<VersionFileDetailDto>>> GetVersionFile(
            string programId,
            string versionId,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            // Validate programId
            var programIdResult = ParseObjectId(programId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            // Validate versionId
            var versionIdResult = ParseObjectId(versionId, "versionId");
            if (versionIdResult.Result != null) return versionIdResult.Result!;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ValidationError<VersionFileDetailDto>("File path is required");
            }

            return await ExecuteAsync(async () =>
            {
                // Verify program and version exist and are accessible
                await _programService.GetByIdAsync(programId, cancellationToken);
                await _versionService.GetByIdAsync(versionId, cancellationToken);

                return await _fileStorageService.GetFileAsync(programId, versionId, filePath, cancellationToken);
            }, $"Get file {filePath} for program {programId}, version {versionId}");
        }

        /// <summary>
        /// Update file content for a specific program version
        /// </summary>
        [HttpPut("programs/{programId}/versions/{versionId}/files/{*filePath}")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("UpdateVersionFile")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateVersionFile(
            string programId,
            string versionId,
            string filePath,
            [FromBody] VersionFileUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate programId
            var programIdResult = ParseObjectId(programId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            // Validate versionId
            var versionIdResult = ParseObjectId(versionId, "versionId");
            if (versionIdResult.Result != null) return versionIdResult.Result!;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ValidationError<string>("File path is required");
            }

            // Validate model state
            var validationResult = ValidateModelState<string>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                // Verify program and version exist and are accessible
                await _programService.GetByIdAsync(programId, cancellationToken);
                var version = await _versionService.GetByIdAsync(versionId, cancellationToken);

                // Only allow file updates to pending versions
                if (version.Status != "pending")
                {
                    throw new InvalidOperationException("Can only update files in pending versions");
                }

                return await _fileStorageService.UpdateFileAsync(
                    programId,
                    versionId,
                    filePath,
                    dto.Content,
                    dto.ContentType ?? "application/octet-stream",
                    cancellationToken);
            }, $"Update file {filePath} for program {programId}, version {versionId}");
        }

        /// <summary>
        /// Delete file from a specific program version
        /// </summary>
        [HttpDelete("programs/{programId}/versions/{versionId}/files/{*filePath}")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("DeleteVersionFile")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteVersionFile(
            string programId,
            string versionId,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            // Validate programId
            var programIdResult = ParseObjectId(programId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            // Validate versionId
            var versionIdResult = ParseObjectId(versionId, "versionId");
            if (versionIdResult.Result != null) return versionIdResult.Result!;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ValidationError<bool>("File path is required");
            }

            return await ExecuteAsync(async () =>
            {
                // Verify program and version exist and are accessible
                await _programService.GetByIdAsync(programId, cancellationToken);
                var version = await _versionService.GetByIdAsync(versionId, cancellationToken);

                // Only allow file deletion from pending versions
                if (version.Status != "pending")
                {
                    throw new InvalidOperationException("Can only delete files from pending versions");
                }

                return await _fileStorageService.DeleteFileAsync(programId, versionId, filePath, cancellationToken);
            }, $"Delete file {filePath} from program {programId}, version {versionId}");
        }

        /// <summary>
        /// Delete all files for a specific program version
        /// </summary>
        [HttpDelete("programs/{programId}/versions/{versionId}")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("DeleteVersionFiles")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteVersionFiles(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            // Validate programId
            var programIdResult = ParseObjectId(programId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            // Validate versionId
            var versionIdResult = ParseObjectId(versionId, "versionId");
            if (versionIdResult.Result != null) return versionIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                // Verify program and version exist and are accessible
                await _programService.GetByIdAsync(programId, cancellationToken);
                var version = await _versionService.GetByIdAsync(versionId, cancellationToken);

                // Only allow deletion from pending versions
                if (version.Status != "pending")
                {
                    throw new InvalidOperationException("Can only delete files from pending versions");
                }

                return await _fileStorageService.DeleteVersionFilesAsync(programId, versionId, cancellationToken);
            }, $"Delete all files from program {programId}, version {versionId}");
        }

        #endregion

        #region Version File Management Operations

        /// <summary>
        /// Copy files from one version to another within the same program
        /// </summary>
        [HttpPost("programs/{programId}/versions/{fromVersionId}/copy-to/{toVersionId}")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("CopyVersionFiles")]
        public async Task<ActionResult<ApiResponse<List<FileStorageResult>>>> CopyVersionFiles(
            string programId,
            string fromVersionId,
            string toVersionId,
            CancellationToken cancellationToken = default)
        {
            // Validate programId
            var programIdResult = ParseObjectId(programId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            // Validate fromVersionId
            var fromVersionIdResult = ParseObjectId(fromVersionId, "fromVersionId");
            if (fromVersionIdResult.Result != null) return fromVersionIdResult.Result!;

            // Validate toVersionId
            var toVersionIdResult = ParseObjectId(toVersionId, "toVersionId");
            if (toVersionIdResult.Result != null) return toVersionIdResult.Result!;

            if (fromVersionId == toVersionId)
            {
                return ValidationError<List<FileStorageResult>>("Source and target versions cannot be the same");
            }

            return await ExecuteAsync(async () =>
            {
                // Verify program and versions exist
                await _programService.GetByIdAsync(programId, cancellationToken);
                await _versionService.GetByIdAsync(fromVersionId, cancellationToken);
                var toVersion = await _versionService.GetByIdAsync(toVersionId, cancellationToken);

                // Only allow copying to pending versions
                if (toVersion.Status != "pending")
                {
                    throw new InvalidOperationException("Can only copy files to pending versions");
                }

                return await _fileStorageService.CopyVersionFilesAsync(programId, fromVersionId, toVersionId, cancellationToken);
            }, $"Copy files from version {fromVersionId} to {toVersionId} in program {programId}");
        }

        #endregion

        #region Program File Management

        /// <summary>
        /// Delete all files for a program (all versions)
        /// </summary>
        [HttpDelete("programs/{programId}")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("DeleteProgramFiles")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteProgramFiles(
            string programId,
            CancellationToken cancellationToken = default)
        {
            // Validate programId
            var programIdResult = ParseObjectId(programId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                // Verify program exists
                await _programService.GetByIdAsync(programId, cancellationToken);

                return await _fileStorageService.DeleteProgramFilesAsync(programId, cancellationToken);
            }, $"Delete all files for program {programId}");
        }

        /// <summary>
        /// Get storage statistics for a program
        /// </summary>
        [HttpGet("programs/{programId}/statistics")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<StorageStatistics>>> GetProgramStorageStatistics(
            string programId,
            CancellationToken cancellationToken = default)
        {
            // Validate programId
            var programIdResult = ParseObjectId(programId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                // Verify program exists
                await _programService.GetByIdAsync(programId, cancellationToken);

                return await _fileStorageService.GetStorageStatisticsAsync(programId, cancellationToken);
            }, $"Get storage statistics for program {programId}");
        }

        #endregion

        #region File Validation and Utility Operations

        /// <summary>
        /// Validate file before upload
        /// </summary>
        [HttpPost("validate")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<FileValidationResult>>> ValidateFile(
            [FromBody] FileValidationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return ValidationError<FileValidationResult>("Validation request is required");
            }

            // Validate model state
            var validationResult = ValidateModelState<FileValidationResult>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _fileStorageService.ValidateFileAsync(
                    request.FileName,
                    request.Content,
                    request.ContentType,
                    cancellationToken);
            }, "Validate file");
        }

        /// <summary>
        /// Calculate file hash
        /// </summary>
        [HttpPost("calculate-hash")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<string>>> CalculateFileHash(
            [FromBody] byte[] content,
            CancellationToken cancellationToken = default)
        {
            if (content == null || content.Length == 0)
            {
                return ValidationError<string>("File content is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await Task.FromResult(_fileStorageService.CalculateFileHash(content));
            }, "Calculate file hash");
        }

        /// <summary>
        /// Get file path structure for a program version
        /// </summary>
        [HttpGet("programs/{programId}/versions/{versionId}/path")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<string>>> GetVersionFilePath(
            string programId,
            string versionId,
            [FromQuery] string filePath = "",
            CancellationToken cancellationToken = default)
        {
            // Validate programId
            var programIdResult = ParseObjectId(programId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            // Validate versionId
            var versionIdResult = ParseObjectId(versionId, "versionId");
            if (versionIdResult.Result != null) return versionIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                // Verify program and version exist
                await _programService.GetByIdAsync(programId, cancellationToken);
                await _versionService.GetByIdAsync(versionId, cancellationToken);

                return await Task.FromResult(_fileStorageService.GetFilePath(programId, versionId, filePath));
            }, $"Get file path for program {programId}, version {versionId}");
        }

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Bulk upload files to a program version
        /// </summary>
        [HttpPost("programs/{programId}/versions/{versionId}/bulk-upload")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("BulkUploadFiles")]
        public async Task<ActionResult<ApiResponse<BulkOperationResult>>> BulkUploadFiles(
            string programId,
            string versionId,
            [FromBody] List<VersionFileCreateDto> files,
            CancellationToken cancellationToken = default)
        {
            // Validate programId
            var programIdResult = ParseObjectId(programId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            // Validate versionId
            var versionIdResult = ParseObjectId(versionId, "versionId");
            if (versionIdResult.Result != null) return versionIdResult.Result!;

            if (files == null || !files.Any())
            {
                return ValidationError<BulkOperationResult>("At least one file is required");
            }

            return await ExecuteAsync(async () =>
            {
                // Verify program and version exist
                await _programService.GetByIdAsync(programId, cancellationToken);
                var version = await _versionService.GetByIdAsync(versionId, cancellationToken);

                // Only allow uploads to pending versions
                if (version.Status != "pending")
                {
                    throw new InvalidOperationException("Can only upload files to pending versions");
                }

                var results = await _fileStorageService.StoreFilesAsync(programId, versionId, files, cancellationToken);

                return new BulkOperationResult
                {
                    SuccessCount = results.Count(r => r.Success),
                    FailureCount = results.Count(r => !r.Success),
                    TotalProcessed = results.Count,
                    Errors = results.Where(r => !r.Success).Select(r => r.ErrorMessage ?? "Unknown error").ToList()
                };
            }, $"Bulk upload {files.Count} files to program {programId}, version {versionId}");
        }

        /// <summary>
        /// Bulk delete files from a program version
        /// </summary>
        [HttpPost("programs/{programId}/versions/{versionId}/bulk-delete")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("BulkDeleteFiles")]
        public async Task<ActionResult<ApiResponse<BulkOperationResult>>> BulkDeleteFiles(
            string programId,
            string versionId,
            [FromBody] List<string> filePaths,
            CancellationToken cancellationToken = default)
        {
            // Validate programId
            var programIdResult = ParseObjectId(programId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            // Validate versionId
            var versionIdResult = ParseObjectId(versionId, "versionId");
            if (versionIdResult.Result != null) return versionIdResult.Result!;

            if (filePaths == null || !filePaths.Any())
            {
                return ValidationError<BulkOperationResult>("At least one file path is required");
            }

            return await ExecuteAsync(async () =>
            {
                // Verify program and version exist
                await _programService.GetByIdAsync(programId, cancellationToken);
                var version = await _versionService.GetByIdAsync(versionId, cancellationToken);

                // Only allow deletion from pending versions
                if (version.Status != "pending")
                {
                    throw new InvalidOperationException("Can only delete files from pending versions");
                }

                int successCount = 0;
                int failureCount = 0;
                var errors = new List<string>();

                foreach (var filePath in filePaths)
                {
                    try
                    {
                        var success = await _fileStorageService.DeleteFileAsync(programId, versionId, filePath, cancellationToken);
                        if (success)
                            successCount++;
                        else
                            failureCount++;
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        errors.Add($"Failed to delete {filePath}: {ex.Message}");
                    }
                }

                return new BulkOperationResult
                {
                    SuccessCount = successCount,
                    FailureCount = failureCount,
                    TotalProcessed = filePaths.Count,
                    Errors = errors
                };
            }, $"Bulk delete {filePaths.Count} files from program {programId}, version {versionId}");
        }

        #endregion
    }
}