using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Interfaces.Specifications;
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

        public async Task<(IReadOnlyList<T> Items, long TotalCount)> FindWithSpecificationAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
        {
            var query = ApplySpecification(spec);

            // Get total count before applying pagination
            var totalCount = spec.Criteria != null 
                ? await _collection.CountDocumentsAsync(spec.Criteria, cancellationToken: cancellationToken)
                : await _collection.CountDocumentsAsync(_ => true, cancellationToken: cancellationToken);

            // Apply specification and get paginated results
            var items = await query.ToListAsync(cancellationToken);

            return (items.AsReadOnly(), totalCount);
        }

        private IFindFluent<T, T> ApplySpecification(ISpecification<T> spec)
        {
            // Start with the base query
            var query = spec.Criteria != null 
                ? _collection.Find(spec.Criteria) 
                : _collection.Find(_ => true);

            // Apply ordering
            if (spec.OrderBy != null)
            {
                query = query.SortBy(spec.OrderBy);
            }
            else if (spec.OrderByDescending != null)
            {
                query = query.SortByDescending(spec.OrderByDescending);
            }

            // Apply additional sorting (ThenBy)
            if (spec.ThenByExpressions.Any())
            {
                var sortDefinitions = new List<SortDefinition<T>>();
                
                // Add primary sort
                if (spec.OrderBy != null)
                {
                    sortDefinitions.Add(Builders<T>.Sort.Ascending(spec.OrderBy));
                }
                else if (spec.OrderByDescending != null)
                {
                    sortDefinitions.Add(Builders<T>.Sort.Descending(spec.OrderByDescending));
                }

                // Add ThenBy sorts
                foreach (var thenBy in spec.ThenByExpressions)
                {
                    sortDefinitions.Add(Builders<T>.Sort.Ascending(thenBy));
                }

                // Add ThenByDescending sorts
                foreach (var thenByDesc in spec.ThenByDescendingExpressions)
                {
                    sortDefinitions.Add(Builders<T>.Sort.Descending(thenByDesc));
                }

                if (sortDefinitions.Any())
                {
                    query = query.Sort(Builders<T>.Sort.Combine(sortDefinitions));
                }
            }

            // Apply pagination
            if (spec.IsPagingEnabled)
            {
                query = query.Skip(spec.Skip).Limit(spec.Take);
            }

            return query;
        }
    }
}