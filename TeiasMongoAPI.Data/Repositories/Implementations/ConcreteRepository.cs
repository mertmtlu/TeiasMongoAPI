using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Block;
using TeiasMongoAPI.Data.Context;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class ConcreteRepository : BlockRepository<Concrete>, IConcreteRepository
    {
        public ConcreteRepository(MongoDbContext context) : base(context, "concreteBlocks")
        {
        }

        public async Task<IEnumerable<Concrete>> GetStrengthenedBlocksAsync(CancellationToken cancellationToken = default)
        {
            return await _context.ConcreteBlocks
                .Find(c => c.IsStrengthened)
                .ToListAsync(cancellationToken);
        }
    }
}