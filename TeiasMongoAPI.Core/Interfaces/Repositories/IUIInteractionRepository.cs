using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IUIInteractionRepository : IGenericRepository<UIInteraction>
    {
        Task<List<UIInteraction>> GetPendingForUserAsync(string userId, CancellationToken cancellationToken = default);
        Task<List<UIInteraction>> GetByWorkflowExecutionAsync(ObjectId workflowExecutionId, CancellationToken cancellationToken = default);
        Task<UIInteraction?> GetByIdAsync(string interactionId, CancellationToken cancellationToken = default);
        Task<bool> UpdateStatusAsync(ObjectId interactionId, UIInteractionStatus status, BsonDocument? outputData = null, CancellationToken cancellationToken = default);
        Task<List<UIInteraction>> GetActiveInteractionsAsync(CancellationToken cancellationToken = default);
        Task<List<UIInteraction>> GetTimedOutInteractionsAsync(CancellationToken cancellationToken = default);
    }
}