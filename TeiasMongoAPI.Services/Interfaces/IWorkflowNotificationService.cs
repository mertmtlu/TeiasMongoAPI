using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.UIWorkflow;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IWorkflowNotificationService
    {
        Task NotifyUIInteractionCreatedAsync(string workflowId, UIInteractionCreatedEventArgs args, CancellationToken cancellationToken = default);
        Task NotifyUIInteractionCreatedWithPayloadAsync(string workflowId, UIInteractionCreatedWithPayloadEventArgs args, CancellationToken cancellationToken = default);
        Task NotifyUIInteractionStatusChangedAsync(string workflowId, UIInteractionStatusChangedEventArgs args, CancellationToken cancellationToken = default);
        Task NotifyUIInteractionAvailableAsync(string workflowId, UIInteractionAvailableEventArgs args, CancellationToken cancellationToken = default);
    }

    public class UIInteractionCreatedEventArgs
    {
        public required string InteractionId { get; set; }
        public required string ExecutionId { get; set; } // ADD THIS
        public required string NodeId { get; set; }
        public required string InteractionType { get; set; }
        public required string Status { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required Dictionary<string, object> InputSchema { get; set; }
        public Dictionary<string, object>? ContextData { get; set; } // ADD THIS
        public required DateTime CreatedAt { get; set; }
        public TimeSpan? Timeout { get; set; }
    }

    public class UIInteractionStatusChangedEventArgs
    {
        public required string InteractionId { get; set; }
        public required string ExecutionId { get; set; } // ADD THIS
        public required string Status { get; set; }
        public Dictionary<string, object>? OutputData { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class UIInteractionAvailableEventArgs
    {
        public required string NodeId { get; set; }
        public required string InteractionId { get; set; }
        public required DateTime Timestamp { get; set; }
    }

    public class UIInteractionCreatedWithPayloadEventArgs
    {
        public required string InteractionId { get; set; }
        public required string ExecutionId { get; set; }
        public required string NodeId { get; set; }
        public required string InteractionType { get; set; }
        public required string Status { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required Dictionary<string, object> InputSchema { get; set; }
        public Dictionary<string, object>? ContextData { get; set; }
        public required DateTime CreatedAt { get; set; }
        public TimeSpan? Timeout { get; set; }
        public required UIInteractionSession SessionPayload { get; set; } // Complete session data
    }
}