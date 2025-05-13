using TeiasMongoAPI.Services.DTOs.Request.Client;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Client;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IClientService
    {
        Task<ClientDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<ClientListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<ClientDto> CreateAsync(ClientCreateDto dto, CancellationToken cancellationToken = default);
        Task<ClientDto> UpdateAsync(string id, ClientUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<ClientDto> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    }
}