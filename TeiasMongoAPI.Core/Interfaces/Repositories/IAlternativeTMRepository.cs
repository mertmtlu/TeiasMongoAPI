using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IAlternativeTMRepository : IGenericRepository<AlternativeTM>
    {
        Task<IEnumerable<AlternativeTM>> GetByTmIdAsync(ObjectId tmId);
        Task<IEnumerable<AlternativeTM>> GetByCityAsync(string city);
        Task<IEnumerable<AlternativeTM>> GetByCountyAsync(string county);
    }
}