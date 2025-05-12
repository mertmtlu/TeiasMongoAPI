using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Data.Repositories.Base
{
    public class GenericRepository<T> : IGenericRepository<T> where T : AEntityBase
    {
        protected readonly IMongoCollection<T> _collection;

        public GenericRepository(IMongoDatabase database)
        {
            var collectionName = typeof(T).Name.ToLower() + "s";
            _collection = database.GetCollection<T>(collectionName);
        }

        public async Task<T> GetByIdAsync(ObjectId id, CancellationToken cancellationToken = default)
        {
            var filter = Builders<T>.Filter.Eq("_id", id);
            return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _collection.Find(_ => true).ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        {
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default)
        {
            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
            return entity;
        }

        public async Task<bool> UpdateAsync(ObjectId id, T entity, CancellationToken cancellationToken = default)
        {
            entity._ID = id;
            var result = await _collection.ReplaceOneAsync(
                filter: Builders<T>.Filter.Eq("_id", id),
                replacement: entity,
                options: new ReplaceOptions { IsUpsert = false },
                cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAsync(ObjectId id, CancellationToken cancellationToken = default)
        {
            var result = await _collection.DeleteOneAsync(
                filter: Builders<T>.Filter.Eq("_id", id),
                cancellationToken: cancellationToken);

            return result.DeletedCount > 0;
        }

        public async Task<bool> ExistsAsync(ObjectId id, CancellationToken cancellationToken = default)
        {
            var filter = Builders<T>.Filter.Eq("_id", id);
            var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            return count > 0;
        }

        public async Task<long> CountAsync(Expression<Func<T, bool>>? filter = null, CancellationToken cancellationToken = default)
        {
            if (filter == null)
            {
                return await _collection.CountDocumentsAsync(_ => true, cancellationToken: cancellationToken);
            }

            return await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        }

        public async Task<T> FindOneAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default)
        {
            return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }
    }
}