using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.RemoteApp;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.RemoteApp;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IRemoteAppService
    {
        Task<PagedResponse<RemoteAppListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<RemoteAppDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<RemoteAppDto> CreateAsync(RemoteAppCreateDto dto, string creatorId, CancellationToken cancellationToken = default);
        Task<RemoteAppDto> UpdateAsync(string id, RemoteAppUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        
        Task<PagedResponse<RemoteAppListDto>> GetByCreatorAsync(string creatorId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<RemoteAppListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<RemoteAppListDto>> GetUserAccessibleAppsAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<RemoteAppListDto>> GetPublicAppsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        
        Task<bool> AssignUserAsync(string remoteAppId, string userId, CancellationToken cancellationToken = default);
        Task<bool> UnassignUserAsync(string remoteAppId, string userId, CancellationToken cancellationToken = default);
        Task<bool> IsUserAssignedAsync(string remoteAppId, string userId, CancellationToken cancellationToken = default);
        
        Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken = default);
        Task<bool> ValidateNameUniqueAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default);
        Task<string> GetLaunchUrlAsync(string id, string userId, CancellationToken cancellationToken = default);
    }
}