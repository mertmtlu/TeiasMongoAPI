using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Search;
using TeiasMongoAPI.Services.DTOs.Request.TM;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.TM;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface ITMService
    {
        Task<TMDetailResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<TMListResponseDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<TMListResponseDto>> GetByRegionIdAsync(string regionId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<TMListResponseDto>> SearchAsync(TMSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<TMResponseDto> CreateAsync(TMCreateDto dto, CancellationToken cancellationToken = default);
        Task<TMResponseDto> UpdateAsync(string id, TMUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<TMResponseDto> UpdateStateAsync(string id, TMStateUpdateDto dto, CancellationToken cancellationToken = default);
        Task<TMResponseDto> UpdateVoltagesAsync(string id, TMVoltageUpdateDto dto, CancellationToken cancellationToken = default);
        Task<TMResponseDto> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        Task<TMStatisticsResponseDto> GetStatisticsAsync(string id, CancellationToken cancellationToken = default);
        Task<TMHazardSummaryResponseDto> GetHazardSummaryAsync(string id, CancellationToken cancellationToken = default);
    }
}