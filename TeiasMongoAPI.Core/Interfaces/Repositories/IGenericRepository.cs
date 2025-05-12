using MongoDB.Bson;
using System.Linq.Expressions;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IGenericRepository<T> where T : AEntityBase
    {
        Task<T> GetByIdAsync(ObjectId id, CancellationToken cancellationToken = default);
        Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
        Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(ObjectId id, T entity, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(ObjectId id, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(ObjectId id, CancellationToken cancellationToken = default);

        // Additional methods that might be useful
        Task<long> CountAsync(Expression<Func<T, bool>> filter = null, CancellationToken cancellationToken = default);
        Task<T> FindOneAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
    }
}