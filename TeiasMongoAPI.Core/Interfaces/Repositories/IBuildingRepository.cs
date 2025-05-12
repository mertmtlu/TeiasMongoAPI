using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IBuildingRepository : IGenericRepository<Building>
    {
        Task<IEnumerable<Building>> GetByTmIdAsync(ObjectId tmId);
        Task<IEnumerable<Building>> GetByTypeAsync(BuildingType type);
        Task<Building> GetByNameAsync(string name);
        Task<IEnumerable<Building>> GetInScopeOfMETUAsync();
    }
}