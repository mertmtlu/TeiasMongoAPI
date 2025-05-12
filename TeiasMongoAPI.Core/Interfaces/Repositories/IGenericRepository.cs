using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IGenericRepository<T> where T : AEntityBase
    {
        Task<T> GetByIdAsync(ObjectId id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> filter);
        Task<T> CreateAsync(T entity);
        Task<bool> UpdateAsync(ObjectId id, T entity);
        Task<bool> DeleteAsync(ObjectId id);
        Task<bool> ExistsAsync(ObjectId id);
    }
}
