using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class UiComponentRepository : GenericRepository<UiComponent>, IUiComponentRepository
    {
        private readonly MongoDbContext _context;

        public UiComponentRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<IEnumerable<UiComponent>> GetGlobalComponentsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<UiComponent>("uicomponents")
                .Find(c => c.IsGlobal == true)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<UiComponent>> GetByProgramIdAsync(ObjectId programId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<UiComponent>("uicomponents")
                .Find(c => c.ProgramId == programId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<UiComponent>> GetByTypeAsync(string type, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<UiComponent>("uicomponents")
                .Find(c => c.Type == type)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<UiComponent>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<UiComponent>("uicomponents")
                .Find(c => c.Creator == creatorId)
                .SortByDescending(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<UiComponent>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<UiComponent>("uicomponents")
                .Find(c => c.Status == status)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<UiComponent>> GetAvailableForProgramAsync(ObjectId programId, CancellationToken cancellationToken = default)
        {
            // Get global components and program-specific components
            var filter = Builders<UiComponent>.Filter.Or(
                Builders<UiComponent>.Filter.Eq(c => c.IsGlobal, true),
                Builders<UiComponent>.Filter.Eq(c => c.ProgramId, programId)
            );

            return await _context.Database.GetCollection<UiComponent>("uicomponents")
                .Find(filter)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> UpdateStatusAsync(ObjectId id, string status, CancellationToken cancellationToken = default)
        {
            var update = Builders<UiComponent>.Update.Set(c => c.Status, status);
            var result = await _context.Database.GetCollection<UiComponent>("uicomponents")
                .UpdateOneAsync(c => c._ID == id, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> IsNameUniqueForProgramAsync(string name, ObjectId? programId, ObjectId? excludeId = null, CancellationToken cancellationToken = default)
        {
            var filter = Builders<UiComponent>.Filter.And(
                Builders<UiComponent>.Filter.Eq(c => c.Name, name),
                Builders<UiComponent>.Filter.Eq(c => c.ProgramId, programId),
                Builders<UiComponent>.Filter.Eq(c => c.IsGlobal, false)
            );

            if (excludeId.HasValue)
            {
                filter = Builders<UiComponent>.Filter.And(
                    filter,
                    Builders<UiComponent>.Filter.Ne(c => c._ID, excludeId.Value)
                );
            }

            var count = await _context.Database.GetCollection<UiComponent>("uicomponents")
                .CountDocumentsAsync(filter, cancellationToken: cancellationToken);

            return count == 0;
        }

        public async Task<bool> IsNameUniqueForGlobalAsync(string name, ObjectId? excludeId = null, CancellationToken cancellationToken = default)
        {
            var filter = Builders<UiComponent>.Filter.And(
                Builders<UiComponent>.Filter.Eq(c => c.Name, name),
                Builders<UiComponent>.Filter.Eq(c => c.IsGlobal, true)
            );

            if (excludeId.HasValue)
            {
                filter = Builders<UiComponent>.Filter.And(
                    filter,
                    Builders<UiComponent>.Filter.Ne(c => c._ID, excludeId.Value)
                );
            }

            var count = await _context.Database.GetCollection<UiComponent>("uicomponents")
                .CountDocumentsAsync(filter, cancellationToken: cancellationToken);

            return count == 0;
        }
    }
}