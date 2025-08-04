namespace TeiasMongoAPI.Services.DTOs.Response.UIWorkflow
{
    public class UIInteractionSessionApiResponse
    {
        public required string SessionId { get; set; }
        public required string WorkflowId { get; set; }
        public required string ExecutionId { get; set; }
        public required string NodeId { get; set; }
        public required string Status { get; set; }
        public required string InteractionType { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required Dictionary<string, object> InputSchema { get; set; }
        public Dictionary<string, object>? InputData { get; set; }
        public Dictionary<string, object>? OutputData { get; set; }
        public Dictionary<string, object> ContextData { get; set; } = new();
        public DateTime? TimeoutAt { get; set; }
        public required DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class UIInteractionSessionListApiResponse
    {
        public required List<UIInteractionSessionApiResponse> Sessions { get; set; }
        public required int TotalCount { get; set; }
    }

    public class UIInteractionDetailApiResponse
    {
        public required UIInteractionSessionApiResponse Session { get; set; }
        public WorkflowContextInfo? WorkflowContext { get; set; }
    }

    public class WorkflowContextInfo
    {
        public required string WorkflowName { get; set; }
        public required string ExecutionName { get; set; }
        public required string NodeName { get; set; }
        public required string ExecutedBy { get; set; }
        public required DateTime ExecutionStartedAt { get; set; }
        public string? ExecutionStatus { get; set; }
    }

    public class UIInteractionSession
    {
        public required string SessionId { get; set; }
        public required string WorkflowId { get; set; }
        public required string ExecutionId { get; set; }
        public required string NodeId { get; set; }
        public required string Status { get; set; }
        public required string InteractionType { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required Dictionary<string, object> InputSchema { get; set; }
        public Dictionary<string, object>? InputData { get; set; }
        public Dictionary<string, object>? OutputData { get; set; }
        public Dictionary<string, object> ContextData { get; set; } = new();
        public DateTime? TimeoutAt { get; set; }
        public required DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}