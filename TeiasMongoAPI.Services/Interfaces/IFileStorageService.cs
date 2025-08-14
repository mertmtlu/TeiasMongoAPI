using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IFileStorageService
    {
        /// <summary>
        /// Stores a file for a specific program version. All files must belong to a version.
        /// Storage path: ./storage/{programId}/{versionId}/files/{filePath}
        /// </summary>
        Task<string> StoreFileAsync(string programId, string versionId, string filePath, byte[] content, string contentType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores multiple files for a specific program version
        /// </summary>
        Task<List<FileStorageResult>> StoreFilesAsync(string programId, string versionId, List<VersionFileCreateDto> files, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves file content for a specific program version
        /// </summary>
        Task<byte[]> GetFileContentAsync(string programId, string versionId, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves file with metadata for a specific program version
        /// </summary>
        Task<VersionFileDetailDto> GetFileAsync(string programId, string versionId, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing file in a specific program version
        /// </summary>
        Task<string> UpdateFileAsync(string programId, string versionId, string filePath, byte[] content, string contentType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a specific file from a program version
        /// </summary>
        Task<bool> DeleteFileAsync(string programId, string versionId, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all files for a specific program version
        /// </summary>
        Task<List<VersionFileDto>> ListVersionFilesAsync(string programId, string versionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all execution files (runtime-generated files) for a specific execution.
        /// Storage path: ./storage/{programId}/{versionId}/execution/{executionId}/
        /// Excludes version files and filters out generated folders like __pycache__
        /// </summary>
        Task<List<VersionFileDto>> ListExecutionFilesAsync(string programId, string versionId, string executionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a specific execution output file with metadata.
        /// Storage path: ./storage/{programId}/{versionId}/execution/{executionId}/outputs/{filePath}
        /// </summary>
        Task<VersionFileDetailDto> GetExecutionFileAsync(string programId, string versionId, string executionId, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes all files for a specific program version
        /// </summary>
        Task<bool> DeleteVersionFilesAsync(string programId, string versionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Copies all files from one version to another within the same program
        /// </summary>
        Task<List<FileStorageResult>> CopyVersionFilesAsync(string programId, string fromVersionId, string toVersionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes all files for an entire program (all versions)
        /// </summary>
        Task<bool> DeleteProgramFilesAsync(string programId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets storage statistics for a program across all versions
        /// </summary>
        Task<StorageStatistics> GetStorageStatisticsAsync(string programId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates file before storage
        /// </summary>
        Task<FileValidationResult> ValidateFileAsync(string fileName, byte[] content, string contentType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates file hash
        /// </summary>
        string CalculateFileHash(byte[] content);

        /// <summary>
        /// Gets the physical file path for a program version file
        /// </summary>
        string GetFilePath(string programId, string versionId, string filePath);

        /// <summary>
        /// Downloads multiple files as a zip archive from a specific program version
        /// </summary>
        Task<BulkDownloadResult> BulkDownloadFilesAsync(string programId, string versionId, List<string> filePaths, bool includeMetadata = false, string compressionLevel = "optimal", CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads all files from a specific program version as a zip archive
        /// </summary>
        Task<BulkDownloadResult> DownloadAllVersionFilesAsync(string programId, string versionId, bool includeMetadata = false, string compressionLevel = "optimal", CancellationToken cancellationToken = default);
    }

    public class FileStorageResult
    {
        public string FilePath { get; set; } = string.Empty;
        public string StorageKey { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class FileMetadata
    {
        public string StorageKey { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        public bool Exists { get; set; }
    }

    public class FileValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string? SuggestedContentType { get; set; }
    }

    public class StorageStatistics
    {
        public string ProgramId { get; set; } = string.Empty;
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public int VersionCount { get; set; }
        public DateTime LastModified { get; set; }
        public Dictionary<string, int> FileTypeCount { get; set; } = new();
        public Dictionary<string, long> FileTypeSizes { get; set; } = new();
    }
}