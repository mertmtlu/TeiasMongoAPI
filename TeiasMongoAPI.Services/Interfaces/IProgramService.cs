using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.DTOs;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IProgramService
    {
        // Basic CRUD Operations
        Task<ProgramDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<ProgramListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ProgramListDto>> SearchAsync(ProgramSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<ProgramDto> CreateAsync(ProgramCreateDto dto, ObjectId? userId, CancellationToken cancellationToken = default);
        Task<ProgramDto> UpdateAsync(string id, ProgramUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

        // Program-specific Operations
        Task<PagedResponse<ProgramListDto>> GetByCreatorAsync(string creatorId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ProgramListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ProgramListDto>> GetByTypeAsync(string type, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ProgramListDto>> GetByLanguageAsync(string language, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ProgramSummaryDto>> GetUserAccessibleProgramsAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ProgramListDto>> GetGroupAccessibleProgramsAsync(string groupId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);

        // Status Management
        Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken = default);
        Task<bool> UpdateCurrentVersionAsync(string id, string versionId, CancellationToken cancellationToken = default);

        // Permission Management
        Task<ProgramDto> AddUserPermissionAsync(string programId, ProgramUserPermissionDto dto, CancellationToken cancellationToken = default);
        Task<bool> RemoveUserPermissionAsync(string programId, string userId, CancellationToken cancellationToken = default);
        Task<ProgramDto> UpdateUserPermissionAsync(string programId, ProgramUserPermissionDto dto, CancellationToken cancellationToken = default);
        Task<ProgramDto> AddGroupPermissionAsync(string programId, ProgramGroupPermissionDto dto, CancellationToken cancellationToken = default);
        Task<bool> RemoveGroupPermissionAsync(string programId, string groupId, CancellationToken cancellationToken = default);
        Task<ProgramDto> UpdateGroupPermissionAsync(string programId, ProgramGroupPermissionDto dto, CancellationToken cancellationToken = default);
        Task<List<ProgramPermissionDto>> GetProgramPermissionsAsync(string programId, CancellationToken cancellationToken = default);

        // Deployment Operations (these stay as they're program-level operations)
        Task<ProgramDeploymentDto> DeployPreBuiltAppAsync(string programId, ProgramDeploymentRequestDto dto, CancellationToken cancellationToken = default);
        Task<ProgramDeploymentDto> DeployStaticSiteAsync(string programId, ProgramDeploymentRequestDto dto, CancellationToken cancellationToken = default);
        Task<ProgramDeploymentDto> DeployContainerAppAsync(string programId, ProgramDeploymentRequestDto dto, CancellationToken cancellationToken = default);
        Task<ProgramDeploymentStatusDto> GetDeploymentStatusAsync(string programId, CancellationToken cancellationToken = default);
        Task<bool> RestartApplicationAsync(string programId, CancellationToken cancellationToken = default);
        Task<List<string>> GetApplicationLogsAsync(string programId, int lines = 100, CancellationToken cancellationToken = default);
        Task<ProgramDto> UpdateDeploymentConfigAsync(string programId, ProgramDeploymentConfigDto dto, CancellationToken cancellationToken = default);

        // Validation
        Task<bool> ValidateNameUniqueAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default);
        Task<bool> ValidateUserAccessAsync(string programId, string userId, string requiredAccessLevel, CancellationToken cancellationToken = default);

        // UI Type Management
        Task<string> UpdateProgramUiTypeAsync(string programId, CancellationToken cancellationToken = default);
    }
}