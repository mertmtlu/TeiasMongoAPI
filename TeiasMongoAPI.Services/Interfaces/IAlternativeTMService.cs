using TeiasMongoAPI.Services.DTOs.Request.AlternativeTM;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.AlternativeTM;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IAlternativeTMService
    {
        Task<AlternativeTMDetailResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<AlternativeTMSummaryResponseDto>> GetByTmIdAsync(string tmId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<AlternativeTMResponseDto> CreateAsync(AlternativeTMCreateDto dto, CancellationToken cancellationToken = default);
        Task<AlternativeTMResponseDto> UpdateAsync(string id, AlternativeTMUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<List<AlternativeTMComparisonResponseDto>> CompareAlternativesAsync(string tmId, CancellationToken cancellationToken = default);
        Task<PagedResponse<AlternativeTMSummaryResponseDto>> GetByCityAsync(string city, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<AlternativeTMSummaryResponseDto>> GetByCountyAsync(string county, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<AlternativeTMResponseDto> CreateFromTMAsync(string tmId, CreateFromTMDto dto, CancellationToken cancellationToken = default);
    }
}