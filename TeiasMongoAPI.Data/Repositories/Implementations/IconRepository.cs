using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class IconRepository : GenericRepository<Icon>, IIconRepository
    {
        private readonly MongoDbContext _context;

        public IconRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
            CreateIndexes();
        }

        private void CreateIndexes()
        {
            var entityTypeIndex = Builders<Icon>.IndexKeys.Ascending(x => x.EntityType);
            var entityCompositeIndex = Builders<Icon>.IndexKeys
                .Ascending(x => x.EntityType)
                .Ascending(x => x.EntityId);
            var creatorIndex = Builders<Icon>.IndexKeys.Ascending(x => x.Creator);
            var activeIndex = Builders<Icon>.IndexKeys.Ascending(x => x.IsActive);

            var indexModels = new[]
            {
                new CreateIndexModel<Icon>(entityTypeIndex),
                new CreateIndexModel<Icon>(entityCompositeIndex, new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<Icon>(creatorIndex),
                new CreateIndexModel<Icon>(activeIndex)
            };

            _collection.Indexes.CreateMany(indexModels);
        }

        public async Task<IEnumerable<Icon>> GetByEntityTypeAsync(IconEntityType entityType, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Icon>.Filter.And(
                Builders<Icon>.Filter.Eq(x => x.EntityType, entityType),
                Builders<Icon>.Filter.Eq(x => x.IsActive, true)
            );
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<Icon?> GetByEntityAsync(IconEntityType entityType, ObjectId entityId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Icon>.Filter.And(
                Builders<Icon>.Filter.Eq(x => x.EntityType, entityType),
                Builders<Icon>.Filter.Eq(x => x.EntityId, entityId),
                Builders<Icon>.Filter.Eq(x => x.IsActive, true)
            );
            return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<Icon>> GetByEntityIdsAsync(IconEntityType entityType, IEnumerable<ObjectId> entityIds, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Icon>.Filter.And(
                Builders<Icon>.Filter.Eq(x => x.EntityType, entityType),
                Builders<Icon>.Filter.In(x => x.EntityId, entityIds),
                Builders<Icon>.Filter.Eq(x => x.IsActive, true)
            );
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Icon>> GetByIconIdsAsync(IEnumerable<ObjectId> iconIds, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Icon>.Filter.And(
                Builders<Icon>.Filter.In(x => x._ID, iconIds),
                Builders<Icon>.Filter.Eq(x => x.IsActive, true)
            );
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<bool> EntityHasIconAsync(IconEntityType entityType, ObjectId entityId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Icon>.Filter.And(
                Builders<Icon>.Filter.Eq(x => x.EntityType, entityType),
                Builders<Icon>.Filter.Eq(x => x.EntityId, entityId),
                Builders<Icon>.Filter.Eq(x => x.IsActive, true)
            );
            var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            return count > 0;
        }

        public async Task<IEnumerable<Icon>> GetByCreatorAsync(string creator, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Icon>.Filter.And(
                Builders<Icon>.Filter.Eq(x => x.Creator, creator),
                Builders<Icon>.Filter.Eq(x => x.IsActive, true)
            );
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<bool> DeleteByEntityAsync(IconEntityType entityType, ObjectId entityId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Icon>.Filter.And(
                Builders<Icon>.Filter.Eq(x => x.EntityType, entityType),
                Builders<Icon>.Filter.Eq(x => x.EntityId, entityId)
            );
            var update = Builders<Icon>.Update
                .Set(x => x.IsActive, false)
                .Set(x => x.ModifiedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<long> GetCountByEntityTypeAsync(IconEntityType entityType, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Icon>.Filter.And(
                Builders<Icon>.Filter.Eq(x => x.EntityType, entityType),
                Builders<Icon>.Filter.Eq(x => x.IsActive, true)
            );
            return await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        }

        public new async Task<bool> DeleteAsync(ObjectId id, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Icon>.Filter.Eq(x => x._ID, id);
            var update = Builders<Icon>.Update
                .Set(x => x.IsActive, false)
                .Set(x => x.ModifiedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }
    }
}