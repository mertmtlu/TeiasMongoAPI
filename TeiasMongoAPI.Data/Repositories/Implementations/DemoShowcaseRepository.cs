using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class DemoShowcaseRepository : GenericRepository<DemoShowcase>, IDemoShowcaseRepository
    {
        private readonly MongoDbContext _context;

        public DemoShowcaseRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }
    }
}
