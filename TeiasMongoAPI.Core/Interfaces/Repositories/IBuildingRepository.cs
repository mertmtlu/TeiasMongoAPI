using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Block;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IBuildingRepository : IGenericRepository<Building>
    {
        Task<IEnumerable<Building>> GetByTmIdAsync(ObjectId tmId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Building>> GetByTypeAsync(BuildingType type, CancellationToken cancellationToken = default);
        Task<Building> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        Task<IEnumerable<Building>> GetInScopeOfMETUAsync(CancellationToken cancellationToken = default);

        // Block-related operations
        Task<bool> AddBlockAsync(ObjectId buildingId, ABlock block, CancellationToken cancellationToken = default);
        Task<bool> UpdateBlockAsync(ObjectId buildingId, string blockId, ABlock block, CancellationToken cancellationToken = default);
        Task<bool> RemoveBlockAsync(ObjectId buildingId, string blockId, CancellationToken cancellationToken = default);
        Task<ABlock> GetBlockAsync(ObjectId buildingId, string blockId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ABlock>> GetBlocksAsync(ObjectId buildingId, CancellationToken cancellationToken = default);
        Task<IEnumerable<T>> GetBlocksByTypeAsync<T>(ObjectId buildingId, CancellationToken cancellationToken = default) where T : ABlock;
    }
}