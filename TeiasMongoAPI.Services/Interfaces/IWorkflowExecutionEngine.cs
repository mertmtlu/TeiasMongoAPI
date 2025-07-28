using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IWorkflowExecutionEngine
    {
        Task<WorkflowExecutionResponseDto> ExecuteWorkflowAsync(WorkflowExecutionRequest request, ObjectId currentUserId, CancellationToken cancellationToken = default);
        Task<WorkflowExecutionResponseDto> ResumeWorkflowAsync(string executionId, CancellationToken cancellationToken = default);
        Task<bool> PauseWorkflowAsync(string executionId, CancellationToken cancellationToken = default);
        Task<bool> CancelWorkflowAsync(string executionId, CancellationToken cancellationToken = default);
        Task<WorkflowExecutionResponseDto> GetExecutionStatusAsync(string executionId, CancellationToken cancellationToken = default);
        Task<List<WorkflowExecutionResponseDto>> GetActiveExecutionsAsync(CancellationToken cancellationToken = default);
        Task<NodeExecutionResponseDto> ExecuteNodeAsync(string executionId, string nodeId, CancellationToken cancellationToken = default);
        Task<NodeExecutionResponseDto> RetryNodeAsync(string executionId, string nodeId, CancellationToken cancellationToken = default);
        Task<bool> SkipNodeAsync(string executionId, string nodeId, string reason, CancellationToken cancellationToken = default);
        Task<WorkflowDataContractDto> GetNodeOutputAsync(string executionId, string nodeId, CancellationToken cancellationToken = default);
        Task<Dictionary<string, WorkflowDataContractDto>> GetAllNodeOutputsAsync(string executionId, CancellationToken cancellationToken = default);
        Task<WorkflowExecutionStatisticsResponseDto> GetExecutionStatisticsAsync(string executionId, CancellationToken cancellationToken = default);
        Task<List<WorkflowExecutionLogResponseDto>> GetExecutionLogsAsync(string executionId, int skip = 0, int take = 100, CancellationToken cancellationToken = default);
        Task<bool> IsExecutionCompleteAsync(string executionId, CancellationToken cancellationToken = default);
        Task<bool> CleanupExecutionAsync(string executionId, CancellationToken cancellationToken = default);
        Task<VersionFileDetailDto> DownloadExecutionFileAsync(string executionId, string filePath, CancellationToken cancellationToken = default);
        Task<BulkDownloadResult> DownloadAllExecutionFilesAsync(string executionId, CancellationToken cancellationToken = default);
        Task<BulkDownloadResult> BulkDownloadExecutionFilesAsync(string executionId, WorkflowExecutionFileBulkDownloadRequest request, CancellationToken cancellationToken = default);
    }

    public class WorkflowExecutionRequest
    {
        public required string WorkflowId { get; set; }
        public string? WorkflowVersionId { get; set; }
        public string ExecutionName { get; set; } = string.Empty;
        public WorkflowExecutionContextDto ExecutionContext { get; set; } = new();
        public WorkflowExecutionOptions Options { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class WorkflowExecutionOptions
    {
        [BsonElement("dryRun")]
        public bool DryRun { get; set; } = false;
        
        [BsonElement("debugMode")]
        public bool DebugMode { get; set; } = false;
        
        [BsonElement("saveIntermediateResults")]
        public bool SaveIntermediateResults { get; set; } = true;
        
        [BsonElement("continueOnError")]
        public bool ContinueOnError { get; set; } = false;
        
        [BsonElement("maxConcurrentNodes")]
        public int MaxConcurrentNodes { get; set; } = 5;
        
        [BsonElement("timeoutMinutes")]
        public int TimeoutMinutes { get; set; } = 60;
        
        [BsonElement("enableNotifications")]
        public bool EnableNotifications { get; set; } = true;
        
        [BsonElement("notificationRecipients")]
        public List<string> NotificationRecipients { get; set; } = new();
        
        [BsonElement("priority")]
        public ExecutionPriority Priority { get; set; } = ExecutionPriority.Normal;
        
        [BsonElement("customOptions")]
        public Dictionary<string, object> CustomOptions { get; set; } = new();
    }

    public enum ExecutionPriority
    {
        Low,
        Normal,
        High,
        Critical
    }
}