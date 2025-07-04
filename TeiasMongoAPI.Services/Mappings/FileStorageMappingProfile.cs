using AutoMapper;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.Mappings
{
    public class FileStorageMappingProfile : Profile
    {
        public FileStorageMappingProfile()
        {
            // File upload/creation mappings
            CreateMap<ProgramFileUploadDto, FileMetadata>()
                .ForMember(dest => dest.StorageKey, opt => opt.Ignore()) // Generated in service
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => src.Path))
                .ForMember(dest => dest.VersionId, opt => opt.Ignore()) // Set in service if applicable
                .ForMember(dest => dest.Hash, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Content.Length))
                .ForMember(dest => dest.ContentType, opt => opt.MapFrom(src => src.ContentType))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Exists, opt => opt.MapFrom(src => true));

            CreateMap<VersionFileCreateDto, FileMetadata>()
                .ForMember(dest => dest.StorageKey, opt => opt.Ignore()) // Generated in service
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => src.Path))
                .ForMember(dest => dest.VersionId, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.Hash, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Content.Length))
                .ForMember(dest => dest.ContentType, opt => opt.MapFrom(src => src.ContentType))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Exists, opt => opt.MapFrom(src => true));

            CreateMap<UiComponentAssetUploadDto, FileMetadata>()
                .ForMember(dest => dest.StorageKey, opt => opt.Ignore()) // Generated in service
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore()) // Set in service (component-related)
                .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => src.Path))
                .ForMember(dest => dest.VersionId, opt => opt.Ignore()) // Not applicable for components
                .ForMember(dest => dest.Hash, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Content.Length))
                .ForMember(dest => dest.ContentType, opt => opt.MapFrom(src => src.ContentType))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Exists, opt => opt.MapFrom(src => true));

            // File metadata to response mappings
            CreateMap<FileMetadata, ProgramFileDto>()
                .ForMember(dest => dest.Path, opt => opt.MapFrom(src => src.FilePath))
                .ForMember(dest => dest.Hash, opt => opt.MapFrom(src => src.Hash))
                .ForMember(dest => dest.Description, opt => opt.Ignore()); // Set separately if available

            CreateMap<FileMetadata, VersionFileDto>()
                .ForMember(dest => dest.Path, opt => opt.MapFrom(src => src.FilePath))
                .ForMember(dest => dest.StorageKey, opt => opt.MapFrom(src => src.StorageKey))
                .ForMember(dest => dest.Hash, opt => opt.MapFrom(src => src.Hash))
                .ForMember(dest => dest.FileType, opt => opt.MapFrom(src => GetFileTypeFromPath(src.FilePath)))
                .ForMember(dest => dest.ContentType, opt => opt.MapFrom(src => src.ContentType));

            CreateMap<FileMetadata, UiComponentAssetDto>()
                .ForMember(dest => dest.Path, opt => opt.MapFrom(src => src.FilePath))
                .ForMember(dest => dest.AssetType, opt => opt.MapFrom(src => GetAssetTypeFromContentType(src.ContentType)))
                .ForMember(dest => dest.Url, opt => opt.Ignore()) // Generated in service
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => src.LastModified));

            // File content mappings
            CreateMap<FileMetadata, ProgramFileContentDto>()
                .ForMember(dest => dest.Path, opt => opt.MapFrom(src => src.FilePath))
                .ForMember(dest => dest.Content, opt => opt.Ignore()) // Loaded separately from storage
                .ForMember(dest => dest.Description, opt => opt.Ignore()) // Set separately if available
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => src.LastModified));

            CreateMap<FileMetadata, VersionFileDetailDto>()
                .ForMember(dest => dest.Path, opt => opt.MapFrom(src => src.FilePath))
                .ForMember(dest => dest.StorageKey, opt => opt.MapFrom(src => src.StorageKey))
                .ForMember(dest => dest.Hash, opt => opt.MapFrom(src => src.Hash))
                .ForMember(dest => dest.FileType, opt => opt.MapFrom(src => GetFileTypeFromPath(src.FilePath)))
                .ForMember(dest => dest.ContentType, opt => opt.MapFrom(src => src.ContentType))
                .ForMember(dest => dest.Content, opt => opt.Ignore()) // Loaded separately from storage
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => src.LastModified));


            // File storage results
            CreateMap<FileMetadata, FileStorageResult>()
                .ForMember(dest => dest.FilePath, opt => opt.MapFrom(src => src.FilePath))
                .ForMember(dest => dest.StorageKey, opt => opt.MapFrom(src => src.StorageKey))
                .ForMember(dest => dest.Hash, opt => opt.MapFrom(src => src.Hash))
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Size))
                .ForMember(dest => dest.ContentType, opt => opt.MapFrom(src => src.ContentType))
                .ForMember(dest => dest.Success, opt => opt.MapFrom(src => src.Exists))
                .ForMember(dest => dest.ErrorMessage, opt => opt.MapFrom(src => src.Exists ? null : "File not found"));

            // Storage statistics mappings
            CreateMap<List<FileMetadata>, StorageStatistics>()
                .ConvertUsing(src => new StorageStatistics
                {
                    ProgramId = src.Any() ? src.First().ProgramId : string.Empty,
                    TotalFiles = src.Count,
                    TotalSize = src.Sum(f => f.Size),
                    VersionCount = src.Where(f => !string.IsNullOrEmpty(f.VersionId)).Select(f => f.VersionId).Distinct().Count(),
                    LastModified = src.Any() ? src.Max(f => f.LastModified) : DateTime.MinValue,
                    FileTypeCount = src.GroupBy(f => GetFileExtensionFromPath(f.FilePath))
                                      .ToDictionary(g => g.Key, g => g.Count()),
                    FileTypeSizes = src.GroupBy(f => GetFileExtensionFromPath(f.FilePath))
                                      .ToDictionary(g => g.Key, g => g.Sum(f => f.Size))
                });

            // File validation mappings
            CreateMap<ProgramFileUploadDto, FileValidationResult>()
                .ConvertUsing(src => ValidateFile(src.Path, src.Content, src.ContentType));

            CreateMap<VersionFileCreateDto, FileValidationResult>()
                .ConvertUsing(src => ValidateFile(src.Path, src.Content, src.ContentType));

            CreateMap<UiComponentAssetUploadDto, FileValidationResult>()
                .ConvertUsing(src => ValidateFile(src.Path, src.Content, src.ContentType));

            // File update mappings
            CreateMap<ProgramFileUpdateDto, FileMetadata>()
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Content.Length))
                .ForMember(dest => dest.ContentType, opt => opt.MapFrom(src => src.ContentType ?? "application/octet-stream"))
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Hash, opt => opt.Ignore()) // Recalculated in service
                .ForMember(dest => dest.StorageKey, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.FilePath, opt => opt.Ignore())
                .ForMember(dest => dest.VersionId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Exists, opt => opt.Ignore());

            CreateMap<VersionFileUpdateDto, FileMetadata>()
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => src.Content.Length))
                .ForMember(dest => dest.ContentType, opt => opt.MapFrom(src => src.ContentType ?? "application/octet-stream"))
                .ForMember(dest => dest.LastModified, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Hash, opt => opt.Ignore()) // Recalculated in service
                .ForMember(dest => dest.StorageKey, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.FilePath, opt => opt.Ignore())
                .ForMember(dest => dest.VersionId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Exists, opt => opt.Ignore());
        }

        private string GetFileTypeFromPath(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".cs" or ".js" or ".ts" or ".py" or ".cpp" or ".c" or ".h" or ".java" => "source",
                ".html" or ".css" or ".scss" or ".less" => "web",
                ".json" or ".xml" or ".yml" or ".yaml" or ".config" => "config",
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".ico" => "asset",
                ".dll" or ".exe" or ".so" or ".dylib" => "build_artifact",
                ".md" or ".txt" or ".rst" => "documentation",
                _ => "other"
            };
        }

        private string GetAssetTypeFromContentType(string contentType)
        {
            return contentType.ToLowerInvariant() switch
            {
                var ct when ct.StartsWith("image/") => "image",
                "application/javascript" or "text/javascript" => "js",
                "text/css" => "css",
                "text/html" => "html",
                _ => "file"
            };
        }

        private string GetFileExtensionFromPath(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return string.IsNullOrEmpty(extension) ? "no-extension" : extension.TrimStart('.').ToLowerInvariant();
        }

        private FileValidationResult ValidateFile(string filePath, byte[] content, string contentType)
        {
            var result = new FileValidationResult { IsValid = true };

            // Basic validation
            if (string.IsNullOrEmpty(filePath))
            {
                result.IsValid = false;
                result.Errors.Add("File path is required");
            }

            if (content == null || content.Length == 0)
            {
                result.IsValid = false;
                result.Errors.Add("File content cannot be empty");
            }

            // File size validation (100MB limit)
            if (content != null && content.Length > 100 * 1024 * 1024)
            {
                result.IsValid = false;
                result.Errors.Add("File size exceeds 100MB limit");
            }

            // Path validation
            if (!string.IsNullOrEmpty(filePath))
            {
                var invalidChars = Path.GetInvalidPathChars();
                if (filePath.Any(c => invalidChars.Contains(c)))
                {
                    result.IsValid = false;
                    result.Errors.Add("File path contains invalid characters");
                }

                // Security check for path traversal
                if (filePath.Contains("..") || filePath.StartsWith("/") || filePath.Contains("\\..\\"))
                {
                    result.IsValid = false;
                    result.Errors.Add("File path contains potentially dangerous path traversal sequences");
                }
            }

            // Content type validation
            if (string.IsNullOrEmpty(contentType))
            {
                result.Warnings.Add("Content type not specified, will be auto-detected");
                result.SuggestedContentType = GetContentTypeFromExtension(filePath);
            }
            else if (!IsValidContentType(contentType))
            {
                result.Warnings.Add($"Unusual content type: {contentType}");
            }

            // File extension warnings
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension))
            {
                result.Warnings.Add("File has no extension, this may cause issues with content type detection");
            }

            // Executable file warning
            if (IsExecutableFile(extension))
            {
                result.Warnings.Add("Executable files may pose security risks");
            }

            return result;
        }

        private string GetContentTypeFromExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".cs" => "text/x-csharp",
                ".js" => "application/javascript",
                ".ts" => "application/typescript",
                ".py" => "text/x-python",
                ".html" => "text/html",
                ".css" => "text/css",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                ".yml" or ".yaml" => "application/x-yaml",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }

        private bool IsValidContentType(string contentType)
        {
            // Basic validation for content type format
            return !string.IsNullOrEmpty(contentType) &&
                   contentType.Contains('/') &&
                   !contentType.Contains(' ') &&
                   contentType.Length < 100;
        }

        private bool IsExecutableFile(string extension)
        {
            var executableExtensions = new[] { ".exe", ".dll", ".bat", ".cmd", ".com", ".scr", ".msi", ".ps1", ".sh" };
            return executableExtensions.Contains(extension);
        }
    }
}