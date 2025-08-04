using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IWorkflowNotificationService
    {
        Task NotifyUIInteractionCreatedAsync(string workflowId, UIInteractionCreatedEventArgs args, CancellationToken cancellationToken = default);
        Task NotifyUIInteractionStatusChangedAsync(string workflowId, UIInteractionStatusChangedEventArgs args, CancellationToken cancellationToken = default);
        Task NotifyUIInteractionAvailableAsync(string workflowId, UIInteractionAvailableEventArgs args, CancellationToken cancellationToken = default);
    }

    public class UIInteractionCreatedEventArgs
    {
        public required string InteractionId { get; set; }
        public required string NodeId { get; set; }
        public required string InteractionType { get; set; }
        public required string Status { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required BsonDocument InputSchema { get; set; }
        public required DateTime CreatedAt { get; set; }
        public TimeSpan? Timeout { get; set; }
    }

    public class UIInteractionStatusChangedEventArgs
    {
        public required string InteractionId { get; set; }
        public required string Status { get; set; }
        public BsonDocument? OutputData { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class UIInteractionAvailableEventArgs
    {
        public required string NodeId { get; set; }
        public required string InteractionId { get; set; }
        public required DateTime Timestamp { get; set; }
    }
}