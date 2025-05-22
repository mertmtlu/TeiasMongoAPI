using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;
using Version = TeiasMongoAPI.Core.Models.Collaboration.Version;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class VersionRepository : GenericRepository<Version>, IVersionRepository
    {
        private readonly MongoDbContext _context;

        public VersionRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<IEnumerable<Version>> GetByProgramIdAsync(ObjectId programId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Version>("versions")
                .Find(v => v.ProgramId == programId)
                .SortByDescending(v => v.VersionNumber)
                .ToListAsync(cancellationToken);
        }

        public async Task<Version> GetLatestVersionForProgramAsync(ObjectId programId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Version>("versions")
                .Find(v => v.ProgramId == programId)
                .SortByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<Version>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Version>("versions")
                .Find(v => v.CreatedBy == creatorId)
                .SortByDescending(v => v.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Version>> GetByReviewerAsync(string reviewerId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Version>("versions")
                .Find(v => v.Reviewer == reviewerId)
                .SortByDescending(v => v.ReviewedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Version>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Version>("versions")
                .Find(v => v.Status == status)
                .SortByDescending(v => v.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Version>> GetPendingReviewsAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Version>("versions")
                .Find(v => v.Status == "pending")
                .SortBy(v => v.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Version> GetByProgramIdAndVersionNumberAsync(ObjectId programId, int versionNumber, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Version>("versions")
                .Find(v => v.ProgramId == programId && v.VersionNumber == versionNumber)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<int> GetNextVersionNumberAsync(ObjectId programId, CancellationToken cancellationToken = default)
        {
            var latestVersion = await GetLatestVersionForProgramAsync(programId, cancellationToken);
            return latestVersion?.VersionNumber + 1 ?? 1;
        }

        public async Task<bool> UpdateStatusAsync(ObjectId id, string status, string reviewerId, string? comments = null, CancellationToken cancellationToken = default)
        {
            var updateBuilder = Builders<Version>.Update
                .Set(v => v.Status, status)
                .Set(v => v.Reviewer, reviewerId)
                .Set(v => v.ReviewedAt, DateTime.UtcNow);

            if (!string.IsNullOrEmpty(comments))
            {
                updateBuilder = updateBuilder.Set(v => v.ReviewComments, comments);
            }

            var result = await _context.Database.GetCollection<Version>("versions")
                .UpdateOneAsync(v => v._ID == id, updateBuilder, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> AddFileAsync(ObjectId id, VersionFile file, CancellationToken cancellationToken = default)
        {
            var update = Builders<Version>.Update.Push(v => v.Files, file);
            var result = await _context.Database.GetCollection<Version>("versions")
                .UpdateOneAsync(v => v._ID == id, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateFileAsync(ObjectId id, string filePath, VersionFile file, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Version>.Filter.And(
                Builders<Version>.Filter.Eq(v => v._ID, id),
                Builders<Version>.Filter.ElemMatch(v => v.Files, f => f.Path == filePath)
            );

            var update = Builders<Version>.Update.Set("files.$", file);

            var result = await _context.Database.GetCollection<Version>("versions")
                .UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> RemoveFileAsync(ObjectId id, string filePath, CancellationToken cancellationToken = default)
        {
            var update = Builders<Version>.Update.PullFilter(v => v.Files, f => f.Path == filePath);
            var result = await _context.Database.GetCollection<Version>("versions")
                .UpdateOneAsync(v => v._ID == id, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<IEnumerable<VersionFile>> GetFilesByVersionIdAsync(ObjectId versionId, CancellationToken cancellationToken = default)
        {
            var version = await _context.Database.GetCollection<Version>("versions")
                .Find(v => v._ID == versionId)
                .FirstOrDefaultAsync(cancellationToken);

            return version?.Files ?? new List<VersionFile>();
        }

        public async Task<VersionFile?> GetFileByPathAsync(ObjectId versionId, string filePath, CancellationToken cancellationToken = default)
        {
            var files = await GetFilesByVersionIdAsync(versionId, cancellationToken);
            return files.FirstOrDefault(f => f.Path == filePath);
        }
    }
}