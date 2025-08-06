using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public class UIInteraction : AEntityBase
    {
        [BsonElement("workflowExecutionId")]
        public required ObjectId WorkflowExecutionId { get; set; }

        [BsonElement("nodeId")]
        public required string NodeId { get; set; }

        [BsonElement("componentId")]
        public ObjectId? ComponentId { get; set; }

        [BsonElement("interactionType")]
        public UIInteractionType InteractionType { get; set; }

        [BsonElement("status")]
        public UIInteractionStatus Status { get; set; } = UIInteractionStatus.Pending;

        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("inputSchema")]
        public Dictionary<string, object> InputSchema { get; set; } = new();

        [BsonElement("inputData")]
        public Dictionary<string, object> InputData { get; set; } = new();

        [BsonElement("outputData")]
        public Dictionary<string, object> OutputData { get; set; } = new();

        [BsonElement("contextData")]
        public Dictionary<string, object>? ContextData { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [BsonElement("timeout")]
        public TimeSpan? Timeout { get; set; }

        [BsonElement("metadata")]
        public BsonDocument Metadata { get; set; } = new();

        [BsonElement("uiComponentId")]
        public string? UiComponentId { get; set; }

        [BsonElement("uiComponentConfiguration")]
        public BsonDocument? UiComponentConfiguration { get; set; }
    }

    public enum UIInteractionType
    {
        UserInput,
        Confirmation,
        Selection,
        FileUpload,
        DataReview,
        Custom
    }

    public enum UIInteractionStatus
    {
        Pending,
        InProgress,
        Completed,
        Cancelled,
        Timeout
    }
}