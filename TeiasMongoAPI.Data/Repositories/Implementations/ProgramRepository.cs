using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class ProgramRepository : GenericRepository<Program>, IProgramRepository
    {
        private readonly MongoDbContext _context;

        public ProgramRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<IEnumerable<Program>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Program>("programs")
                .Find(p => p.CreatorId == creatorId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Program>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Program>("programs")
                .Find(p => p.Status == status)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Program>> GetByTypeAsync(string type, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Program>("programs")
                .Find(p => p.Type == type)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Program>> GetByLanguageAsync(string language, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Program>("programs")
                .Find(p => p.Language == language)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Program>> GetUserAccessibleProgramsAsync(string userId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Program>.Filter.Or(
                Builders<Program>.Filter.Eq(p => p.CreatorId, userId),
                Builders<Program>.Filter.ElemMatch(p => p.Permissions.Users,
                    Builders<UserPermission>.Filter.Eq(up => up.UserId, userId))
            );

            return await _context.Database.GetCollection<Program>("programs")
                .Find(filter)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Program>> GetGroupAccessibleProgramsAsync(string groupId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Program>.Filter.ElemMatch(p => p.Permissions.Groups,
                Builders<GroupPermission>.Filter.Eq(gp => gp.GroupId, groupId));

            return await _context.Database.GetCollection<Program>("programs")
                .Find(filter)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> UpdateStatusAsync(ObjectId id, string status, CancellationToken cancellationToken = default)
        {
            var update = Builders<Program>.Update.Set(p => p.Status, status);
            var result = await _context.Database.GetCollection<Program>("programs")
                .UpdateOneAsync(p => p._ID == id, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateCurrentVersionAsync(ObjectId id, string versionId, CancellationToken cancellationToken = default)
        {
            var update = Builders<Program>.Update.Set(p => p.CurrentVersion, versionId);
            var result = await _context.Database.GetCollection<Program>("programs")
                .UpdateOneAsync(p => p._ID == id, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> IsNameUniqueAsync(string name, ObjectId? excludeId = null, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Program>.Filter.Eq(p => p.Name, name);

            if (excludeId.HasValue)
            {
                filter = Builders<Program>.Filter.And(
                    filter,
                    Builders<Program>.Filter.Ne(p => p._ID, excludeId.Value)
                );
            }

            var count = await _context.Database.GetCollection<Program>("programs")
                .CountDocumentsAsync(filter, cancellationToken: cancellationToken);

            return count == 0;
        }

        public async Task<bool> AddUserPermissionAsync(ObjectId programId, string userId, string accessLevel, CancellationToken cancellationToken = default)
        {
            var permission = new UserPermission { UserId = userId, AccessLevel = accessLevel };
            var update = Builders<Program>.Update.Push(p => p.Permissions.Users, permission);

            var result = await _context.Database.GetCollection<Program>("programs")
                .UpdateOneAsync(p => p._ID == programId, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemoveUserPermissionAsync(ObjectId programId, string userId, CancellationToken cancellationToken = default)
        {
            var update = Builders<Program>.Update.PullFilter(p => p.Permissions.Users,
                Builders<UserPermission>.Filter.Eq(up => up.UserId, userId));

            var result = await _context.Database.GetCollection<Program>("programs")
                .UpdateOneAsync(p => p._ID == programId, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateUserPermissionAsync(ObjectId programId, string userId, string accessLevel, CancellationToken cancellationToken = default)
        {
            // Remove existing permission and add new one
            await RemoveUserPermissionAsync(programId, userId, cancellationToken);
            return await AddUserPermissionAsync(programId, userId, accessLevel, cancellationToken);
        }

        public async Task<bool> AddGroupPermissionAsync(ObjectId programId, string groupId, string accessLevel, CancellationToken cancellationToken = default)
        {
            var permission = new GroupPermission { GroupId = groupId, AccessLevel = accessLevel };
            var update = Builders<Program>.Update.Push(p => p.Permissions.Groups, permission);

            var result = await _context.Database.GetCollection<Program>("programs")
                .UpdateOneAsync(p => p._ID == programId, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemoveGroupPermissionAsync(ObjectId programId, string groupId, CancellationToken cancellationToken = default)
        {
            var update = Builders<Program>.Update.PullFilter(p => p.Permissions.Groups,
                Builders<GroupPermission>.Filter.Eq(gp => gp.GroupId, groupId));

            var result = await _context.Database.GetCollection<Program>("programs")
                .UpdateOneAsync(p => p._ID == programId, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateGroupPermissionAsync(ObjectId programId, string groupId, string accessLevel, CancellationToken cancellationToken = default)
        {
            // Remove existing permission and add new one
            await RemoveGroupPermissionAsync(programId, groupId, cancellationToken);
            return await AddGroupPermissionAsync(programId, groupId, accessLevel, cancellationToken);
        }
    }
}