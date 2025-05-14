using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Block;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class BuildingRepository : GenericRepository<Building>, IBuildingRepository
    {
        private readonly MongoDbContext _context;

        public BuildingRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        #region Building-related operations

        public async Task<IEnumerable<Building>> GetByTmIdAsync(ObjectId tmId, CancellationToken cancellationToken = default)
        {
            return await _context.Buildings
                .Find(b => b.TmID == tmId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Building>> GetByTypeAsync(BuildingType type, CancellationToken cancellationToken = default)
        {
            return await _context.Buildings
                .Find(b => b.Type == type)
                .ToListAsync(cancellationToken);
        }

        public async Task<Building> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return await _context.Buildings
                .Find(b => b.Name == name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<Building>> GetInScopeOfMETUAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Buildings
                .Find(b => b.InScopeOfMETU)
                .ToListAsync(cancellationToken);
        }

        #endregion

        #region Block-related operations

        public async Task<bool> AddBlockAsync(ObjectId buildingId, ABlock block, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Building>.Filter.Eq(b => b._ID, buildingId);
            var update = Builders<Building>.Update.Push(b => b.Blocks, block);

            var result = await _context.Buildings.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateBlockAsync(ObjectId buildingId, string blockId, ABlock block, CancellationToken cancellationToken = default)
        {
            // First, get the building to find the block index
            var building = await GetByIdAsync(buildingId, cancellationToken);
            if (building == null) return false;

            var blockIndex = building.Blocks.FindIndex(b => b.ID == blockId);
            if (blockIndex == -1) return false;

            // Update the specific block
            var filter = Builders<Building>.Filter.And(
                Builders<Building>.Filter.Eq(b => b._ID, buildingId),
                Builders<Building>.Filter.Eq("Blocks.ID", blockId)
            );
            var update = Builders<Building>.Update.Set($"Blocks.{blockIndex}", block);

            var result = await _context.Buildings.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemoveBlockAsync(ObjectId buildingId, string blockId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Building>.Filter.Eq(b => b._ID, buildingId);
            var update = Builders<Building>.Update.PullFilter(b => b.Blocks,
                Builders<ABlock>.Filter.Eq(block => block.ID, blockId));

            var result = await _context.Buildings.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<ABlock> GetBlockAsync(ObjectId buildingId, string blockId, CancellationToken cancellationToken = default)
        {
            var building = await GetByIdAsync(buildingId, cancellationToken);
            return building?.Blocks.FirstOrDefault(b => b.ID == blockId);
        }

        public async Task<IEnumerable<ABlock>> GetBlocksAsync(ObjectId buildingId, CancellationToken cancellationToken = default)
        {
            var building = await GetByIdAsync(buildingId, cancellationToken);
            return building?.Blocks ?? Enumerable.Empty<ABlock>();
        }

        public async Task<IEnumerable<T>> GetBlocksByTypeAsync<T>(ObjectId buildingId, CancellationToken cancellationToken = default) where T : ABlock
        {
            var building = await GetByIdAsync(buildingId, cancellationToken);
            return building?.Blocks.OfType<T>() ?? Enumerable.Empty<T>();
        }

        #endregion
    }
}