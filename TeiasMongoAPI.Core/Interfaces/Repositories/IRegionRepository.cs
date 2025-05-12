using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IRegionRepository : IGenericRepository<Region>
    {
        Task<IEnumerable<Region>> GetByClientIdAsync(ObjectId clientId, CancellationToken cancellationToken = default);
        Task<Region> GetByNoAsync(int regionNo, CancellationToken cancellationToken = default);
        Task<IEnumerable<Region>> GetByCityAsync(string city, CancellationToken cancellationToken = default);
        Task<Region> GetByHeadquartersAsync(string headquarters, CancellationToken cancellationToken = default);
    }
}