using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Region;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Region;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IRegionService
    {
        Task<RegionDetailResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<RegionListResponseDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<RegionListResponseDto>> GetByClientIdAsync(string clientId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<RegionResponseDto> CreateAsync(RegionCreateDto dto, CancellationToken cancellationToken = default);
        Task<RegionResponseDto> UpdateAsync(string id, RegionUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<RegionResponseDto> GetByNoAsync(int regionNo, CancellationToken cancellationToken = default);
        Task<RegionResponseDto> UpdateCitiesAsync(string id, RegionCityUpdateDto dto, CancellationToken cancellationToken = default);
        Task<RegionStatisticsResponseDto> GetStatisticsAsync(string id, CancellationToken cancellationToken = default);
        Task<List<RegionSummaryResponseDto>> GetRegionsInCityAsync(string city, CancellationToken cancellationToken = default);
    }
}