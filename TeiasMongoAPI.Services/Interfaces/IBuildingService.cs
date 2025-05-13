using TeiasMongoAPI.Services.DTOs.Request.Building;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Search;
using TeiasMongoAPI.Services.DTOs.Response.Building;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IBuildingService
    {
        Task<BuildingDetailResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<BuildingListResponseDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<BuildingListResponseDto>> GetByTmIdAsync(string tmId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<BuildingListResponseDto>> SearchAsync(BuildingSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<BuildingResponseDto> CreateAsync(BuildingCreateDto dto, CancellationToken cancellationToken = default);
        Task<BuildingResponseDto> UpdateAsync(string id, BuildingUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<BuildingResponseDto> AddBlockAsync(string buildingId, BuildingBlockAddDto dto, CancellationToken cancellationToken = default);
        Task<BuildingResponseDto> RemoveBlockAsync(string buildingId, BuildingBlockRemoveDto dto, CancellationToken cancellationToken = default);
    }
}