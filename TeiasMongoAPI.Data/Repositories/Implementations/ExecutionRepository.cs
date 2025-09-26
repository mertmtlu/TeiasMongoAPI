using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class ExecutionRepository : GenericRepository<Execution>, IExecutionRepository
    {
        private readonly MongoDbContext _context;

        public ExecutionRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<IEnumerable<Execution>> GetByProgramIdAsync(ObjectId programId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Execution>("executions")
                .Find(e => e.ProgramId == programId)
                .SortByDescending(e => e.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Execution>> GetByVersionIdAsync(ObjectId versionId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Execution>("executions")
                .Find(e => e.VersionId == versionId)
                .SortByDescending(e => e.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Execution>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Execution>("executions")
                .Find(e => e.UserId == userId)
                .SortByDescending(e => e.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Execution>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Execution>("executions")
                .Find(e => e.Status == status)
                .SortByDescending(e => e.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Execution>> GetRunningExecutionsAsync(CancellationToken cancellationToken = default)
        {
            return await GetByStatusAsync("running", cancellationToken);
        }

        public async Task<IEnumerable<Execution>> GetCompletedExecutionsAsync(CancellationToken cancellationToken = default)
        {
            return await GetByStatusAsync("completed", cancellationToken);
        }

        public async Task<IEnumerable<Execution>> GetFailedExecutionsAsync(CancellationToken cancellationToken = default)
        {
            return await GetByStatusAsync("failed", cancellationToken);
        }

        public async Task<IEnumerable<Execution>> GetRecentExecutionsAsync(int count, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Execution>("executions")
                .Find(_ => true)
                .SortByDescending(e => e.StartedAt)
                .Limit(count)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Execution>> GetRecentExecutionsAsync(int count, string? userId, CancellationToken cancellationToken = default)
        {
            var collection = _context.Database.GetCollection<Execution>("executions");

            if (string.IsNullOrEmpty(userId))
            {
                // Return all executions if no userId is specified
                return await collection
                    .Find(_ => true)
                    .SortByDescending(e => e.StartedAt)
                    .Limit(count)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                // Filter by userId if specified
                return await collection
                    .Find(e => e.UserId == userId)
                    .SortByDescending(e => e.StartedAt)
                    .Limit(count)
                    .ToListAsync(cancellationToken);
            }
        }

        public async Task<bool> UpdateStatusAsync(ObjectId id, string status, CancellationToken cancellationToken = default)
        {
            var updateBuilder = Builders<Execution>.Update.Set(e => e.Status, status);

            if (status == "completed" || status == "failed")
            {
                updateBuilder = updateBuilder.Set(e => e.CompletedAt, DateTime.UtcNow);
            }

            var result = await _context.Database.GetCollection<Execution>("executions")
                .UpdateOneAsync(e => e._ID == id, updateBuilder, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> CompleteExecutionAsync(ObjectId id, int exitCode, string output, List<string> outputFiles, string? error = null, CancellationToken cancellationToken = default)
        {
            var results = new ExecutionResults
            {
                ExitCode = exitCode,
                Output = output,
                OutputFiles = outputFiles,
                Error = error
            };

            var update = Builders<Execution>.Update
                .Set(e => e.Status, exitCode == 0 ? "completed" : "failed")
                .Set(e => e.CompletedAt, DateTime.UtcNow)
                .Set(e => e.Results, results);

            var result = await _context.Database.GetCollection<Execution>("executions")
                .UpdateOneAsync(e => e._ID == id, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateResourceUsageAsync(ObjectId id, double cpuTime, long memoryUsed, long diskUsed, CancellationToken cancellationToken = default)
        {
            var resourceUsage = new ResourceUsage
            {
                CpuTime = cpuTime,
                MemoryUsed = memoryUsed,
                DiskUsed = diskUsed
            };

            var update = Builders<Execution>.Update.Set(e => e.ResourceUsage, resourceUsage);
            var result = await _context.Database.GetCollection<Execution>("executions")
                .UpdateOneAsync(e => e._ID == id, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<int> CleanupOldExecutionsAsync(int daysToKeep, CancellationToken cancellationToken = default)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            var filter = Builders<Execution>.Filter.And(
                Builders<Execution>.Filter.Lt(e => e.StartedAt, cutoffDate),
                Builders<Execution>.Filter.In(e => e.Status, new[] { "completed", "failed" })
            );

            var result = await _context.Database.GetCollection<Execution>("executions")
                .DeleteManyAsync(filter, cancellationToken);

            return (int)result.DeletedCount;
        }
    }
}