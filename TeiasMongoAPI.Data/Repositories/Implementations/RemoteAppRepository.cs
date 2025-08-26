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
                Builders<RemoteApp>.Filter.AnyEq(x => x.AssignedUsers, userId),
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

        public async Task<bool> IsUserAssignedAsync(ObjectId remoteAppId, ObjectId userId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<RemoteApp>.Filter.And(
                Builders<RemoteApp>.Filter.Eq(x => x._ID, remoteAppId),
                Builders<RemoteApp>.Filter.AnyEq(x => x.AssignedUsers, userId)
            );

            var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
            return count > 0;
        }

        public async Task<bool> AddUserAssignmentAsync(ObjectId remoteAppId, ObjectId userId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<RemoteApp>.Filter.Eq(x => x._ID, remoteAppId);
            var update = Builders<RemoteApp>.Update
                .AddToSet(x => x.AssignedUsers, userId)
                .Set(x => x.ModifiedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemoveUserAssignmentAsync(ObjectId remoteAppId, ObjectId userId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<RemoteApp>.Filter.Eq(x => x._ID, remoteAppId);
            var update = Builders<RemoteApp>.Update
                .Pull(x => x.AssignedUsers, userId)
                .Set(x => x.ModifiedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }
    }
}