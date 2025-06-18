using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
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

        public async Task<string> StoreFileAsync(string programId, string versionId, string filePath, byte[] content, string contentType, CancellationToken cancellationToken = default)
        {
            try
            {
                // Generate storage key with new structure
                var storageKey = GenerateStorageKey(programId, versionId, filePath);
                var physicalPath = GetFilePath(programId, versionId, filePath);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(physicalPath);
                if (directory != null)
                {
                    EnsureDirectoryExists(directory);
                }

                // Store file
                await File.WriteAllBytesAsync(physicalPath, content, cancellationToken);

                // Store metadata
                await StoreFileMetadataAsync(storageKey, programId, versionId, filePath, content, contentType);

                _logger.LogInformation("Stored file {FilePath} for program {ProgramId}, version {VersionId}",
                    filePath, programId, versionId);

                return storageKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store file {FilePath} for program {ProgramId}, version {VersionId}",
                    filePath, programId, versionId);
                throw;
            }
        }

        public async Task<List<FileStorageResult>> StoreFilesAsync(string programId, string versionId, List<VersionFileCreateDto> files, CancellationToken cancellationToken = default)
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

                    // Store file
                    var storageKey = await StoreFileAsync(programId, versionId, file.Path, file.Content, file.ContentType, cancellationToken);
                    var hash = CalculateFileHash(file.Content);

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
                    _logger.LogError(ex, "Failed to store file {FilePath} for program {ProgramId}, version {VersionId}",
                        file.Path, programId, versionId);
                    results.Add(new FileStorageResult
                    {
                        FilePath = file.Path,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            _logger.LogInformation("Stored {SuccessCount}/{TotalCount} files for program {ProgramId}, version {VersionId}",
                results.Count(r => r.Success), results.Count, programId, versionId);

            return results;
        }

        public async Task<byte[]> GetFileContentAsync(string programId, string versionId, string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var physicalPath = GetFilePath(programId, versionId, filePath);

                if (!File.Exists(physicalPath))
                {
                    throw new FileNotFoundException($"File not found: {filePath} in program {programId}, version {versionId}");
                }

                return await File.ReadAllBytesAsync(physicalPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get file content for {FilePath} in program {ProgramId}, version {VersionId}",
                    filePath, programId, versionId);
                throw;
            }
        }

        public async Task<VersionFileDetailDto> GetFileAsync(string programId, string versionId, string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var content = await GetFileContentAsync(programId, versionId, filePath, cancellationToken);
                var storageKey = GenerateStorageKey(programId, versionId, filePath);
                var metadata = await GetFileMetadataAsync(storageKey, cancellationToken);

                return new VersionFileDetailDto
                {
                    Path = filePath,
                    StorageKey = storageKey,
                    Hash = metadata.Hash,
                    Size = metadata.Size,
                    FileType = DetermineFileType(filePath),
                    ContentType = metadata.ContentType,
                    Content = content,
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

        public async Task<string> UpdateFileAsync(string programId, string versionId, string filePath, byte[] content, string contentType, CancellationToken cancellationToken = default)
        {
            try
            {
                var storageKey = GenerateStorageKey(programId, versionId, filePath);
                var physicalPath = GetFilePath(programId, versionId, filePath);

                // Store updated content
                await File.WriteAllBytesAsync(physicalPath, content, cancellationToken);

                // Update metadata
                await StoreFileMetadataAsync(storageKey, programId, versionId, filePath, content, contentType);

                _logger.LogInformation("Updated file {FilePath} for program {ProgramId}, version {VersionId}",
                    filePath, programId, versionId);

                return storageKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update file {FilePath} for program {ProgramId}, version {VersionId}",
                    filePath, programId, versionId);
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string programId, string versionId, string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var physicalPath = GetFilePath(programId, versionId, filePath);
                var storageKey = GenerateStorageKey(programId, versionId, filePath);

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

                _logger.LogInformation("Deleted file {FilePath} from program {ProgramId}, version {VersionId}",
                    filePath, programId, versionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file {FilePath} from program {ProgramId}, version {VersionId}",
                    filePath, programId, versionId);
                return false;
            }
        }

        public async Task<List<VersionFileDto>> ListVersionFilesAsync(string programId, string versionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var files = new List<VersionFileDto>();
                var versionPath = Path.Combine(_settings.BasePath, programId, versionId, "files");

                if (!Directory.Exists(versionPath))
                {
                    return files;
                }

                var metadataFiles = Directory.GetFiles(versionPath, "*.metadata", SearchOption.AllDirectories);

                foreach (var metadataFile in metadataFiles)
                {
                    try
                    {
                        var metadataJson = await File.ReadAllTextAsync(metadataFile, cancellationToken);
                        var metadata = System.Text.Json.JsonSerializer.Deserialize<FileMetadata>(metadataJson);
                        if (metadata != null)
                        {
                            files.Add(new VersionFileDto
                            {
                                Path = metadata.FilePath,
                                StorageKey = metadata.StorageKey,
                                Hash = metadata.Hash,
                                Size = metadata.Size,
                                FileType = DetermineFileType(metadata.FilePath),
                                ContentType = metadata.ContentType
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read metadata file {MetadataFile}", metadataFile);
                    }
                }

                return files.OrderBy(f => f.Path).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list files for program {ProgramId}, version {VersionId}", programId, versionId);
                throw;
            }
        }

        public async Task<bool> DeleteVersionFilesAsync(string programId, string versionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var versionPath = Path.Combine(_settings.BasePath, programId, versionId);

                if (Directory.Exists(versionPath))
                {
                    Directory.Delete(versionPath, recursive: true);
                    _logger.LogInformation("Deleted all files for program {ProgramId}, version {VersionId}", programId, versionId);
                    return true;
                }

                return true; // No files to delete
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete files for program {ProgramId}, version {VersionId}", programId, versionId);
                return false;
            }
        }

        public async Task<List<FileStorageResult>> CopyVersionFilesAsync(string programId, string fromVersionId, string toVersionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var results = new List<FileStorageResult>();
                var sourceFiles = await ListVersionFilesAsync(programId, fromVersionId, cancellationToken);

                foreach (var sourceFile in sourceFiles)
                {
                    try
                    {
                        var content = await GetFileContentAsync(programId, fromVersionId, sourceFile.Path, cancellationToken);
                        var storageKey = await StoreFileAsync(programId, toVersionId, sourceFile.Path, content, sourceFile.ContentType, cancellationToken);

                        results.Add(new FileStorageResult
                        {
                            FilePath = sourceFile.Path,
                            StorageKey = storageKey,
                            Hash = sourceFile.Hash,
                            Size = sourceFile.Size,
                            ContentType = sourceFile.ContentType,
                            Success = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to copy file {FilePath} from version {FromVersion} to {ToVersion}",
                            sourceFile.Path, fromVersionId, toVersionId);

                        results.Add(new FileStorageResult
                        {
                            FilePath = sourceFile.Path,
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

        public async Task<bool> DeleteProgramFilesAsync(string programId, CancellationToken cancellationToken = default)
        {
            try
            {
                var programPath = Path.Combine(_settings.BasePath, programId);

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

        public async Task<StorageStatistics> GetStorageStatisticsAsync(string programId, CancellationToken cancellationToken = default)
        {
            try
            {
                var stats = new StorageStatistics { ProgramId = programId };
                var programPath = Path.Combine(_settings.BasePath, programId);

                if (!Directory.Exists(programPath))
                {
                    return stats;
                }

                // Count versions
                var versionDirectories = Directory.GetDirectories(programPath);
                stats.VersionCount = versionDirectories.Length;

                // Aggregate file statistics across all versions
                var allFiles = new List<VersionFileDto>();
                foreach (var versionDir in versionDirectories)
                {
                    var versionId = Path.GetFileName(versionDir);
                    try
                    {
                        var versionFiles = await ListVersionFilesAsync(programId, versionId, cancellationToken);
                        allFiles.AddRange(versionFiles);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get files for version {VersionId}", versionId);
                    }
                }

                stats.TotalFiles = allFiles.Count;
                stats.TotalSize = allFiles.Sum(f => f.Size);
                stats.LastModified = allFiles.Any() ? DateTime.UtcNow : DateTime.MinValue; // Would need proper tracking

                // Group by file types
                foreach (var file in allFiles)
                {
                    var extension = Path.GetExtension(file.Path).ToLowerInvariant();
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

                // Content-based validation
                if (content.Length == 0)
                {
                    result.Warnings.Add("File is empty");
                }

                // Suggest content type if needed
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

        public string CalculateFileHash(byte[] content)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(content);
            return Convert.ToBase64String(hash);
        }

        public string GetFilePath(string programId, string versionId, string filePath)
        {
            var normalizedPath = NormalizeFilePath(filePath);
            return Path.Combine(_settings.BasePath, programId, versionId, "files", normalizedPath);
        }

        #region Private Helper Methods

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private string GenerateStorageKey(string programId, string versionId, string filePath)
        {
            var normalizedPath = NormalizeFilePath(filePath);
            return Path.Combine(programId, versionId, "files", normalizedPath).Replace('\\', '/');
        }

        private string NormalizeFilePath(string filePath)
        {
            return filePath.Replace('\\', '/').Trim('/');
        }

        private string GetMetadataPath(string storageKey)
        {
            return Path.Combine(_settings.BasePath, storageKey + ".metadata");
        }

        private async Task<FileMetadata> GetFileMetadataAsync(string storageKey, CancellationToken cancellationToken)
        {
            var metadataPath = GetMetadataPath(storageKey);

            if (File.Exists(metadataPath))
            {
                var metadataJson = await File.ReadAllTextAsync(metadataPath, cancellationToken);
                var metadata = System.Text.Json.JsonSerializer.Deserialize<FileMetadata>(metadataJson);
                if (metadata != null)
                {
                    return metadata;
                }
            }

            throw new FileNotFoundException($"Metadata not found for storage key: {storageKey}");
        }

        private async Task StoreFileMetadataAsync(string storageKey, string programId, string versionId, string filePath, byte[] content, string contentType)
        {
            var metadata = new FileMetadata
            {
                StorageKey = storageKey,
                ProgramId = programId,
                VersionId = versionId,
                FilePath = filePath,
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

        private string DetermineFileType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".cs" or ".py" or ".js" or ".ts" or ".java" or ".cpp" or ".c" or ".rs" => "source",
                ".json" or ".xml" or ".yaml" or ".yml" or ".config" => "config",
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" => "asset",
                ".exe" or ".dll" or ".so" or ".dylib" => "build_artifact",
                _ => "source"
            };
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
        public List<string> BlockedExtensions { get; set; } = new() { ".bat", ".cmd", ".scr", ".vbs", ".ps1" };
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