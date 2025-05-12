using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Block;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IBlockRepository<T> : IGenericRepository<T> where T : ABlock
    {
        Task<IEnumerable<T>> GetByBuildingIdAsync(ObjectId buildingId, CancellationToken cancellationToken = default);
        Task<T> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        Task<IEnumerable<T>> GetByModelingTypeAsync(ModelingType modelingType, CancellationToken cancellationToken = default);
    }
}