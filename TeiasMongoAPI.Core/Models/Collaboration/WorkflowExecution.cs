using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public class WorkflowExecution : AEntityBase
    {
        [BsonElement("workflowId")]
        public required ObjectId WorkflowId { get; set; }

        [BsonElement("workflowVersion")]
        public int WorkflowVersion { get; set; } = 1;

        [BsonElement("executionName")]
        public string ExecutionName { get; set; } = string.Empty;

        [BsonElement("executedBy")]
        public required string ExecutedBy { get; set; } // User ID

        [BsonElement("startedAt")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [BsonElement("status")]
        public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Pending;

        [BsonElement("progress")]
        public WorkflowExecutionProgress Progress { get; set; } = new();

        [BsonElement("nodeExecutions")]
        public List<NodeExecution> NodeExecutions { get; set; } = new();

        [BsonElement("executionContext")]
        public WorkflowExecutionContext ExecutionContext { get; set; } = new();

        [BsonElement("results")]
        public WorkflowExecutionResults Results { get; set; } = new();

        [BsonElement("error")]
        public WorkflowExecutionError? Error { get; set; }

        [BsonElement("metadata")]
        public BsonDocument Metadata { get; set; } = new();

        [BsonElement("resourceUsage")]
        public WorkflowResourceUsage ResourceUsage { get; set; } = new();

        [BsonElement("logs")]
        public List<WorkflowExecutionLog> Logs { get; set; } = new();

        [BsonElement("isRerun")]
        public bool IsRerun { get; set; } = false;

        [BsonElement("parentExecutionId")]
        public ObjectId? ParentExecutionId { get; set; }

        [BsonElement("triggerType")]
        public WorkflowTriggerType TriggerType { get; set; } = WorkflowTriggerType.Manual;

        [BsonElement("scheduleId")]
        public ObjectId? ScheduleId { get; set; }
    }

    public class WorkflowExecutionProgress
    {
        [BsonElement("totalNodes")]
        public int TotalNodes { get; set; }

        [BsonElement("completedNodes")]
        public int CompletedNodes { get; set; }

        [BsonElement("failedNodes")]
        public int FailedNodes { get; set; }

        [BsonElement("skippedNodes")]
        public int SkippedNodes { get; set; }

        [BsonElement("runningNodes")]
        public int RunningNodes { get; set; }

        [BsonElement("percentComplete")]
        public double PercentComplete { get; set; }

        [BsonElement("currentPhase")]
        public string CurrentPhase { get; set; } = string.Empty;

        [BsonElement("estimatedTimeRemaining")]
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }

    public class NodeExecution
    {
        [BsonElement("nodeId")]
        public required string NodeId { get; set; }

        [BsonElement("nodeName")]
        public string NodeName { get; set; } = string.Empty;

        [BsonElement("programId")]
        public ObjectId ProgramId { get; set; }

        [BsonElement("programExecutionId")]
        public string ProgramExecutionId { get; set; } = string.Empty;

        [BsonElement("startedAt")]
        public DateTime? StartedAt { get; set; }

        [BsonElement("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [BsonElement("status")]
        public NodeExecutionStatus Status { get; set; } = NodeExecutionStatus.Pending;

        [BsonElement("inputData")]
        public BsonDocument InputData { get; set; } = new();

        [BsonElement("outputData")]
        public BsonDocument OutputData { get; set; } = new();

        [BsonElement("error")]
        public NodeExecutionError? Error { get; set; }

        [BsonElement("retryCount")]
        public int RetryCount { get; set; } = 0;

        [BsonElement("maxRetries")]
        public int MaxRetries { get; set; } = 3;

        [BsonElement("resourceUsage")]
        public NodeResourceUsage ResourceUsage { get; set; } = new();

        [BsonElement("logs")]
        public List<NodeExecutionLog> Logs { get; set; } = new();

        [BsonElement("metadata")]
        public BsonDocument Metadata { get; set; } = new();

        [BsonElement("duration")]
        public TimeSpan? Duration { get; set; }

        [BsonElement("wasSkipped")]
        public bool WasSkipped { get; set; } = false;

        [BsonElement("skipReason")]
        public string? SkipReason { get; set; }
    }

    public class WorkflowExecutionContext
    {
        [BsonElement("userInputs")]
        public BsonDocument UserInputs { get; set; } = new();

        [BsonElement("globalVariables")]
        public BsonDocument GlobalVariables { get; set; } = new();

        [BsonElement("environment")]
        public Dictionary<string, string> Environment { get; set; } = new();

        [BsonElement("executionMode")]
        public WorkflowExecutionMode ExecutionMode { get; set; } = WorkflowExecutionMode.Normal;

        [BsonElement("debugMode")]
        public bool DebugMode { get; set; } = false;

        [BsonElement("saveIntermediateResults")]
        public bool SaveIntermediateResults { get; set; } = true;

        [BsonElement("maxConcurrentNodes")]
        public int MaxConcurrentNodes { get; set; } = 5;

        [BsonElement("timeoutMinutes")]
        public int TimeoutMinutes { get; set; } = 60;
    }

    public class WorkflowExecutionResults
    {
        [BsonElement("finalOutputs")]
        public BsonDocument FinalOutputs { get; set; } = new();

        [BsonElement("intermediateResults")]
        public Dictionary<string, BsonDocument> IntermediateResults { get; set; } = new();

        [BsonElement("artifactsGenerated")]
        public List<string> ArtifactsGenerated { get; set; } = new();

        [BsonElement("summary")]
        public string Summary { get; set; } = string.Empty;

        [BsonElement("outputFiles")]
        public List<WorkflowOutputFile> OutputFiles { get; set; } = new();

        [BsonElement("executionStatistics")]
        public WorkflowExecutionStatistics ExecutionStatistics { get; set; } = new();
    }

    public class WorkflowExecutionError
    {
        [BsonElement("errorType")]
        public WorkflowErrorType ErrorType { get; set; }

        [BsonElement("message")]
        public string Message { get; set; } = string.Empty;

        [BsonElement("nodeId")]
        public string? NodeId { get; set; }

        [BsonElement("stackTrace")]
        public string? StackTrace { get; set; }

        [BsonElement("innerError")]
        public string? InnerError { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("canRetry")]
        public bool CanRetry { get; set; } = true;

        [BsonElement("retryCount")]
        public int RetryCount { get; set; } = 0;
    }

    public class NodeExecutionError
    {
        [BsonElement("errorType")]
        public NodeErrorType ErrorType { get; set; }

        [BsonElement("message")]
        public string Message { get; set; } = string.Empty;

        [BsonElement("exitCode")]
        public int? ExitCode { get; set; }

        [BsonElement("stackTrace")]
        public string? StackTrace { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("canRetry")]
        public bool CanRetry { get; set; } = true;
    }

    public class NodeResourceUsage
    {
        [BsonElement("cpuTimeSeconds")]
        public double CpuTimeSeconds { get; set; }

        [BsonElement("peakMemoryBytes")]
        public long PeakMemoryBytes { get; set; }

        [BsonElement("diskSpaceUsedBytes")]
        public long DiskSpaceUsedBytes { get; set; }

        [BsonElement("processCount")]
        public int ProcessCount { get; set; }

        [BsonElement("outputSizeBytes")]
        public long OutputSizeBytes { get; set; }
    }

    public class WorkflowResourceUsage
    {
        [BsonElement("totalCpuTimeSeconds")]
        public double TotalCpuTimeSeconds { get; set; }

        [BsonElement("totalMemoryUsedBytes")]
        public long TotalMemoryUsedBytes { get; set; }

        [BsonElement("totalDiskUsedBytes")]
        public long TotalDiskUsedBytes { get; set; }

        [BsonElement("peakConcurrentNodes")]
        public int PeakConcurrentNodes { get; set; }

        [BsonElement("nodeResourceUsage")]
        public Dictionary<string, NodeResourceUsage> NodeResourceUsage { get; set; } = new();
    }

    public class WorkflowExecutionLog
    {
        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("level")]
        public LogLevel Level { get; set; } = LogLevel.Info;

        [BsonElement("message")]
        public string Message { get; set; } = string.Empty;

        [BsonElement("nodeId")]
        public string? NodeId { get; set; }

        [BsonElement("source")]
        public string Source { get; set; } = string.Empty;

        [BsonElement("metadata")]
        public BsonDocument Metadata { get; set; } = new();
    }

    public class NodeExecutionLog
    {
        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("level")]
        public LogLevel Level { get; set; } = LogLevel.Info;

        [BsonElement("message")]
        public string Message { get; set; } = string.Empty;

        [BsonElement("source")]
        public string Source { get; set; } = string.Empty;
    }

    public class WorkflowOutputFile
    {
        [BsonElement("fileName")]
        public string FileName { get; set; } = string.Empty;

        [BsonElement("filePath")]
        public string FilePath { get; set; } = string.Empty;

        [BsonElement("size")]
        public long Size { get; set; }

        [BsonElement("contentType")]
        public string ContentType { get; set; } = string.Empty;

        [BsonElement("nodeId")]
        public string NodeId { get; set; } = string.Empty;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class WorkflowExecutionStatistics
    {
        [BsonElement("totalExecutionTime")]
        public TimeSpan TotalExecutionTime { get; set; }

        [BsonElement("averageNodeExecutionTime")]
        public TimeSpan AverageNodeExecutionTime { get; set; }

        [BsonElement("slowestNode")]
        public string? SlowestNode { get; set; }

        [BsonElement("fastestNode")]
        public string? FastestNode { get; set; }

        [BsonElement("totalRetries")]
        public int TotalRetries { get; set; }

        [BsonElement("parallelizationEfficiency")]
        public double ParallelizationEfficiency { get; set; }
    }

    public enum WorkflowExecutionStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled,
        Paused,
        Timeout
    }

    public enum NodeExecutionStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled,
        Skipped,
        Timeout,
        Retrying
    }

    public enum WorkflowExecutionMode
    {
        Normal,
        Debug,
        DryRun,
        StepByStep
    }

    public enum WorkflowTriggerType
    {
        Manual,
        Scheduled,
        EventBased,
        API,
        Webhook
    }

    public enum WorkflowErrorType
    {
        ValidationError,
        ExecutionError,
        TimeoutError,
        ResourceError,
        DependencyError,
        SystemError
    }

    public enum NodeErrorType
    {
        ExecutionError,
        TimeoutError,
        ResourceError,
        ValidationError,
        DependencyError,
        ConfigurationError
    }

    //public enum LogLevel
    //{
    //    Trace,
    //    Debug,
    //    Info,
    //    Warning,
    //    Error,
    //    Critical
    //}
}