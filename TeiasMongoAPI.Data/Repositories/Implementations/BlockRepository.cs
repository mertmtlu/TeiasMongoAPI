using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Block;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public abstract class BlockRepository<T> : GenericRepository<T>, IBlockRepository<T> where T : ABlock
    {
        protected readonly MongoDbContext _context;
        protected readonly IMongoCollection<T> _blockCollection;

        protected BlockRepository(MongoDbContext context, string collectionName) : base(context.Database)
        {
            _context = context;
            _blockCollection = context.Database.GetCollection<T>(collectionName);
        }

        public async Task<IEnumerable<T>> GetByBuildingIdAsync(ObjectId buildingId, CancellationToken cancellationToken = default)
        {
            // This would need to be implemented based on your actual data structure
            // If blocks are embedded in buildings, this would require a different approach
            // For now, assuming blocks have a BuildingId property (which may need to be added to the model)
            throw new NotImplementedException("This method needs to be implemented based on your data structure");
        }

        public async Task<T> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return await _blockCollection
                .Find(b => b.Name == name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<T>> GetByModelingTypeAsync(ModelingType modelingType, CancellationToken cancellationToken = default)
        {
            return await _blockCollection
                .Find(b => b.ModelingType == modelingType)
                .ToListAsync(cancellationToken);
        }
    }
}