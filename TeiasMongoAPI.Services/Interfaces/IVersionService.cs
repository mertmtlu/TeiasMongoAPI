using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IVersionService
    {
        // Basic CRUD Operations
        Task<VersionDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<VersionListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<VersionListDto>> SearchAsync(VersionSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<VersionDto> CreateAsync(VersionCreateDto dto, CancellationToken cancellationToken = default);
        Task<VersionDto> UpdateAsync(string id, VersionUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

        // Program-specific Version Operations
        Task<PagedResponse<VersionListDto>> GetByProgramIdAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<VersionDto> GetLatestVersionForProgramAsync(string programId, CancellationToken cancellationToken = default);
        Task<VersionDto> GetByProgramIdAndVersionNumberAsync(string programId, int versionNumber, CancellationToken cancellationToken = default);
        Task<int> GetNextVersionNumberAsync(string programId, CancellationToken cancellationToken = default);

        // User-specific Operations
        Task<PagedResponse<VersionListDto>> GetByCreatorAsync(string creatorId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<VersionListDto>> GetByReviewerAsync(string reviewerId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);

        // Status and Review Management
        Task<PagedResponse<VersionListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<VersionListDto>> GetPendingReviewsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<bool> UpdateStatusAsync(string id, VersionStatusUpdateDto dto, CancellationToken cancellationToken = default);
        Task<VersionReviewDto> SubmitReviewAsync(string id, VersionReviewSubmissionDto dto, CancellationToken cancellationToken = default);

        // File Management within Versions
        Task<bool> AddFileAsync(string versionId, VersionFileCreateDto dto, CancellationToken cancellationToken = default);
        Task<bool> UpdateFileAsync(string versionId, string filePath, VersionFileUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> RemoveFileAsync(string versionId, string filePath, CancellationToken cancellationToken = default);
        Task<List<VersionFileDto>> GetFilesByVersionIdAsync(string versionId, CancellationToken cancellationToken = default);
        Task<VersionFileDetailDto> GetFileByPathAsync(string versionId, string filePath, CancellationToken cancellationToken = default);

        // Version Comparison and Diff
        Task<VersionDiffDto> GetDiffBetweenVersionsAsync(string fromVersionId, string toVersionId, CancellationToken cancellationToken = default);
        Task<VersionDiffDto> GetDiffFromPreviousAsync(string versionId, CancellationToken cancellationToken = default);
        Task<List<VersionChangeDto>> GetChangeSummaryAsync(string versionId, CancellationToken cancellationToken = default);

        // Version Deployment Operations
        Task<VersionDeploymentDto> DeployVersionAsync(string versionId, VersionDeploymentRequestDto dto, CancellationToken cancellationToken = default);
        Task<bool> RevertToPreviousVersionAsync(string programId, string versionId, CancellationToken cancellationToken = default);
        Task<bool> SetAsCurrentVersionAsync(string programId, string versionId, CancellationToken cancellationToken = default);

        // Version Statistics
        Task<VersionStatsDto> GetVersionStatsAsync(string programId, CancellationToken cancellationToken = default);
        Task<List<VersionActivityDto>> GetVersionActivityAsync(string programId, int days = 30, CancellationToken cancellationToken = default);

        // Commit Operations
        Task<VersionDto> CommitChangesAsync(string programId, VersionCommitDto dto, CancellationToken cancellationToken = default);
        Task<bool> ValidateCommitAsync(string programId, VersionCommitValidationDto dto, CancellationToken cancellationToken = default);
    }
}