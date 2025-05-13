using TeiasMongoAPI.Services.DTOs.Request.Client;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Client;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IClientService
    {
        Task<ClientDetailResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<ClientListResponseDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<ClientResponseDto> CreateAsync(ClientCreateDto dto, CancellationToken cancellationToken = default);
        Task<ClientResponseDto> UpdateAsync(string id, ClientUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<ClientResponseDto> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    }
}