using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IBuildingRepository : IGenericRepository<Building>
    {
        Task<IEnumerable<Building>> GetByTmIdAsync(ObjectId tmId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Building>> GetByTypeAsync(BuildingType type, CancellationToken cancellationToken = default);
        Task<Building> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        Task<IEnumerable<Building>> GetInScopeOfMETUAsync(CancellationToken cancellationToken = default);
    }
}