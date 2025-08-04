using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class UIInteractionRepository : GenericRepository<UIInteraction>, IUIInteractionRepository
    {
        private readonly ILogger<UIInteractionRepository> _logger;

        public UIInteractionRepository(MongoDbContext context, ILogger<UIInteractionRepository> logger)
            : base(context.Database)
        {
            _logger = logger;
        }

        // Constructor for UnitOfWork compatibility (without logger)
        public UIInteractionRepository(MongoDbContext context)
            : base(context.Database)
        {
            _logger = new NullLogger<UIInteractionRepository>();
        }

        public async Task<List<UIInteraction>> GetPendingForUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                // For now, we'll get all pending interactions
                // In a real implementation, you'd filter by user permissions based on workflow execution
                var filter = Builders<UIInteraction>.Filter.In(x => x.Status, 
                    new[] { UIInteractionStatus.Pending, UIInteractionStatus.InProgress });

                var interactions = await _collection
                    .Find(filter)
                    .SortBy(x => x.CreatedAt)
                    .ToListAsync(cancellationToken);

                // TODO: Filter by user permissions - check if user has access to the workflow executions
                return interactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending UI interactions for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<UIInteraction>> GetByWorkflowExecutionAsync(ObjectId workflowExecutionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var filter = Builders<UIInteraction>.Filter.Eq(x => x.WorkflowExecutionId, workflowExecutionId);
                
                return await _collection
                    .Find(filter)
                    .SortBy(x => x.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting UI interactions for workflow execution {ExecutionId}", workflowExecutionId);
                throw;
            }
        }

        public async Task<UIInteraction?> GetByIdAsync(string interactionId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ObjectId.TryParse(interactionId, out var objectId))
                {
                    return null;
                }

                var filter = Builders<UIInteraction>.Filter.Eq(x => x._ID, objectId);
                return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting UI interaction by ID {InteractionId}", interactionId);
                throw;
            }
        }

        public async Task<bool> UpdateStatusAsync(ObjectId interactionId, UIInteractionStatus status, BsonDocument? outputData = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var filter = Builders<UIInteraction>.Filter.Eq(x => x._ID, interactionId);
                var updateBuilder = Builders<UIInteraction>.Update.Set(x => x.Status, status);

                if (status == UIInteractionStatus.Completed || status == UIInteractionStatus.Cancelled || status == UIInteractionStatus.Timeout)
                {
                    updateBuilder = updateBuilder.Set(x => x.CompletedAt, DateTime.UtcNow);
                }

                if (outputData != null)
                {
                    updateBuilder = updateBuilder.Set(x => x.OutputData, outputData);
                }

                var result = await _collection.UpdateOneAsync(filter, updateBuilder, cancellationToken: cancellationToken);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating UI interaction status {InteractionId} to {Status}", interactionId, status);
                throw;
            }
        }

        public async Task<List<UIInteraction>> GetActiveInteractionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var filter = Builders<UIInteraction>.Filter.In(x => x.Status, 
                    new[] { UIInteractionStatus.Pending, UIInteractionStatus.InProgress });

                return await _collection
                    .Find(filter)
                    .SortBy(x => x.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active UI interactions");
                throw;
            }
        }

        public async Task<List<UIInteraction>> GetTimedOutInteractionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var now = DateTime.UtcNow;
                var filter = Builders<UIInteraction>.Filter.And(
                    Builders<UIInteraction>.Filter.In(x => x.Status, new[] { UIInteractionStatus.Pending, UIInteractionStatus.InProgress }),
                    Builders<UIInteraction>.Filter.Ne(x => x.Timeout, null),
                    Builders<UIInteraction>.Filter.Lt(x => x.CreatedAt.Add(x.Timeout!.Value), now)
                );

                return await _collection
                    .Find(filter)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting timed out UI interactions");
                throw;
            }
        }
    }
}