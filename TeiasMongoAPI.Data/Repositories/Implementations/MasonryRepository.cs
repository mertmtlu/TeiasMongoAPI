using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Block;
using TeiasMongoAPI.Data.Context;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class MasonryRepository : BlockRepository<Masonry>, IMasonryRepository
    {
        public MasonryRepository(MongoDbContext context) : base(context, "masonryBlocks")
        {
        }

        // Add any masonry-specific methods here if needed
    }
}