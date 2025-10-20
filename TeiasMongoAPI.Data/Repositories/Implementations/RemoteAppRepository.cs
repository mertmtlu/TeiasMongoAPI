using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class RemoteAppRepository : GenericRepository<RemoteApp>, IRemoteAppRepository
    {
        private readonly MongoDbContext _context;

        public RemoteAppRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<IEnumerable<RemoteApp>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<RemoteApp>.Filter.Eq(x => x.Creator, creatorId);
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<RemoteApp>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            var filter = Builders<RemoteApp>.Filter.Eq(x => x.Status, status);
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<RemoteApp>> GetPublicAppsAsync(CancellationToken cancellationToken = default)
        {
            var filter = Builders<RemoteApp>.Filter.Eq(x => x.IsPublic, true);
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<RemoteApp>> GetUserAccessibleAppsAsync(ObjectId userId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<RemoteApp>.Filter.Or(
                Builders<RemoteApp>.Filter.Eq(x => x.IsPublic, true),
                Builders<RemoteApp>.Filter.ElemMatch(x => x.Permissions.Users, u => u.UserId == userId.ToString()),
                Builders<RemoteApp>.Filter.Eq(x => x.Creator, userId.ToString())
            );
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<bool> IsNameUniqueAsync(string name, ObjectId? excludeId = null, CancellationToken cancellationToken = default)
        {
            var filterBuilder = Builders<RemoteApp>.Filter;
            var filter = filterBuilder.Eq(x => x.Name, name);

            if (excludeId.HasValue)
            {
                filter &= filterBuilder.Ne(x => x._ID, excludeId.Value);
            }

            var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            return count == 0;
        }
    }
}