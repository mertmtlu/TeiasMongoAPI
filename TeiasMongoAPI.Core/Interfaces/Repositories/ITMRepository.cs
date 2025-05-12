using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface ITMRepository : IGenericRepository<TM>
    {
        Task<IEnumerable<TM>> GetByRegionIdAsync(ObjectId regionId, CancellationToken cancellationToken = default);
        Task<TM> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        Task<IEnumerable<TM>> GetActiveAsync(CancellationToken cancellationToken = default);
    }
}