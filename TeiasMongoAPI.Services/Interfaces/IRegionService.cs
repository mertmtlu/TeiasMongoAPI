using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Region;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Region;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IRegionService
    {
        Task<RegionDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<RegionListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<RegionListDto>> GetByClientIdAsync(string clientId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<RegionDto> CreateAsync(RegionCreateDto dto, CancellationToken cancellationToken = default);
        Task<RegionDto> UpdateAsync(string id, RegionUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<RegionDto> GetByNoAsync(int regionNo, CancellationToken cancellationToken = default);
        Task<RegionDto> UpdateCitiesAsync(string id, RegionCityUpdateDto dto, CancellationToken cancellationToken = default);
    }
}