using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IFileStorageService
    {
        /// <summary>
        /// Stores a file and returns the storage key
        /// </summary>
        Task<string> StoreFileAsync(string programId, string filePath, byte[] content, string contentType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores multiple files for a program version
        /// </summary>
        Task<List<FileStorageResult>> StoreFilesAsync(string programId, string? versionId, List<ProgramFileUploadDto> files, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves file content by storage key
        /// </summary>
        Task<byte[]> GetFileContentAsync(string storageKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves file content by program ID and file path
        /// </summary>
        Task<ProgramFileContentDto> GetFileAsync(string programId, string filePath, string? versionId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing file
        /// </summary>
        Task<string> UpdateFileAsync(string programId, string filePath, byte[] content, string contentType, string? versionId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file
        /// </summary>
        Task<bool> DeleteFileAsync(string storageKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes all files for a program
        /// </summary>
        Task<bool> DeleteProgramFilesAsync(string programId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets file metadata without content
        /// </summary>
        Task<FileMetadata> GetFileMetadataAsync(string storageKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all files for a program
        /// </summary>
        Task<List<FileMetadata>> ListProgramFilesAsync(string programId, string? versionId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates file before storage
        /// </summary>
        Task<FileValidationResult> ValidateFileAsync(string fileName, byte[] content, string contentType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the physical file path for a storage key
        /// </summary>
        string GetFilePath(string storageKey);

        /// <summary>
        /// Calculates file hash
        /// </summary>
        string CalculateFileHash(byte[] content);

        /// <summary>
        /// Copies files from one version to another
        /// </summary>
        Task<List<FileStorageResult>> CopyVersionFilesAsync(string programId, string fromVersionId, string toVersionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets storage statistics for a program
        /// </summary>
        Task<StorageStatistics> GetStorageStatisticsAsync(string programId, CancellationToken cancellationToken = default);
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
        public string FilePath { get; set; } = string.Empty;
        public string? VersionId { get; set; }
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