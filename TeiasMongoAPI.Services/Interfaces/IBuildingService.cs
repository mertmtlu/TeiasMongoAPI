using TeiasMongoAPI.Services.DTOs.Request.Building;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Search;
using TeiasMongoAPI.Services.DTOs.Response.Building;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IBuildingService
    {
        Task<BuildingDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<BuildingListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<BuildingListDto>> GetByTmIdAsync(string tmId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<BuildingListDto>> SearchAsync(BuildingSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<BuildingDto> CreateAsync(BuildingCreateDto dto, CancellationToken cancellationToken = default);
        Task<BuildingDto> UpdateAsync(string id, BuildingUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<BuildingDto> AddBlockAsync(string buildingId, BuildingBlockAddDto dto, CancellationToken cancellationToken = default);
        Task<BuildingDto> RemoveBlockAsync(string buildingId, BuildingBlockRemoveDto dto, CancellationToken cancellationToken = default);
    }
}