using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class BuildingRepository : GenericRepository<Building>, IBuildingRepository
    {
        private readonly MongoDbContext _context;

        public BuildingRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<IEnumerable<Building>> GetByTmIdAsync(ObjectId tmId, CancellationToken cancellationToken = default)
        {
            return await _context.Buildings
                .Find(b => b.TmID == tmId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Building>> GetByTypeAsync(BuildingType type, CancellationToken cancellationToken = default)
        {
            return await _context.Buildings
                .Find(b => b.Type == type)
                .ToListAsync(cancellationToken);
        }

        public async Task<Building> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return await _context.Buildings
                .Find(b => b.Name == name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<Building>> GetInScopeOfMETUAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Buildings
                .Find(b => b.InScopeOfMETU)
                .ToListAsync(cancellationToken);
        }
    }
}