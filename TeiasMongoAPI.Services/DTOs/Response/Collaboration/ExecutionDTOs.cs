using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;

namespace TeiasMongoAPI.Services.DTOs.Response.Collaboration
{
    public class ExecutionDto
    {
        public string Id { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ExecutionType { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public object Parameters { get; set; } = new object();
        public ExecutionResultDto Results { get; set; } = new();
        public ExecutionResourceUsageDto ResourceUsage { get; set; } = new();
    }

    public class ExecutionListDto
    {
        public string Id { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public int? VersionNumber { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string ExecutionType { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? ExitCode { get; set; }
        public bool HasError { get; set; }
        public double? Duration { get; set; }
        public ExecutionResourceUsageDto ResourceUsage { get; set; } = new();
    }

    public class ExecutionDetailDto : ExecutionDto
    {
        public string ProgramName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int? VersionNumber { get; set; }
        public List<string> RecentLogs { get; set; } = new();
        public ExecutionEnvironmentDto Environment { get; set; } = new();
        public string? WebAppUrl { get; set; }
        public WebAppStatusDto? WebAppStatus { get; set; }
    }

    public class ExecutionResultDto
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public List<string> OutputFiles { get; set; } = new();
        public string? Error { get; set; }
        public string? WebAppUrl { get; set; }
        public DateTime? CompletedAt { get; set; }
    }


    public class ExecutionStatusDto
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double? Progress { get; set; }
        public string? CurrentStep { get; set; }
        public ExecutionResourceUsageDto ResourceUsage { get; set; } = new();
        public string? StatusMessage { get; set; }
    }

    public class ExecutionResourceUsageDto
    {
        public double CpuTime { get; set; }
        public long MemoryUsed { get; set; }
        public long DiskUsed { get; set; }
        public double CpuPercentage { get; set; }
        public double MemoryPercentage { get; set; }
        public double DiskPercentage { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class ExecutionResourceTrendDto
    {
        public DateTime Timestamp { get; set; }
        public double CpuUsage { get; set; }
        public long MemoryUsage { get; set; }
        public long DiskUsage { get; set; }
        public int ActiveExecutions { get; set; }
    }

    public class WebAppStatusDto
    {
        public string Status { get; set; } = string.Empty; // "active", "inactive", "building", "failed"
        public string Url { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public int ResponseTime { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
    }

    public class ExecutionStatsDto
    {
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public int RunningExecutions { get; set; }
        public double AverageExecutionTime { get; set; }
        public double SuccessRate { get; set; }
        public long TotalCpuTime { get; set; }
        public long TotalMemoryUsed { get; set; }
        public Dictionary<string, int> ExecutionsByStatus { get; set; } = new();
        public Dictionary<string, int> ExecutionsByType { get; set; } = new();
    }

    public class ExecutionTrendDto
    {
        public DateTime Date { get; set; }
        public int ExecutionCount { get; set; }
        public int SuccessfulCount { get; set; }
        public int FailedCount { get; set; }
        public double AverageExecutionTime { get; set; }
        public long TotalResourceUsage { get; set; }
    }

    public class ExecutionPerformanceDto
    {
        public string ProgramId { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public int ExecutionCount { get; set; }
        public double SuccessRate { get; set; }
        public double AverageExecutionTime { get; set; }
        public double AverageResourceUsage { get; set; }
        public DateTime LastExecution { get; set; }
    }

    public class ExecutionSummaryDto
    {
        public string UserId { get; set; } = string.Empty;
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public double TotalCpuTime { get; set; }
        public long TotalMemoryUsed { get; set; }
        public DateTime? LastExecution { get; set; }
        public List<ExecutionPerformanceDto> ProgramPerformance { get; set; } = new();
    }

    public class ExecutionEnvironmentDto
    {
        public string ProgramId { get; set; } = string.Empty;
        public Dictionary<string, string> Environment { get; set; } = new();
        public ExecutionResourceLimitsDto ResourceLimits { get; set; } = new();
        public Dictionary<string, object> Configuration { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    public class ExecutionTemplateDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public object ParameterSchema { get; set; } = new object();
        public Dictionary<string, string> DefaultEnvironment { get; set; } = new();
        public ExecutionResourceLimitsDto DefaultResourceLimits { get; set; } = new();
    }

    public class ExecutionValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public ExecutionResourceLimitsDto RecommendedLimits { get; set; } = new();
    }

    public class ExecutionQueueStatusDto
    {
        public int QueueLength { get; set; }
        public int RunningExecutions { get; set; }
        public int MaxConcurrentExecutions { get; set; }
        public double AverageWaitTime { get; set; }
        public List<ExecutionListDto> QueuedExecutions { get; set; } = new();
    }

    public class ExecutionCleanupReportDto
    {
        public DateTime CleanupDate { get; set; }
        public int ExecutionsRemoved { get; set; }
        public long SpaceFreed { get; set; }
        public int DaysRetained { get; set; }
        public Dictionary<string, int> RemovedByStatus { get; set; } = new();
    }

    public class ExecutionSecurityScanResult
    {
        public bool IsSecure { get; set; }
        public List<string> SecurityIssues { get; set; } = new();
        public List<string> SecurityWarnings { get; set; } = new();
        public int RiskLevel { get; set; } // 1-5 scale
        public DateTime ScanDate { get; set; }
    }

    public class WorkflowExecutionResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string WorkflowId { get; set; } = string.Empty;
        public string WorkflowName { get; set; } = string.Empty;
        public int WorkflowVersion { get; set; }
        public string ExecutionName { get; set; } = string.Empty;
        public string ExecutedBy { get; set; } = string.Empty;
        public string ExecutedByUsername { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public WorkflowExecutionStatus Status { get; set; }
        public WorkflowExecutionProgress Progress { get; set; } = new();
        public WorkflowTriggerType TriggerType { get; set; }
        public bool IsRerun { get; set; }
        public string? ParentExecutionId { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, NodeExecutionStatus> NodeStatuses { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public WorkflowExecutionContextDto ExecutionContext { get; set; } = new();
    }

    public class NodeExecutionResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string ExecutionId { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public string? VersionId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public NodeExecutionStatus Status { get; set; }
        public int? ExitCode { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> InputData { get; set; } = new();
        public Dictionary<string, object> OutputData { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public List<string> RetryHistory { get; set; } = new();
        public bool IsSkipped { get; set; }
        public string? SkipReason { get; set; }
    }

    public class WorkflowExecutionStatisticsResponseDto
    {
        public int TotalNodes { get; set; }
        public int CompletedNodes { get; set; }
        public int FailedNodes { get; set; }
        public int SkippedNodes { get; set; }
        public int PendingNodes { get; set; }
        public int RunningNodes { get; set; }
        public double CompletionPercentage { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public TimeSpan AverageNodeExecutionTime { get; set; }
        public Dictionary<string, int> StatusDistribution { get; set; } = new();
        public Dictionary<string, TimeSpan> NodeExecutionTimes { get; set; } = new();
    }

    public class WorkflowExecutionLogResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string ExecutionId { get; set; } = string.Empty;
        public string? NodeId { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public string? Exception { get; set; }
    }

    public class WorkflowExecutionContextDto
    {
        public Dictionary<string, object> UserInputs { get; set; } = new();
        public Dictionary<string, object> GlobalVariables { get; set; } = new();
        public Dictionary<string, string> Environment { get; set; } = new();
        public WorkflowExecutionMode ExecutionMode { get; set; } = WorkflowExecutionMode.Normal;
        public bool DebugMode { get; set; } = false;
        public bool SaveIntermediateResults { get; set; } = true;
        public int MaxConcurrentNodes { get; set; } = 5;
        public int TimeoutMinutes { get; set; } = 2880;
    }
}