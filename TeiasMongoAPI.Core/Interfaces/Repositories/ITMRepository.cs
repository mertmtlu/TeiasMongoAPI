using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface ITMRepository : IGenericRepository<TM>
    {
        Task<IEnumerable<TM>> GetByRegionIdAsync(ObjectId regionId);
        Task<TM> GetByNameAsync(string name);
        Task<IEnumerable<TM>> GetActiveAsync();
    }
}
