using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using AutoMapper;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class FileStorageService : BaseService, IFileStorageService
    {
        private readonly FileStorageSettings _settings;

        public FileStorageService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IOptions<FileStorageSettings> settings,
            ILogger<FileStorageService> logger) : base(unitOfWork, mapper, logger) 
        {
            _settings = settings.Value;

            // Ensure base directory exists
            EnsureDirectoryExists(_settings.BasePath);
        }

        public async Task<string> StoreFileAsync(string programId, string filePath, byte[] content, string contentType, CancellationToken cancellationToken = default)
        {
            try
            {
                // Generate storage key
                var storageKey = GenerateStorageKey(programId, filePath);
                var physicalPath = GetFilePath(storageKey);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(physicalPath);
                if (directory != null)
                {
                    EnsureDirectoryExists(directory);
                }

                // Store file
                await File.WriteAllBytesAsync(physicalPath, content, cancellationToken);

                // Store metadata
                await StoreFileMetadataAsync(storageKey, programId, filePath, null, content, contentType);

                _logger.LogInformation("Stored file {FilePath} for program {ProgramId} with storage key {StorageKey}",
                    filePath, programId, storageKey);

                return storageKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store file {FilePath} for program {ProgramId}", filePath, programId);
                throw;
            }
        }

        public async Task<List<FileStorageResult>> StoreFilesAsync(string programId, string? versionId, List<ProgramFileUploadDto> files, CancellationToken cancellationToken = default)
        {
            var results = new List<FileStorageResult>();

            foreach (var file in files)
            {
                try
                {
                    // Validate file
                    var validation = await ValidateFileAsync(file.Path, file.Content, file.ContentType, cancellationToken);
                    if (!validation.IsValid)
                    {
                        results.Add(new FileStorageResult
                        {
                            FilePath = file.Path,
                            Success = false,
                            ErrorMessage = string.Join(", ", validation.Errors)
                        });
                        continue;
                    }

                    // Generate storage key for version-specific file
                    var storageKey = versionId != null
                        ? GenerateVersionStorageKey(programId, versionId, file.Path)
                        : GenerateStorageKey(programId, file.Path);

                    var physicalPath = GetFilePath(storageKey);

                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(physicalPath);
                    if (directory != null)
                    {
                        EnsureDirectoryExists(directory);
                    }

                    // Store file
                    await File.WriteAllBytesAsync(physicalPath, file.Content, cancellationToken);

                    // Calculate hash and store metadata
                    var hash = CalculateFileHash(file.Content);
                    await StoreFileMetadataAsync(storageKey, programId, file.Path, versionId, file.Content, file.ContentType);

                    results.Add(new FileStorageResult
                    {
                        FilePath = file.Path,
                        StorageKey = storageKey,
                        Hash = hash,
                        Size = file.Content.Length,
                        ContentType = file.ContentType,
                        Success = true
                    });

                    _logger.LogDebug("Stored file {FilePath} for program {ProgramId}, version {VersionId}",
                        file.Path, programId, versionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store file {FilePath} for program {ProgramId}", file.Path, programId);
                    results.Add(new FileStorageResult
                    {
                        FilePath = file.Path,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            _logger.LogInformation("Stored {SuccessCount}/{TotalCount} files for program {ProgramId}",
                results.Count(r => r.Success), results.Count, programId);

            return results;
        }

        public async Task<byte[]> GetFileContentAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var physicalPath = GetFilePath(storageKey);

                if (!File.Exists(physicalPath))
                {
                    throw new FileNotFoundException($"File not found for storage key: {storageKey}");
                }

                return await File.ReadAllBytesAsync(physicalPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get file content for storage key {StorageKey}", storageKey);
                throw;
            }
        }

        public async Task<ProgramFileContentDto> GetFileAsync(string programId, string filePath, string? versionId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var storageKey = versionId != null
                    ? GenerateVersionStorageKey(programId, versionId, filePath)
                    : GenerateStorageKey(programId, filePath);

                var content = await GetFileContentAsync(storageKey, cancellationToken);
                var metadata = await GetFileMetadataAsync(storageKey, cancellationToken);

                return new ProgramFileContentDto
                {
                    Path = filePath,
                    ContentType = metadata.ContentType,
                    Content = content,
                    Description = null, // Could be enhanced with metadata
                    LastModified = metadata.LastModified
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get file {FilePath} for program {ProgramId}, version {VersionId}",
                    filePath, programId, versionId);
                throw;
            }
        }

        public async Task<string> UpdateFileAsync(string programId, string filePath, byte[] content, string contentType, string? versionId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // For updates, we typically create a new version, but for now we'll overwrite
                var storageKey = versionId != null
                    ? GenerateVersionStorageKey(programId, versionId, filePath)
                    : GenerateStorageKey(programId, filePath);

                var physicalPath = GetFilePath(storageKey);

                // Store updated content
                await File.WriteAllBytesAsync(physicalPath, content, cancellationToken);

                // Update metadata
                await StoreFileMetadataAsync(storageKey, programId, filePath, versionId, content, contentType);

                _logger.LogInformation("Updated file {FilePath} for program {ProgramId}", filePath, programId);

                return storageKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update file {FilePath} for program {ProgramId}", filePath, programId);
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var physicalPath = GetFilePath(storageKey);

                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                }

                // Delete metadata file
                var metadataPath = GetMetadataPath(storageKey);
                if (File.Exists(metadataPath))
                {
                    File.Delete(metadataPath);
                }

                _logger.LogInformation("Deleted file with storage key {StorageKey}", storageKey);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file with storage key {StorageKey}", storageKey);
                return false;
            }
        }

        public async Task<bool> DeleteProgramFilesAsync(string programId, CancellationToken cancellationToken = default)
        {
            try
            {
                var programPath = Path.Combine(_settings.BasePath, "programs", programId);

                if (Directory.Exists(programPath))
                {
                    Directory.Delete(programPath, recursive: true);
                    _logger.LogInformation("Deleted all files for program {ProgramId}", programId);
                    return true;
                }

                return true; // No files to delete
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete files for program {ProgramId}", programId);
                return false;
            }
        }

        public async Task<FileMetadata> GetFileMetadataAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var metadataPath = GetMetadataPath(storageKey);
                var physicalPath = GetFilePath(storageKey);

                if (File.Exists(metadataPath))
                {
                    var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<FileMetadata>(metadataJson);
                    if (metadata != null)
                    {
                        metadata.Exists = File.Exists(physicalPath);
                        return metadata;
                    }
                }

                // Fallback: create metadata from file if it exists
                if (File.Exists(physicalPath))
                {
                    var fileInfo = new FileInfo(physicalPath);
                    return new FileMetadata
                    {
                        StorageKey = storageKey,
                        Size = fileInfo.Length,
                        CreatedAt = fileInfo.CreationTime,
                        LastModified = fileInfo.LastWriteTime,
                        Exists = true,
                        ContentType = "application/octet-stream"
                    };
                }

                throw new FileNotFoundException($"File not found for storage key: {storageKey}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get metadata for storage key {StorageKey}", storageKey);
                throw;
            }
        }

        public async Task<List<FileMetadata>> ListProgramFilesAsync(string programId, string? versionId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var files = new List<FileMetadata>();
                var searchPath = versionId != null
                    ? Path.Combine(_settings.BasePath, "programs", programId, "source", versionId, "files")
                    : Path.Combine(_settings.BasePath, "programs", programId);

                if (!Directory.Exists(searchPath))
                {
                    return files;
                }

                var metadataFiles = Directory.GetFiles(searchPath, "*.metadata", SearchOption.AllDirectories);

                foreach (var metadataFile in metadataFiles)
                {
                    try
                    {
                        var metadataJson = await File.ReadAllTextAsync(metadataFile, cancellationToken);
                        var metadata = System.Text.Json.JsonSerializer.Deserialize<FileMetadata>(metadataJson);
                        if (metadata != null)
                        {
                            var physicalPath = GetFilePath(metadata.StorageKey);
                            metadata.Exists = File.Exists(physicalPath);
                            files.Add(metadata);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read metadata file {MetadataFile}", metadataFile);
                    }
                }

                return files.OrderBy(f => f.FilePath).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list files for program {ProgramId}, version {VersionId}", programId, versionId);
                throw;
            }
        }

        public async Task<FileValidationResult> ValidateFileAsync(string fileName, byte[] content, string contentType, CancellationToken cancellationToken = default)
        {
            var result = new FileValidationResult { IsValid = true };

            try
            {
                // File size validation
                if (content.Length > _settings.MaxFileSizeBytes)
                {
                    result.Errors.Add($"File size ({content.Length:N0} bytes) exceeds maximum allowed size ({_settings.MaxFileSizeBytes:N0} bytes)");
                    result.IsValid = false;
                }

                // File extension validation
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                if (!string.IsNullOrEmpty(extension) && _settings.BlockedExtensions.Contains(extension))
                {
                    result.Errors.Add($"File extension '{extension}' is not allowed");
                    result.IsValid = false;
                }

                // Content type validation
                if (!_settings.AllowedContentTypes.Contains(contentType.ToLowerInvariant()))
                {
                    result.Warnings.Add($"Content type '{contentType}' is not in the allowed list");
                    // Not marking as invalid, just a warning
                }

                // File name validation
                if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > _settings.MaxFileNameLength)
                {
                    result.Errors.Add("Invalid file name or name too long");
                    result.IsValid = false;
                }

                // Path traversal check
                if (fileName.Contains("..") || fileName.Contains("~"))
                {
                    result.Errors.Add("File path contains invalid characters");
                    result.IsValid = false;
                }

                // Content-based validation (basic)
                if (content.Length == 0)
                {
                    result.Warnings.Add("File is empty");
                }

                // Suggest content type based on extension if needed
                if (string.IsNullOrEmpty(contentType) || contentType == "application/octet-stream")
                {
                    result.SuggestedContentType = GetContentTypeFromExtension(extension);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate file {FileName}", fileName);
                result.IsValid = false;
                result.Errors.Add("Validation failed due to internal error");
                return result;
            }
        }

        public string GetFilePath(string storageKey)
        {
            return Path.Combine(_settings.BasePath, storageKey);
        }

        public string CalculateFileHash(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(content);
            return Convert.ToBase64String(hash);
        }

        public async Task<List<FileStorageResult>> CopyVersionFilesAsync(string programId, string fromVersionId, string toVersionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var results = new List<FileStorageResult>();
                var sourceFiles = await ListProgramFilesAsync(programId, fromVersionId, cancellationToken);

                foreach (var sourceFile in sourceFiles)
                {
                    try
                    {
                        var content = await GetFileContentAsync(sourceFile.StorageKey, cancellationToken);
                        var newStorageKey = GenerateVersionStorageKey(programId, toVersionId, sourceFile.FilePath);
                        var physicalPath = GetFilePath(newStorageKey);

                        // Ensure directory exists
                        var directory = Path.GetDirectoryName(physicalPath);
                        if (directory != null)
                        {
                            EnsureDirectoryExists(directory);
                        }

                        // Copy file
                        await File.WriteAllBytesAsync(physicalPath, content, cancellationToken);

                        // Create metadata for new version
                        await StoreFileMetadataAsync(newStorageKey, programId, sourceFile.FilePath, toVersionId, content, sourceFile.ContentType);

                        results.Add(new FileStorageResult
                        {
                            FilePath = sourceFile.FilePath,
                            StorageKey = newStorageKey,
                            Hash = sourceFile.Hash,
                            Size = sourceFile.Size,
                            ContentType = sourceFile.ContentType,
                            Success = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to copy file {FilePath} from version {FromVersion} to {ToVersion}",
                            sourceFile.FilePath, fromVersionId, toVersionId);

                        results.Add(new FileStorageResult
                        {
                            FilePath = sourceFile.FilePath,
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                    }
                }

                _logger.LogInformation("Copied {SuccessCount}/{TotalCount} files from version {FromVersion} to {ToVersion} for program {ProgramId}",
                    results.Count(r => r.Success), results.Count, fromVersionId, toVersionId, programId);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy files from version {FromVersion} to {ToVersion} for program {ProgramId}",
                    fromVersionId, toVersionId, programId);
                throw;
            }
        }

        public async Task<StorageStatistics> GetStorageStatisticsAsync(string programId, CancellationToken cancellationToken = default)
        {
            try
            {
                var files = await ListProgramFilesAsync(programId, null, cancellationToken);
                var stats = new StorageStatistics
                {
                    ProgramId = programId,
                    TotalFiles = files.Count,
                    TotalSize = files.Sum(f => f.Size),
                    LastModified = files.Any() ? files.Max(f => f.LastModified) : DateTime.MinValue
                };

                // Count versions
                var versionPath = Path.Combine(_settings.BasePath, "programs", programId, "source");
                if (Directory.Exists(versionPath))
                {
                    stats.VersionCount = Directory.GetDirectories(versionPath).Length;
                }

                // Group by file types
                foreach (var file in files)
                {
                    var extension = Path.GetExtension(file.FilePath).ToLowerInvariant();
                    if (string.IsNullOrEmpty(extension)) extension = "no-extension";

                    stats.FileTypeCount[extension] = stats.FileTypeCount.GetValueOrDefault(extension, 0) + 1;
                    stats.FileTypeSizes[extension] = stats.FileTypeSizes.GetValueOrDefault(extension, 0) + file.Size;
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get storage statistics for program {ProgramId}", programId);
                throw;
            }
        }

        #region Private Helper Methods

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private string GenerateStorageKey(string programId, string filePath)
        {
            // Format: programs/{programId}/files/{normalizedFilePath}
            var normalizedPath = NormalizeFilePath(filePath);
            return Path.Combine("programs", programId, "files", normalizedPath).Replace('\\', '/');
        }

        private string GenerateVersionStorageKey(string programId, string versionId, string filePath)
        {
            // Format: programs/{programId}/source/{versionId}/files/{normalizedFilePath}
            var normalizedPath = NormalizeFilePath(filePath);
            return Path.Combine("programs", programId, "source", versionId, "files", normalizedPath).Replace('\\', '/');
        }

        private string NormalizeFilePath(string filePath)
        {
            // Remove invalid characters and normalize path separators
            return filePath.Replace('\\', '/').Trim('/');
        }

        private string GetMetadataPath(string storageKey)
        {
            return Path.Combine(_settings.BasePath, storageKey + ".metadata");
        }

        private async Task StoreFileMetadataAsync(string storageKey, string programId, string filePath, string? versionId, byte[] content, string contentType)
        {
            var metadata = new FileMetadata
            {
                StorageKey = storageKey,
                ProgramId = programId,
                FilePath = filePath,
                VersionId = versionId,
                Hash = CalculateFileHash(content),
                Size = content.Length,
                ContentType = contentType,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                Exists = true
            };

            var metadataPath = GetMetadataPath(storageKey);
            var directory = Path.GetDirectoryName(metadataPath);
            if (directory != null)
            {
                EnsureDirectoryExists(directory);
            }

            var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(metadataPath, metadataJson);
        }

        private string GetContentTypeFromExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".py" => "text/x-python",
                ".cs" => "text/x-csharp",
                ".cpp" => "text/x-c++src",
                ".c" => "text/x-csrc",
                ".java" => "text/x-java-source",
                ".rs" => "text/x-rust",
                ".md" => "text/markdown",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                ".tar" => "application/x-tar",
                ".gz" => "application/gzip",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        #endregion
    }

    public class FileStorageSettings
    {
        public string BasePath { get; set; } = "./storage";
        public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100 MB
        public int MaxFileNameLength { get; set; } = 255;
        public List<string> BlockedExtensions { get; set; } = new() { ".exe", ".bat", ".cmd", ".scr", ".vbs", ".ps1" };
        public List<string> AllowedContentTypes { get; set; } = new()
        {
            "text/plain", "text/html", "text/css", "text/javascript", "text/x-python", "text/x-csharp",
            "text/x-c++src", "text/x-csrc", "text/x-java-source", "text/x-rust", "text/markdown",
            "application/json", "application/xml", "application/javascript", "application/pdf",
            "application/zip", "application/x-tar", "application/gzip", "application/octet-stream",
            "image/png", "image/jpeg", "image/gif", "image/svg+xml"
        };
    }
}