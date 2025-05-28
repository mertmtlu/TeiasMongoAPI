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

        public FilesController(
            IFileStorageService fileStorageService,
            ILogger<FilesController> logger)
            : base(logger)
        {
            _fileStorageService = fileStorageService;
        }

        #region File Operations

        /// <summary>
        /// Store files for a program/version
        /// </summary>
        [HttpPost("programs/{programId}/upload")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("StoreFiles")]
        public async Task<ActionResult<ApiResponse<List<FileStorageResult>>>> StoreFiles(
            string programId,
            [FromQuery] string? versionId,
            [FromBody] List<ProgramFileUploadDto> files,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate versionId if provided
            if (!string.IsNullOrEmpty(versionId))
            {
                var versionObjectIdResult = ParseObjectId(versionId, "versionId");
                if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;
            }

            if (files == null || !files.Any())
            {
                return ValidationError<List<FileStorageResult>>("At least one file is required");
            }

            // Validate model state
            var validationResult = ValidateModelState<List<FileStorageResult>>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _fileStorageService.StoreFilesAsync(programId, versionId, files, cancellationToken);
            }, $"Store files for program {programId}");
        }

        /// <summary>
        /// List all files for a program/version
        /// </summary>
        [HttpGet("programs/{programId}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<List<FileMetadata>>>> ListProgramFiles(
            string programId,
            [FromQuery] string? versionId = null,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate versionId if provided
            if (!string.IsNullOrEmpty(versionId))
            {
                var versionObjectIdResult = ParseObjectId(versionId, "versionId");
                if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;
            }

            return await ExecuteAsync(async () =>
            {
                return await _fileStorageService.ListProgramFilesAsync(programId, versionId, cancellationToken);
            }, $"List files for program {programId}");
        }

        /// <summary>
        /// Get file content by program and file path
        /// </summary>
        [HttpGet("programs/{programId}/files/{*filePath}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<ProgramFileContentDto>>> GetFile(
            string programId,
            string filePath,
            [FromQuery] string? versionId = null,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ValidationError<ProgramFileContentDto>("File path is required");
            }

            // Validate versionId if provided
            if (!string.IsNullOrEmpty(versionId))
            {
                var versionObjectIdResult = ParseObjectId(versionId, "versionId");
                if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;
            }

            return await ExecuteAsync(async () =>
            {
                return await _fileStorageService.GetFileAsync(programId, filePath, versionId, cancellationToken);
            }, $"Get file {filePath} for program {programId}");
        }

        /// <summary>
        /// Update file content
        /// </summary>
        [HttpPut("programs/{programId}/files/{*filePath}")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("UpdateFile")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateFile(
            string programId,
            string filePath,
            [FromBody] ProgramFileUpdateDto dto,
            [FromQuery] string? versionId = null,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ValidationError<string>("File path is required");
            }

            // Validate versionId if provided
            if (!string.IsNullOrEmpty(versionId))
            {
                var versionObjectIdResult = ParseObjectId(versionId, "versionId");
                if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;
            }

            // Validate model state
            var validationResult = ValidateModelState<string>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _fileStorageService.UpdateFileAsync(programId, filePath, dto.Content, dto.ContentType ?? "application/octet-stream", versionId, cancellationToken);
            }, $"Update file {filePath} for program {programId}");
        }

        ///// <summary>
        ///// Delete file by program and file path
        ///// </summary>
        //[HttpDelete("programs/{programId}/files/{*filePath}")]
        //[RequirePermission(UserPermissions.UpdatePrograms)]
        //[AuditLog("DeleteFile")]
        //public async Task<ActionResult<ApiResponse<bool>>> DeleteFile(
        //    string programId,
        //    string filePath,
        //    [FromQuery] string? versionId = null,
        //    CancellationToken cancellationToken = default)
        //{
        //    var objectIdResult = ParseObjectId(programId, "programId");
        //    if (objectIdResult.Result != null) return objectIdResult.Result!;

        //    if (string.IsNullOrWhiteSpace(filePath))
        //    {
        //        return ValidationError<bool>("File path is required");
        //    }

        //    // Validate versionId if provided
        //    if (!string.IsNullOrEmpty(versionId))
        //    {
        //        var versionObjectIdResult = ParseObjectId(versionId, "versionId");
        //        if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;
        //    }

        //    return await ExecuteAsync(async () =>
        //    {
        //        // First get the file to get its storage key
        //        var file = await _fileStorageService.GetFileAsync(programId, filePath, versionId, cancellationToken);
        //        if (file?.StorageKey != null)
        //        {
        //            return await _fileStorageService.DeleteFileAsync(file.StorageKey, cancellationToken);
        //        }
        //        return false;
        //    }, $"Delete file {filePath} for program {programId}");
        //}

        #endregion

        #region File Management

        /// <summary>
        /// Get raw file content by storage key
        /// </summary>
        [HttpGet("content/{storageKey}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<byte[]>> GetFileContent(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(storageKey))
            {
                return BadRequest("Storage key is required");
            }

            try
            {
                _logger.LogInformation($"Retrieving file content for storage key: {storageKey}");
                var content = await _fileStorageService.GetFileContentAsync(storageKey, cancellationToken);

                // Get metadata to determine content type
                var metadata = await _fileStorageService.GetFileMetadataAsync(storageKey, cancellationToken);

                return File(content, metadata.ContentType, metadata.FilePath);
            }
            catch (Exception ex)
            {
                return HandleException<byte[]>(ex, $"Get file content for storage key {storageKey}").Result!;
            }
        }

        /// <summary>
        /// Get file metadata by storage key
        /// </summary>
        [HttpGet("metadata/{storageKey}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<FileMetadata>>> GetFileMetadata(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(storageKey))
            {
                return ValidationError<FileMetadata>("Storage key is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await _fileStorageService.GetFileMetadataAsync(storageKey, cancellationToken);
            }, $"Get metadata for storage key {storageKey}");
        }

        /// <summary>
        /// Validate file before storage
        /// </summary>
        [HttpPost("validate")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<FileValidationResult>>> ValidateFile(
            [FromBody] FileValidationRequest request,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<FileValidationResult>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _fileStorageService.ValidateFileAsync(request.FileName, request.Content, request.ContentType, cancellationToken);
            }, "Validate file");
        }

        /// <summary>
        /// Copy files from one version to another
        /// </summary>
        [HttpPost("programs/{programId}/copy-version-files")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("CopyVersionFiles")]
        public async Task<ActionResult<ApiResponse<List<FileStorageResult>>>> CopyVersionFiles(
            string programId,
            [FromQuery] string fromVersionId,
            [FromQuery] string toVersionId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var fromVersionObjectIdResult = ParseObjectId(fromVersionId, "fromVersionId");
            if (fromVersionObjectIdResult.Result != null) return fromVersionObjectIdResult.Result!;

            var toVersionObjectIdResult = ParseObjectId(toVersionId, "toVersionId");
            if (toVersionObjectIdResult.Result != null) return toVersionObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _fileStorageService.CopyVersionFilesAsync(programId, fromVersionId, toVersionId, cancellationToken);
            }, $"Copy version files from {fromVersionId} to {toVersionId} for program {programId}");
        }

        /// <summary>
        /// Get storage statistics for a program
        /// </summary>
        [HttpGet("programs/{programId}/storage-stats")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<StorageStatistics>>> GetStorageStatistics(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _fileStorageService.GetStorageStatisticsAsync(programId, cancellationToken);
            }, $"Get storage statistics for program {programId}");
        }

        /// <summary>
        /// Delete all files for a program
        /// </summary>
        [HttpDelete("programs/{programId}/all")]
        [RequirePermission(UserPermissions.DeletePrograms)]
        [AuditLog("DeleteProgramFiles")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteProgramFiles(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _fileStorageService.DeleteProgramFilesAsync(programId, cancellationToken);
            }, $"Delete all files for program {programId}");
        }

        /// <summary>
        /// Delete file by storage key
        /// </summary>
        [HttpDelete("{storageKey}")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("DeleteFileByKey")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteFileByKey(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(storageKey))
            {
                return ValidationError<bool>("Storage key is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await _fileStorageService.DeleteFileAsync(storageKey, cancellationToken);
            }, $"Delete file by storage key {storageKey}");
        }

        #endregion

        #region Additional Utility Methods

        /// <summary>
        /// Get file path from storage key
        /// </summary>
        [HttpGet("{storageKey}/path")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<string>>> GetFilePath(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(storageKey))
            {
                return ValidationError<string>("Storage key is required");
            }

            return await ExecuteAsync(async () =>
            {
                return await Task.FromResult(_fileStorageService.GetFilePath(storageKey));
            }, $"Get file path for storage key {storageKey}");
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
        /// Get files by hash (detect duplicates)
        /// </summary>
        [HttpGet("by-hash/{hash}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<List<FileMetadata>>>> GetFilesByHash(
            string hash,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return ValidationError<List<FileMetadata>>("Hash is required");
            }

            return await ExecuteAsync(async () =>
            {
                // PLACEHOLDER: This would require a new service method to find files by hash
                // For now, returning empty list
                return new List<FileMetadata>();
            }, $"Get files by hash {hash}");
        }

        /// <summary>
        /// Get recently uploaded files
        /// </summary>
        [HttpGet("recent")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<List<FileMetadata>>>> GetRecentFiles(
            [FromQuery] int count = 10,
            [FromQuery] string? programId = null,
            CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return ValidationError<List<FileMetadata>>("Count must be greater than 0");
            }

            // Validate programId if provided
            if (!string.IsNullOrEmpty(programId))
            {
                var objectIdResult = ParseObjectId(programId, "programId");
                if (objectIdResult.Result != null) return objectIdResult.Result!;
            }

            return await ExecuteAsync(async () =>
            {
                if (!string.IsNullOrEmpty(programId))
                {
                    var files = await _fileStorageService.ListProgramFilesAsync(programId, null, cancellationToken);
                    return files.OrderByDescending(f => f.CreatedAt).Take(count).ToList();
                }
                else
                {
                    // PLACEHOLDER: This would require a new service method to get recent files across all programs
                    return new List<FileMetadata>();
                }
            }, $"Get recent files (count: {count})");
        }

        /// <summary>
        /// Get large files (above size threshold)
        /// </summary>
        [HttpGet("large")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<List<FileMetadata>>>> GetLargeFiles(
            [FromQuery] long sizeThresholdMB = 10,
            [FromQuery] string? programId = null,
            CancellationToken cancellationToken = default)
        {
            if (sizeThresholdMB <= 0)
            {
                return ValidationError<List<FileMetadata>>("Size threshold must be greater than 0");
            }

            // Validate programId if provided
            if (!string.IsNullOrEmpty(programId))
            {
                var objectIdResult = ParseObjectId(programId, "programId");
                if (objectIdResult.Result != null) return objectIdResult.Result!;
            }

            return await ExecuteAsync(async () =>
            {
                var thresholdBytes = sizeThresholdMB * 1024 * 1024; // Convert MB to bytes

                if (!string.IsNullOrEmpty(programId))
                {
                    var files = await _fileStorageService.ListProgramFilesAsync(programId, null, cancellationToken);
                    return files.Where(f => f.Size > thresholdBytes).OrderByDescending(f => f.Size).ToList();
                }
                else
                {
                    // PLACEHOLDER: This would require a new service method to get large files across all programs
                    return new List<FileMetadata>();
                }
            }, $"Get large files (threshold: {sizeThresholdMB}MB)");
        }

        /// <summary>
        /// Get file type statistics
        /// </summary>
        [HttpGet("type-stats")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<Dictionary<string, FileTypeStatistics>>>> GetFileTypeStatistics(
            [FromQuery] string? programId = null,
            CancellationToken cancellationToken = default)
        {
            // Validate programId if provided
            if (!string.IsNullOrEmpty(programId))
            {
                var objectIdResult = ParseObjectId(programId, "programId");
                if (objectIdResult.Result != null) return objectIdResult.Result!;
            }

            return await ExecuteAsync(async () =>
            {
                if (!string.IsNullOrEmpty(programId))
                {
                    var stats = await _fileStorageService.GetStorageStatisticsAsync(programId, cancellationToken);
                    return stats.FileTypeCount.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new FileTypeStatistics
                        {
                            Count = kvp.Value,
                            TotalSize = stats.FileTypeSizes.GetValueOrDefault(kvp.Key, 0),
                            ContentType = kvp.Key
                        });
                }
                else
                {
                    // PLACEHOLDER: This would require aggregating across all programs
                    return new Dictionary<string, FileTypeStatistics>();
                }
            }, "Get file type statistics");
        }

        /// <summary>
        /// Cleanup orphaned files
        /// </summary>
        [HttpPost("cleanup-orphaned")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("CleanupOrphanedFiles")]
        public async Task<ActionResult<ApiResponse<CleanupResult>>> CleanupOrphanedFiles(
            [FromQuery] bool dryRun = true,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                // PLACEHOLDER: This would require implementing orphaned file detection logic
                return new CleanupResult
                {
                    FilesFound = 0,
                    FilesDeleted = 0,
                    SpaceFreedMB = 0,
                    DryRun = dryRun,
                    ExecutedAt = DateTime.UtcNow
                };
            }, $"Cleanup orphaned files (dry run: {dryRun})");
        }

        /// <summary>
        /// Bulk file operations
        /// </summary>
        [HttpPost("bulk-delete")]
        [RequirePermission(UserPermissions.UpdatePrograms)]
        [AuditLog("BulkDeleteFiles")]
        public async Task<ActionResult<ApiResponse<BulkOperationResult>>> BulkDeleteFiles(
            [FromBody] List<string> storageKeys,
            CancellationToken cancellationToken = default)
        {
            if (storageKeys == null || !storageKeys.Any())
            {
                return ValidationError<BulkOperationResult>("At least one storage key is required");
            }

            return await ExecuteAsync(async () =>
            {
                int successCount = 0;
                int failureCount = 0;
                var errors = new List<string>();

                foreach (var storageKey in storageKeys)
                {
                    try
                    {
                        var success = await _fileStorageService.DeleteFileAsync(storageKey, cancellationToken);
                        if (success)
                            successCount++;
                        else
                            failureCount++;
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        errors.Add($"Failed to delete {storageKey}: {ex.Message}");
                    }
                }

                return new BulkOperationResult
                {
                    SuccessCount = successCount,
                    FailureCount = failureCount,
                    TotalProcessed = storageKeys.Count,
                    Errors = errors
                };
            }, $"Bulk delete {storageKeys.Count} files");
        }

        #endregion
    }

    #region Supporting DTOs (Placeholders)

    public class FileValidationRequest
    {
        [Required]
        public required string FileName { get; set; }

        [Required]
        public required byte[] Content { get; set; }

        [Required]
        public required string ContentType { get; set; }
    }

    public class FileTypeStatistics
    {
        public int Count { get; set; }
        public long TotalSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
    }

    public class CleanupResult
    {
        public int FilesFound { get; set; }
        public int FilesDeleted { get; set; }
        public long SpaceFreedMB { get; set; }
        public bool DryRun { get; set; }
        public DateTime ExecutedAt { get; set; }
    }

    public class BulkOperationResult
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int TotalProcessed { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    #endregion
}