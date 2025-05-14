using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class RegionRepository : GenericRepository<Region>, IRegionRepository
    {
        private readonly MongoDbContext _context;

        public RegionRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<IEnumerable<Region>> GetByClientIdAsync(ObjectId clientId, CancellationToken cancellationToken = default)
        {
            return await _context.Regions
                .Find(r => r.ClientID == clientId)
                .ToListAsync(cancellationToken);
        }

        public async Task<Region> GetByNoAsync(int regionNo, CancellationToken cancellationToken = default)
        {
            return await _context.Regions
                .Find(r => r.RegionID == regionNo)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<Region>> GetByCityAsync(string city, CancellationToken cancellationToken = default)
        {
            return await _context.Regions
                .Find(r => r.Cities.Contains(city))
                .ToListAsync(cancellationToken);
        }

        public async Task<Region> GetByHeadquartersAsync(string headquarters, CancellationToken cancellationToken = default)
        {
            return await _context.Regions
                .Find(r => r.Headquarters == headquarters)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}