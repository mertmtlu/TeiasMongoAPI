using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Block;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IBlockRepository<T> : IGenericRepository<T> where T : ABlock
    {
        Task<IEnumerable<T>> GetByBuildingIdAsync(ObjectId buildingId);
        Task<T> GetByNameAsync(string name);
        Task<IEnumerable<T>> GetByModelingTypeAsync(ModelingType modelingType);
    }
}