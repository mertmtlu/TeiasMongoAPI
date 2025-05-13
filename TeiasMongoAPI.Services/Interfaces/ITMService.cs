using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Search;
using TeiasMongoAPI.Services.DTOs.Request.TM;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.TM;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface ITMService
    {
        Task<TMDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<TMListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<TMListDto>> GetByRegionIdAsync(string regionId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<TMListDto>> SearchAsync(TMSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<TMDto> CreateAsync(TMCreateDto dto, CancellationToken cancellationToken = default);
        Task<TMDto> UpdateAsync(string id, TMUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<TMDto> UpdateStateAsync(string id, TMStateUpdateDto dto, CancellationToken cancellationToken = default);
        Task<TMDto> UpdateVoltagesAsync(string id, TMVoltageUpdateDto dto, CancellationToken cancellationToken = default);
        Task<TMDto> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    }
}