using TeiasMongoAPI.Services.DTOs.Request.AlternativeTM;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.AlternativeTM;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IAlternativeTMService
    {
        Task<AlternativeTMDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<AlternativeTMSummaryDto>> GetByTmIdAsync(string tmId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<AlternativeTMDto> CreateAsync(AlternativeTMCreateDto dto, CancellationToken cancellationToken = default);
        Task<AlternativeTMDto> UpdateAsync(string id, AlternativeTMUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<List<AlternativeTMComparisonDto>> CompareAlternativesAsync(string tmId, CancellationToken cancellationToken = default);
        Task<PagedResponse<AlternativeTMSummaryDto>> GetByCityAsync(string city, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<AlternativeTMSummaryDto>> GetByCountyAsync(string county, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
    }
}