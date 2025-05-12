using TeiasMongoAPI.Core.Models.Block;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IConcreteRepository : IBlockRepository<Concrete>
    {
        Task<IEnumerable<Concrete>> GetStrengthenedBlocksAsync(CancellationToken cancellationToken = default);
    }
}