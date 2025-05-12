using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class AlternativeTMRepository : GenericRepository<AlternativeTM>, IAlternativeTMRepository
    {
        private readonly MongoDbContext _context;

        public AlternativeTMRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<IEnumerable<AlternativeTM>> GetByTmIdAsync(ObjectId tmId, CancellationToken cancellationToken = default)
        {
            return await _context.AlternativeTMs
                .Find(atm => atm.TmID == tmId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<AlternativeTM>> GetByCityAsync(string city, CancellationToken cancellationToken = default)
        {
            return await _context.AlternativeTMs
                .Find(atm => atm.City == city)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<AlternativeTM>> GetByCountyAsync(string county, CancellationToken cancellationToken = default)
        {
            return await _context.AlternativeTMs
                .Find(atm => atm.County == county)
                .ToListAsync(cancellationToken);
        }
    }
}