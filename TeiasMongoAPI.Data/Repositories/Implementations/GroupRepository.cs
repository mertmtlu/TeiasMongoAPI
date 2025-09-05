using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class GroupRepository : GenericRepository<Group>, IGroupRepository
    {
        private readonly MongoDbContext _context;

        public GroupRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<IEnumerable<Group>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Group>("groups")
                .Find(g => g.CreatedBy == creatorId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Group>> GetActiveGroupsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Group>("groups")
                .Find(g => g.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Group>> GetUserGroupsAsync(ObjectId userId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Group>.Filter.And(
                Builders<Group>.Filter.AnyEq(g => g.Members, userId),
                Builders<Group>.Filter.Eq(g => g.IsActive, true)
            );

            return await _context.Database.GetCollection<Group>("groups")
                .Find(filter)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> IsNameUniqueAsync(string name, ObjectId? excludeId = null, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Group>.Filter.Eq(g => g.Name, name);
            
            if (excludeId.HasValue)
            {
                filter = Builders<Group>.Filter.And(
                    filter,
                    Builders<Group>.Filter.Ne(g => g._ID, excludeId.Value)
                );
            }

            var count = await _context.Database.GetCollection<Group>("groups")
                .CountDocumentsAsync(filter, cancellationToken: cancellationToken);

            return count == 0;
        }

        public async Task<bool> AddMemberAsync(ObjectId groupId, ObjectId userId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Group>.Filter.Eq(g => g._ID, groupId);
            var update = Builders<Group>.Update
                .AddToSet(g => g.Members, userId)
                .Set(g => g.ModifiedAt, DateTime.UtcNow);

            var result = await _context.Database.GetCollection<Group>("groups")
                .UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemoveMemberAsync(ObjectId groupId, ObjectId userId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Group>.Filter.Eq(g => g._ID, groupId);
            var update = Builders<Group>.Update
                .Pull(g => g.Members, userId)
                .Set(g => g.ModifiedAt, DateTime.UtcNow);

            var result = await _context.Database.GetCollection<Group>("groups")
                .UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> IsMemberAsync(ObjectId groupId, ObjectId userId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Group>.Filter.And(
                Builders<Group>.Filter.Eq(g => g._ID, groupId),
                Builders<Group>.Filter.AnyEq(g => g.Members, userId)
            );

            var count = await _context.Database.GetCollection<Group>("groups")
                .CountDocumentsAsync(filter, cancellationToken: cancellationToken);

            return count > 0;
        }

        public async Task<IEnumerable<ObjectId>> GetGroupMembersAsync(ObjectId groupId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Group>.Filter.Eq(g => g._ID, groupId);
            var projection = Builders<Group>.Projection.Include(g => g.Members);

            var group = await _context.Database.GetCollection<Group>("groups")
                .Find(filter)
                .Project<Group>(projection)
                .FirstOrDefaultAsync(cancellationToken);

            return group?.Members ?? new List<ObjectId>();
        }

        public async Task<int> GetMemberCountAsync(ObjectId groupId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Group>.Filter.Eq(g => g._ID, groupId);
            var projection = Builders<Group>.Projection.Include(g => g.Members);

            var group = await _context.Database.GetCollection<Group>("groups")
                .Find(filter)
                .Project<Group>(projection)
                .FirstOrDefaultAsync(cancellationToken);

            return group?.Members.Count ?? 0;
        }

        public async Task<bool> UpdateStatusAsync(ObjectId groupId, bool isActive, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Group>.Filter.Eq(g => g._ID, groupId);
            var update = Builders<Group>.Update
                .Set(g => g.IsActive, isActive)
                .Set(g => g.ModifiedAt, DateTime.UtcNow);

            var result = await _context.Database.GetCollection<Group>("groups")
                .UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }
    }
}