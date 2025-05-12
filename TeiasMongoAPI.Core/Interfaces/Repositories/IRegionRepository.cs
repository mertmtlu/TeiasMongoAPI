using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IRegionRepository : IGenericRepository<Region>
    {
        Task<IEnumerable<Region>> GetByClientIdAsync(ObjectId clientId);
        Task<Region> GetByNoAsync(int regionNo);
        Task<IEnumerable<Region>> GetByCityAsync(string city);
        Task<Region> GetByHeadquartersAsync(string headquarters);
    }
}