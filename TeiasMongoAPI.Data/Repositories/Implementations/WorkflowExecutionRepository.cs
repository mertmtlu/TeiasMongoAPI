using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Linq.Expressions;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class WorkflowExecutionRepository : GenericRepository<WorkflowExecution>, IWorkflowExecutionRepository
    {
        public WorkflowExecutionRepository(MongoDbContext context) : base(context.Database)
        {
        }

        public async Task<IEnumerable<WorkflowExecution>> GetExecutionsByWorkflowIdAsync(ObjectId workflowId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e.WorkflowId, workflowId);
            return await _collection.Find(filter)
                .SortByDescending(e => e.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<WorkflowExecution>> GetExecutionsByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e.ExecutedBy, userId);
            return await _collection.Find(filter)
                .SortByDescending(e => e.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<WorkflowExecution>> GetExecutionsByStatusAsync(WorkflowExecutionStatus status, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e.Status, status);
            return await _collection.Find(filter)
                .SortByDescending(e => e.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<WorkflowExecution>> GetRunningExecutionsAsync(CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.In(e => e.Status,
                new[] { WorkflowExecutionStatus.Running, WorkflowExecutionStatus.Pending });
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<WorkflowExecution>> GetExecutionsWithPaginationAsync(int skip, int take, Expression<Func<WorkflowExecution, bool>>? filter = null, CancellationToken cancellationToken = default)
        {
            var mongoFilter = filter != null ? Builders<WorkflowExecution>.Filter.Where(filter) : Builders<WorkflowExecution>.Filter.Empty;

            return await _collection.Find(mongoFilter)
                .Skip(skip)
                .Limit(take)
                .SortByDescending(e => e.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<WorkflowExecution> GetLatestExecutionAsync(ObjectId workflowId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e.WorkflowId, workflowId);
            return await _collection.Find(filter)
                .SortByDescending(e => e.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<WorkflowExecution>> GetExecutionHistoryAsync(ObjectId workflowId, int limit = 10, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e.WorkflowId, workflowId);
            return await _collection.Find(filter)
                .SortByDescending(e => e.StartedAt)
                .Limit(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> UpdateExecutionStatusAsync(ObjectId executionId, WorkflowExecutionStatus status, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e._ID, executionId);
            var update = Builders<WorkflowExecution>.Update
                .Set(e => e.Status, status);

            if (status == WorkflowExecutionStatus.Completed || status == WorkflowExecutionStatus.Failed || status == WorkflowExecutionStatus.Cancelled)
            {
                update = update.Set(e => e.CompletedAt, DateTime.UtcNow);
            }

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateExecutionProgressAsync(ObjectId executionId, WorkflowExecutionProgress progress, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e._ID, executionId);
            var update = Builders<WorkflowExecution>.Update.Set(e => e.Progress, progress);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> AddNodeExecutionAsync(ObjectId executionId, NodeExecution nodeExecution, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e._ID, executionId);
            var update = Builders<WorkflowExecution>.Update.Push(e => e.NodeExecutions, nodeExecution);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateNodeExecutionAsync(ObjectId executionId, string nodeId, NodeExecution nodeExecution, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.And(
                Builders<WorkflowExecution>.Filter.Eq(e => e._ID, executionId),
                Builders<WorkflowExecution>.Filter.ElemMatch(e => e.NodeExecutions, n => n.NodeId == nodeId)
            );

            var update = Builders<WorkflowExecution>.Update.Set("NodeExecutions.$", nodeExecution);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> AddExecutionLogAsync(ObjectId executionId, WorkflowExecutionLog log, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e._ID, executionId);
            var update = Builders<WorkflowExecution>.Update.Push(e => e.Logs, log);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> SetExecutionResultsAsync(ObjectId executionId, WorkflowExecutionResults results, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e._ID, executionId);
            var update = Builders<WorkflowExecution>.Update.Set(e => e.Results, results);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> SetExecutionErrorAsync(ObjectId executionId, WorkflowExecutionError error, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e._ID, executionId);
            var update = Builders<WorkflowExecution>.Update.Set(e => e.Error, error);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateResourceUsageAsync(ObjectId executionId, WorkflowResourceUsage resourceUsage, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e._ID, executionId);
            var update = Builders<WorkflowExecution>.Update.Set(e => e.ResourceUsage, resourceUsage);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<IEnumerable<WorkflowExecution>> GetExecutionsByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.And(
                Builders<WorkflowExecution>.Filter.Gte(e => e.StartedAt, startDate),
                Builders<WorkflowExecution>.Filter.Lte(e => e.StartedAt, endDate)
            );

            return await _collection.Find(filter)
                .SortByDescending(e => e.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<WorkflowExecution> GetExecutionWithDetailsAsync(ObjectId executionId, CancellationToken cancellationToken = default)
        {
            return await GetByIdAsync(executionId, cancellationToken);
        }

        public async Task<bool> CancelExecutionAsync(ObjectId executionId, CancellationToken cancellationToken = default)
        {
            return await UpdateExecutionStatusAsync(executionId, WorkflowExecutionStatus.Cancelled, cancellationToken);
        }

        public async Task<bool> PauseExecutionAsync(ObjectId executionId, CancellationToken cancellationToken = default)
        {
            return await UpdateExecutionStatusAsync(executionId, WorkflowExecutionStatus.Paused, cancellationToken);
        }

        public async Task<bool> ResumeExecutionAsync(ObjectId executionId, CancellationToken cancellationToken = default)
        {
            return await UpdateExecutionStatusAsync(executionId, WorkflowExecutionStatus.Running, cancellationToken);
        }

        public async Task<IEnumerable<WorkflowExecution>> GetFailedExecutionsAsync(CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e.Status, WorkflowExecutionStatus.Failed);
            return await _collection.Find(filter)
                .SortByDescending(e => e.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<WorkflowExecution>> GetExecutionsForRetryAsync(CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.And(
                Builders<WorkflowExecution>.Filter.Eq(e => e.Status, WorkflowExecutionStatus.Failed),
                Builders<WorkflowExecution>.Filter.Exists(e => e.Error.CanRetry, true),
                Builders<WorkflowExecution>.Filter.Eq(e => e.Error.CanRetry, true)
            );

            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<bool> MarkExecutionForRetryAsync(ObjectId executionId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.Eq(e => e._ID, executionId);
            var update = Builders<WorkflowExecution>.Update
                .Set(e => e.IsRerun, true)
                .Inc(e => e.Error.RetryCount, 1);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<Dictionary<string, int>> GetExecutionStatisticsByStatusAsync(CancellationToken cancellationToken = default)
        {
            var pipeline = new[]
            {
                new BsonDocument("$group", new BsonDocument
                {
                    { "_id", "$Status" },
                    { "count", new BsonDocument("$sum", 1) }
                })
            };

            var result = await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);

            var statistics = new Dictionary<string, int>();
            foreach (var doc in result)
            {
                var status = doc["_id"].AsString;
                var count = doc["count"].AsInt32;
                statistics[status] = count;
            }

            return statistics;
        }

        public async Task<IEnumerable<WorkflowExecution>> GetLongRunningExecutionsAsync(TimeSpan threshold, CancellationToken cancellationToken = default)
        {
            var cutoffTime = DateTime.UtcNow - threshold;
            var filter = Builders<WorkflowExecution>.Filter.And(
                Builders<WorkflowExecution>.Filter.Eq(e => e.Status, WorkflowExecutionStatus.Running),
                Builders<WorkflowExecution>.Filter.Lt(e => e.StartedAt, cutoffTime)
            );

            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<bool> CleanupOldExecutionsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
        {
            var filter = Builders<WorkflowExecution>.Filter.And(
                Builders<WorkflowExecution>.Filter.Lt(e => e.StartedAt, cutoffDate),
                Builders<WorkflowExecution>.Filter.In(e => e.Status,
                    new[] { WorkflowExecutionStatus.Completed, WorkflowExecutionStatus.Failed, WorkflowExecutionStatus.Cancelled })
            );

            var result = await _collection.DeleteManyAsync(filter, cancellationToken);
            return result.DeletedCount > 0;
        }
    }
}