using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    /// <summary>
    /// Extended execution result for workflow nodes with structured output data
    /// </summary>
    public class NodeExecutionResult
    {
        [BsonElement("nodeId")]
        public string NodeId { get; set; } = string.Empty;

        [BsonElement("executionId")]
        public string ExecutionId { get; set; } = string.Empty;

        [BsonElement("programExecutionId")]
        public string? ProgramExecutionId { get; set; }

        [BsonElement("success")]
        public bool Success { get; set; }

        [BsonElement("status")]
        public NodeExecutionResultStatus Status { get; set; } = NodeExecutionResultStatus.Pending;

        [BsonElement("startedAt")]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("completedAt")]
        public DateTime? CompletedAt { get; set; }

        [BsonElement("duration")]
        public TimeSpan? Duration { get; set; }

        [BsonElement("inputData")]
        public WorkflowDataContract? InputData { get; set; }

        [BsonElement("outputData")]
        public WorkflowDataContract? OutputData { get; set; }

        [BsonElement("rawOutput")]
        public NodeRawOutput RawOutput { get; set; } = new();

        [BsonElement("error")]
        public NodeExecutionResultError? Error { get; set; }

        [BsonElement("warnings")]
        public List<NodeExecutionWarning> Warnings { get; set; } = new();

        [BsonElement("metrics")]
        public NodeExecutionMetrics Metrics { get; set; } = new();

        [BsonElement("logs")]
        public List<NodeExecutionLogEntry> Logs { get; set; } = new();

        [BsonElement("artifacts")]
        public List<NodeExecutionArtifact> Artifacts { get; set; } = new();

        [BsonElement("retryInfo")]
        public NodeRetryInfo? RetryInfo { get; set; }

        [BsonElement("debugInfo")]
        public NodeDebugInfo? DebugInfo { get; set; }

        [BsonElement("metadata")]
        public BsonDocument Metadata { get; set; } = new();

        [BsonElement("parentExecutionId")]
        public string? ParentExecutionId { get; set; }

        [BsonElement("childExecutions")]
        public List<string> ChildExecutions { get; set; } = new();
    }

    public class NodeRawOutput
    {
        [BsonElement("stdout")]
        public string Stdout { get; set; } = string.Empty;

        [BsonElement("stderr")]
        public string Stderr { get; set; } = string.Empty;

        [BsonElement("exitCode")]
        public int ExitCode { get; set; }

        [BsonElement("outputFiles")]
        public List<NodeOutputFile> OutputFiles { get; set; } = new();

        [BsonElement("generatedFiles")]
        public List<NodeOutputFile> GeneratedFiles { get; set; } = new();

        [BsonElement("environmentVariables")]
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

        [BsonElement("workingDirectory")]
        public string WorkingDirectory { get; set; } = string.Empty;

        [BsonElement("processId")]
        public int? ProcessId { get; set; }
    }

    public class NodeOutputFile
    {
        [BsonElement("fileName")]
        public string FileName { get; set; } = string.Empty;

        [BsonElement("relativePath")]
        public string RelativePath { get; set; } = string.Empty;

        [BsonElement("absolutePath")]
        public string AbsolutePath { get; set; } = string.Empty;

        [BsonElement("size")]
        public long Size { get; set; }

        [BsonElement("contentType")]
        public string ContentType { get; set; } = string.Empty;

        [BsonElement("checksum")]
        public string Checksum { get; set; } = string.Empty;

        [BsonElement("isTemporary")]
        public bool IsTemporary { get; set; } = false;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("lastModified")]
        public DateTime? LastModified { get; set; }
    }

    public class NodeExecutionResultError
    {
        [BsonElement("errorCode")]
        public string ErrorCode { get; set; } = string.Empty;

        [BsonElement("errorType")]
        public NodeExecutionErrorType ErrorType { get; set; }

        [BsonElement("message")]
        public string Message { get; set; } = string.Empty;

        [BsonElement("details")]
        public string Details { get; set; } = string.Empty;

        [BsonElement("stackTrace")]
        public string? StackTrace { get; set; }

        [BsonElement("innerException")]
        public string? InnerException { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("source")]
        public NodeExecutionErrorSource Source { get; set; }

        [BsonElement("canRetry")]
        public bool CanRetry { get; set; } = true;

        [BsonElement("suggestedAction")]
        public string? SuggestedAction { get; set; }

        [BsonElement("helpUrl")]
        public string? HelpUrl { get; set; }

        [BsonElement("context")]
        public BsonDocument Context { get; set; } = new();
    }

    public class NodeExecutionWarning
    {
        [BsonElement("warningCode")]
        public string WarningCode { get; set; } = string.Empty;

        [BsonElement("warningType")]
        public NodeExecutionWarningType WarningType { get; set; }

        [BsonElement("message")]
        public string Message { get; set; } = string.Empty;

        [BsonElement("details")]
        public string Details { get; set; } = string.Empty;

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("source")]
        public string Source { get; set; } = string.Empty;

        [BsonElement("severity")]
        public WarningSeverity Severity { get; set; } = WarningSeverity.Medium;

        [BsonElement("canIgnore")]
        public bool CanIgnore { get; set; } = true;

        [BsonElement("recommendation")]
        public string? Recommendation { get; set; }
    }

    public class NodeExecutionMetrics
    {
        [BsonElement("resourceUsage")]
        public NodeResourceMetrics ResourceUsage { get; set; } = new();

        [BsonElement("performance")]
        public NodePerformanceMetrics Performance { get; set; } = new();

        [BsonElement("dataMetrics")]
        public NodeDataMetrics DataMetrics { get; set; } = new();

        [BsonElement("qualityMetrics")]
        public NodeQualityMetrics QualityMetrics { get; set; } = new();

        [BsonElement("customMetrics")]
        public BsonDocument CustomMetrics { get; set; } = new();
    }

    public class NodeResourceMetrics
    {
        [BsonElement("cpuUsage")]
        public NodeCpuUsage CpuUsage { get; set; } = new();

        [BsonElement("memoryUsage")]
        public NodeMemoryUsage MemoryUsage { get; set; } = new();

        [BsonElement("diskUsage")]
        public NodeDiskUsage DiskUsage { get; set; } = new();

        [BsonElement("networkUsage")]
        public NodeNetworkUsage NetworkUsage { get; set; } = new();
    }

    public class NodeCpuUsage
    {
        [BsonElement("totalTimeSeconds")]
        public double TotalTimeSeconds { get; set; }

        [BsonElement("userTimeSeconds")]
        public double UserTimeSeconds { get; set; }

        [BsonElement("systemTimeSeconds")]
        public double SystemTimeSeconds { get; set; }

        [BsonElement("peakUsagePercentage")]
        public double PeakUsagePercentage { get; set; }

        [BsonElement("averageUsagePercentage")]
        public double AverageUsagePercentage { get; set; }
    }

    public class NodeMemoryUsage
    {
        [BsonElement("peakUsageBytes")]
        public long PeakUsageBytes { get; set; }

        [BsonElement("averageUsageBytes")]
        public long AverageUsageBytes { get; set; }

        [BsonElement("allocatedBytes")]
        public long AllocatedBytes { get; set; }

        [BsonElement("garbageCollections")]
        public int GarbageCollections { get; set; }
    }

    public class NodeDiskUsage
    {
        [BsonElement("totalReadBytes")]
        public long TotalReadBytes { get; set; }

        [BsonElement("totalWriteBytes")]
        public long TotalWriteBytes { get; set; }

        [BsonElement("temporaryFilesBytes")]
        public long TemporaryFilesBytes { get; set; }

        [BsonElement("outputFilesBytes")]
        public long OutputFilesBytes { get; set; }
    }

    public class NodeNetworkUsage
    {
        [BsonElement("bytesReceived")]
        public long BytesReceived { get; set; }

        [BsonElement("bytesSent")]
        public long BytesSent { get; set; }

        [BsonElement("requestsMade")]
        public int RequestsMade { get; set; }

        [BsonElement("connectionsOpened")]
        public int ConnectionsOpened { get; set; }
    }

    public class NodePerformanceMetrics
    {
        [BsonElement("initializationTime")]
        public TimeSpan InitializationTime { get; set; }

        [BsonElement("processingTime")]
        public TimeSpan ProcessingTime { get; set; }

        [BsonElement("cleanupTime")]
        public TimeSpan CleanupTime { get; set; }

        [BsonElement("throughput")]
        public double Throughput { get; set; }

        [BsonElement("latency")]
        public TimeSpan Latency { get; set; }

        [BsonElement("efficiency")]
        public double Efficiency { get; set; }
    }

    public class NodeDataMetrics
    {
        [BsonElement("inputDataSize")]
        public long InputDataSize { get; set; }

        [BsonElement("outputDataSize")]
        public long OutputDataSize { get; set; }

        [BsonElement("recordsProcessed")]
        public long RecordsProcessed { get; set; }

        [BsonElement("recordsGenerated")]
        public long RecordsGenerated { get; set; }

        [BsonElement("compressionRatio")]
        public double CompressionRatio { get; set; }

        [BsonElement("dataQualityScore")]
        public double DataQualityScore { get; set; }
    }

    public class NodeQualityMetrics
    {
        [BsonElement("successRate")]
        public double SuccessRate { get; set; } = 1.0;

        [BsonElement("errorRate")]
        public double ErrorRate { get; set; } = 0.0;

        [BsonElement("warningCount")]
        public int WarningCount { get; set; } = 0;

        [BsonElement("codecoverage")]
        public double CodeCoverage { get; set; } = 0.0;

        [BsonElement("testsPassed")]
        public int TestsPassed { get; set; } = 0;

        [BsonElement("testsFailed")]
        public int TestsFailed { get; set; } = 0;
    }

    public class NodeExecutionLogEntry
    {
        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("level")]
        public LogLevel Level { get; set; } = LogLevel.Info;

        [BsonElement("message")]
        public string Message { get; set; } = string.Empty;

        [BsonElement("source")]
        public string Source { get; set; } = string.Empty;

        [BsonElement("category")]
        public string Category { get; set; } = string.Empty;

        [BsonElement("threadId")]
        public string? ThreadId { get; set; }

        [BsonElement("processId")]
        public int? ProcessId { get; set; }

        [BsonElement("context")]
        public BsonDocument Context { get; set; } = new();
    }

    public class NodeExecutionArtifact
    {
        [BsonElement("artifactId")]
        public string ArtifactId { get; set; } = Guid.NewGuid().ToString();

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("type")]
        public ArtifactType Type { get; set; }

        [BsonElement("path")]
        public string Path { get; set; } = string.Empty;

        [BsonElement("size")]
        public long Size { get; set; }

        [BsonElement("contentType")]
        public string ContentType { get; set; } = string.Empty;

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("metadata")]
        public BsonDocument Metadata { get; set; } = new();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        [BsonElement("isPublic")]
        public bool IsPublic { get; set; } = false;

        [BsonElement("checksum")]
        public string Checksum { get; set; } = string.Empty;
    }

    public class NodeRetryInfo
    {
        [BsonElement("attemptNumber")]
        public int AttemptNumber { get; set; } = 1;

        [BsonElement("maxAttempts")]
        public int MaxAttempts { get; set; } = 3;

        [BsonElement("nextRetryAt")]
        public DateTime? NextRetryAt { get; set; }

        [BsonElement("retryReason")]
        public string RetryReason { get; set; } = string.Empty;

        [BsonElement("retryStrategy")]
        public RetryStrategy RetryStrategy { get; set; } = RetryStrategy.Exponential;

        [BsonElement("delaySeconds")]
        public int DelaySeconds { get; set; } = 30;

        [BsonElement("previousAttempts")]
        public List<RetryAttempt> PreviousAttempts { get; set; } = new();
    }

    public class RetryAttempt
    {
        [BsonElement("attemptNumber")]
        public int AttemptNumber { get; set; }

        [BsonElement("attemptedAt")]
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("error")]
        public string Error { get; set; } = string.Empty;

        [BsonElement("duration")]
        public TimeSpan Duration { get; set; }
    }

    public class NodeDebugInfo
    {
        [BsonElement("breakpoints")]
        public List<string> Breakpoints { get; set; } = new();

        [BsonElement("variables")]
        public BsonDocument Variables { get; set; } = new();

        [BsonElement("stackFrames")]
        public List<StackFrame> StackFrames { get; set; } = new();

        [BsonElement("executionSteps")]
        public List<ExecutionStep> ExecutionSteps { get; set; } = new();

        [BsonElement("profiling")]
        public ProfilingInfo? Profiling { get; set; }
    }

    public class StackFrame
    {
        [BsonElement("method")]
        public string Method { get; set; } = string.Empty;

        [BsonElement("file")]
        public string File { get; set; } = string.Empty;

        [BsonElement("line")]
        public int Line { get; set; }

        [BsonElement("column")]
        public int Column { get; set; }

        [BsonElement("locals")]
        public BsonDocument Locals { get; set; } = new();
    }

    public class ExecutionStep
    {
        [BsonElement("stepNumber")]
        public int StepNumber { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("state")]
        public BsonDocument State { get; set; } = new();

        [BsonElement("duration")]
        public TimeSpan Duration { get; set; }
    }

    public class ProfilingInfo
    {
        [BsonElement("hotspots")]
        public List<PerformanceHotspot> Hotspots { get; set; } = new();

        [BsonElement("memoryProfile")]
        public MemoryProfile MemoryProfile { get; set; } = new();

        [BsonElement("callGraph")]
        public BsonDocument CallGraph { get; set; } = new();
    }

    public class PerformanceHotspot
    {
        [BsonElement("function")]
        public string Function { get; set; } = string.Empty;

        [BsonElement("file")]
        public string File { get; set; } = string.Empty;

        [BsonElement("line")]
        public int Line { get; set; }

        [BsonElement("timeSpent")]
        public TimeSpan TimeSpent { get; set; }

        [BsonElement("callCount")]
        public int CallCount { get; set; }

        [BsonElement("percentage")]
        public double Percentage { get; set; }
    }

    public class MemoryProfile
    {
        [BsonElement("allocations")]
        public List<MemoryAllocation> Allocations { get; set; } = new();

        [BsonElement("leaks")]
        public List<MemoryLeak> Leaks { get; set; } = new();

        [BsonElement("gcStatistics")]
        public GarbageCollectionStatistics GcStatistics { get; set; } = new();
    }

    public class MemoryAllocation
    {
        [BsonElement("type")]
        public string Type { get; set; } = string.Empty;

        [BsonElement("size")]
        public long Size { get; set; }

        [BsonElement("count")]
        public int Count { get; set; }

        [BsonElement("location")]
        public string Location { get; set; } = string.Empty;
    }

    public class MemoryLeak
    {
        [BsonElement("type")]
        public string Type { get; set; } = string.Empty;

        [BsonElement("size")]
        public long Size { get; set; }

        [BsonElement("location")]
        public string Location { get; set; } = string.Empty;

        [BsonElement("severity")]
        public LeakSeverity Severity { get; set; }
    }

    public class GarbageCollectionStatistics
    {
        [BsonElement("collections")]
        public int Collections { get; set; }

        [BsonElement("totalTime")]
        public TimeSpan TotalTime { get; set; }

        [BsonElement("averageTime")]
        public TimeSpan AverageTime { get; set; }

        [BsonElement("memoryReclaimed")]
        public long MemoryReclaimed { get; set; }
    }

    public enum NodeExecutionResultStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled,
        Skipped,
        Timeout,
        Retrying,
        Paused
    }

    public enum NodeExecutionErrorType
    {
        Timeout,
        ResourceError,
        ValidationError,
        RuntimeError,
        ConfigurationError,
        DependencyError,
        AuthenticationError,
        AuthorizationError,
        NetworkError,
        FileSystemError,
        SystemError,
        Unknown
    }

    public enum NodeExecutionErrorSource
    {
        Program,
        Runtime,
        System,
        Network,
        Database,
        FileSystem,
        Configuration,
        User,
        External
    }

    public enum NodeExecutionWarningType
    {
        Performance,
        Resource,
        DataQuality,
        Deprecation,
        Security,
        Configuration,
        BestPractice,
        Compatibility
    }

    public enum WarningSeverity
    {
        Low,
        Medium,
        High
    }

    public enum ArtifactType
    {
        Log,
        Report,
        Data,
        Image,
        Document,
        Archive,
        Binary,
        Source,
        Configuration,
        Metadata
    }

    public enum RetryStrategy
    {
        Linear,
        Exponential,
        Custom
    }

    public enum LeakSeverity
    {
        Minor,
        Major,
        Critical
    }

    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }
}