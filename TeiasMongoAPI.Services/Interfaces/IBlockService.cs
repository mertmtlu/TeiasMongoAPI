using TeiasMongoAPI.Services.DTOs.Request.Block;
using TeiasMongoAPI.Services.DTOs.Response.Block;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IBlockService
    {
        Task<BlockResponseDto> GetBlockAsync(string buildingId, string blockId, CancellationToken cancellationToken = default);
        Task<List<BlockResponseDto>> GetBlocksAsync(string buildingId, CancellationToken cancellationToken = default);
        Task<List<ConcreteBlockResponseDto>> GetConcreteBlocksAsync(string buildingId, CancellationToken cancellationToken = default);
        Task<List<MasonryBlockResponseDto>> GetMasonryBlocksAsync(string buildingId, CancellationToken cancellationToken = default);
        Task<ConcreteBlockResponseDto> CreateConcreteBlockAsync(string buildingId, ConcreteCreateDto dto, CancellationToken cancellationToken = default);
        Task<MasonryBlockResponseDto> CreateMasonryBlockAsync(string buildingId, MasonryCreateDto dto, CancellationToken cancellationToken = default);
        Task<ConcreteBlockResponseDto> UpdateConcreteBlockAsync(string buildingId, string blockId, ConcreteUpdateDto dto, CancellationToken cancellationToken = default);
        Task<MasonryBlockResponseDto> UpdateMasonryBlockAsync(string buildingId, string blockId, MasonryUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteBlockAsync(string buildingId, string blockId, CancellationToken cancellationToken = default);
        Task<BlockSummaryResponseDto> GetBlockSummaryAsync(string buildingId, string blockId, CancellationToken cancellationToken = default);
        Task<BlockStatisticsResponseDto> GetBlockStatisticsAsync(string buildingId, string blockId, CancellationToken cancellationToken = default);
        Task<BlockResponseDto> CopyBlockAsync(string buildingId, string blockId, CopyBlockDto dto, CancellationToken cancellationToken = default);
    }
}