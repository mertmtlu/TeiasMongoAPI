using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class TMRepository : GenericRepository<TM>, ITMRepository
    {
        private readonly MongoDbContext _context;

        public TMRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<IEnumerable<TM>> GetByRegionIdAsync(ObjectId regionId, CancellationToken cancellationToken = default)
        {
            return await _context.TMs
                .Find(tm => tm.RegionID == regionId)
                .ToListAsync(cancellationToken);
        }

        public async Task<TM> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return await _context.TMs
                .Find(tm => tm.Name == name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<TM>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            return await _context.TMs
                .Find(tm => tm.State == TMState.Active)
                .ToListAsync(cancellationToken);
        }
    }
}