using TeiasMongoAPI.Services.DTOs.Request.Block;
using TeiasMongoAPI.Services.DTOs.Response.Block;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IBlockService
    {
        Task<BlockDto> GetBlockAsync(string buildingId, string blockId, CancellationToken cancellationToken = default);
        Task<List<BlockDto>> GetBlocksAsync(string buildingId, CancellationToken cancellationToken = default);
        Task<List<ConcreteBlockDto>> GetConcreteBlocksAsync(string buildingId, CancellationToken cancellationToken = default);
        Task<List<MasonryBlockDto>> GetMasonryBlocksAsync(string buildingId, CancellationToken cancellationToken = default);
        Task<ConcreteBlockDto> CreateConcreteBlockAsync(string buildingId, ConcreteCreateDto dto, CancellationToken cancellationToken = default);
        Task<MasonryBlockDto> CreateMasonryBlockAsync(string buildingId, MasonryCreateDto dto, CancellationToken cancellationToken = default);
        Task<ConcreteBlockDto> UpdateConcreteBlockAsync(string buildingId, string blockId, ConcreteUpdateDto dto, CancellationToken cancellationToken = default);
        Task<MasonryBlockDto> UpdateMasonryBlockAsync(string buildingId, string blockId, MasonryUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteBlockAsync(string buildingId, string blockId, CancellationToken cancellationToken = default);
        Task<BlockSummaryDto> GetBlockSummaryAsync(string buildingId, string blockId, CancellationToken cancellationToken = default);
    }
}