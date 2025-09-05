using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.DTOs.Response.Collaboration
{
    public class WorkflowListDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public WorkflowStatus Status { get; set; }
        public int Version { get; set; }
        public List<string> Tags { get; set; } = new();
        public bool IsTemplate { get; set; }
        public int ExecutionCount { get; set; }
        public TimeSpan? AverageExecutionTime { get; set; }
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
        public ComplexityLevel ComplexityLevel { get; set; }
        public bool IsPublic { get; set; }
        public bool HasPermission { get; set; }
    }

    public class WorkflowDetailDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public WorkflowStatus Status { get; set; }
        public int Version { get; set; }
        public List<WorkflowNodeDto> Nodes { get; set; } = new();
        public List<WorkflowEdgeDto> Edges { get; set; } = new();
        public WorkflowSettingsDto Settings { get; set; } = new();
        public WorkflowPermissionDto Permissions { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool IsTemplate { get; set; }
        public string? TemplateId { get; set; }
        public string? LastExecutionId { get; set; }
        public int ExecutionCount { get; set; }
        public TimeSpan? AverageExecutionTime { get; set; }
        public bool IsPublic { get; set; }
        public WorkflowComplexityMetrics ComplexityMetrics { get; set; } = new();
        public WorkflowValidationResult ValidationResult { get; set; } = new();
    }

    public class WorkflowNodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public string? VersionId { get; set; }
        public WorkflowNodeType NodeType { get; set; }
        public NodePositionDto Position { get; set; } = new();
        public NodeInputConfigurationDto InputConfiguration { get; set; } = new();
        public NodeOutputConfigurationDto OutputConfiguration { get; set; } = new();
        public NodeExecutionSettingsDto ExecutionSettings { get; set; } = new();
        public NodeConditionalExecutionDto? ConditionalExecution { get; set; }
        public NodeUIConfigurationDto UIConfiguration { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool IsDisabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public NodeValidationResult ValidationResult { get; set; } = new();
    }

    public class WorkflowEdgeDto
    {
        public string Id { get; set; } = string.Empty;
        public string SourceNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string SourceOutputName { get; set; } = string.Empty;
        public string TargetInputName { get; set; } = string.Empty;
        public WorkflowEdgeType EdgeType { get; set; }
        public EdgeConditionDto? Condition { get; set; }
        public EdgeTransformationDto? Transformation { get; set; }
        public EdgeUIConfigurationDto UIConfiguration { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool IsDisabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public EdgeValidationResult ValidationResult { get; set; } = new();
    }

    public class WorkflowPermissionDto
    {
        public bool IsPublic { get; set; }
        public List<string> AllowedUsers { get; set; } = new();
        public List<string> AllowedRoles { get; set; } = new();
        public List<WorkflowUserPermissionDto> Permissions { get; set; } = new();
        public List<WorkflowPermissionType> CurrentUserPermissions { get; set; } = new();
    }

    public class WorkflowStatisticsDto
    {
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public int CancelledExecutions { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public TimeSpan FastestExecutionTime { get; set; }
        public TimeSpan SlowestExecutionTime { get; set; }
        public DateTime? LastExecutionDate { get; set; }
        public Dictionary<string, int> ExecutionsByStatus { get; set; } = new();
        public Dictionary<string, int> ExecutionsByMonth { get; set; } = new();
        public Dictionary<string, double> NodeSuccessRates { get; set; } = new();
        public Dictionary<string, TimeSpan> NodeAverageExecutionTimes { get; set; } = new();
        public List<WorkflowExecutionSummaryDto> RecentExecutions { get; set; } = new();
    }

    public class WorkflowExecutionSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string ExecutionName { get; set; } = string.Empty;
        public string ExecutedBy { get; set; } = string.Empty;
        public string ExecutedByUserName { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public WorkflowExecutionStatus Status { get; set; }
        public WorkflowExecutionProgress Progress { get; set; } = new();
        public WorkflowTriggerType TriggerType { get; set; }
        public bool IsRerun { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, NodeExecutionStatus> NodeStatuses { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class WorkflowExecutionPlanDto
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string WorkflowName { get; set; } = string.Empty;
        public List<string> ExecutionOrder { get; set; } = new();
        public Dictionary<string, List<string>> DependencyGraph { get; set; } = new();
        public List<WorkflowExecutionPhaseDto> ExecutionPhases { get; set; } = new();
        public TimeSpan EstimatedExecutionTime { get; set; }
        public int MaxConcurrentNodes { get; set; }
        public List<WorkflowExecutionRiskDto> PotentialRisks { get; set; } = new();
        public List<WorkflowExecutionOptimizationDto> Optimizations { get; set; } = new();
        public WorkflowValidationResult ValidationResult { get; set; } = new();
    }

    public class WorkflowExecutionPhaseDto
    {
        public int PhaseNumber { get; set; }
        public string PhaseName { get; set; } = string.Empty;
        public List<string> NodeIds { get; set; } = new();
        public List<string> NodeNames { get; set; } = new();
        public TimeSpan EstimatedDuration { get; set; }
        public bool CanRunInParallel { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public List<string> Outputs { get; set; } = new();
    }

    public class WorkflowExecutionRiskDto
    {
        public string RiskType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RiskLevel Level { get; set; }
        public string? NodeId { get; set; }
        public string? EdgeId { get; set; }
        public string Mitigation { get; set; } = string.Empty;
        public double Impact { get; set; }
        public double Probability { get; set; }
    }

    public class WorkflowExecutionOptimizationDto
    {
        public string OptimizationType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public double PotentialTimeReduction { get; set; }
        public double PotentialResourceReduction { get; set; }
        public OptimizationComplexity Complexity { get; set; }
        public List<string> AffectedNodes { get; set; } = new();
    }

    public class WorkflowDashboardDto
    {
        public WorkflowStatisticsDto Statistics { get; set; } = new();
        public List<WorkflowExecutionSummaryDto> RecentExecutions { get; set; } = new();
        public List<WorkflowListDto> RecentlyModifiedWorkflows { get; set; } = new();
        public List<WorkflowListDto> MostExecutedWorkflows { get; set; } = new();
        public List<WorkflowListDto> FailingWorkflows { get; set; } = new();
        public Dictionary<string, int> ExecutionTrends { get; set; } = new();
        public Dictionary<string, double> PerformanceMetrics { get; set; } = new();
        public List<WorkflowAlertDto> Alerts { get; set; } = new();
    }

    public class WorkflowAlertDto
    {
        public string Id { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? WorkflowId { get; set; }
        public string? WorkflowName { get; set; }
        public string? ExecutionId { get; set; }
        public bool IsRead { get; set; }
        public string? ActionUrl { get; set; }
    }

    public class WorkflowExportDto
    {
        public string Format { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public DateTime ExportedAt { get; set; }
        public string ExportedBy { get; set; } = string.Empty;
        public WorkflowExportOptions Options { get; set; } = new();
    }

    public class WorkflowExportOptions
    {
        public bool IncludeExecutionHistory { get; set; } = false;
        public bool IncludePermissions { get; set; } = false;
        public bool IncludeMetadata { get; set; } = true;
        public bool IncludeValidationResults { get; set; } = false;
        public bool CompressOutput { get; set; } = false;
        public List<string> ExcludedFields { get; set; } = new();
    }

    public enum RiskLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum OptimizationComplexity
    {
        Simple,
        Moderate,
        Complex
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public class WorkflowDataContractDto
    {
        public string ContractId { get; set; } = string.Empty;
        public string SourceNodeId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public WorkflowDataType DataType { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public DataContractMetadataDto Metadata { get; set; } = new();
        public Dictionary<string, object>? Schema { get; set; }
        public string Version { get; set; } = "1.0";
        public DateTime Timestamp { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? Checksum { get; set; }
        public CompressionType Compression { get; set; }
        public EncryptionInfoDto? Encryption { get; set; }
        public List<DataAttachmentDto> Attachments { get; set; } = new();
    }

    public class DataContractMetadataDto
    {
        public string ContentType { get; set; } = "application/json";
        public string Encoding { get; set; } = "UTF-8";
        public long Size { get; set; }
        public string? OriginalFormat { get; set; }
        public List<DataTransformationDto> Transformations { get; set; } = new();
        public List<DataValidationResultDto> ValidationResults { get; set; } = new();
        public DataQualityMetricsDto Quality { get; set; } = new();
        public DataLineageDto Lineage { get; set; } = new();
        public Dictionary<string, object> CustomMetadata { get; set; } = new();
    }

    public class DataTransformationDto
    {
        public string TransformationId { get; set; } = string.Empty;
        public TransformationType Type { get; set; }
        public string Expression { get; set; } = string.Empty;
        public DateTime AppliedAt { get; set; }
        public string AppliedBy { get; set; } = string.Empty;
        public Dictionary<string, object>? InputSchema { get; set; }
        public Dictionary<string, object>? OutputSchema { get; set; }
        public bool Success { get; set; } = true;
        public string? Error { get; set; }
    }

    public class DataValidationResultDto
    {
        public string ValidationId { get; set; } = string.Empty;
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public DateTime ValidatedAt { get; set; }
        public ValidationType ValidationType { get; set; }
        public Dictionary<string, object>? SchemaUsed { get; set; }
    }

    public class DataQualityMetricsDto
    {
        public double Completeness { get; set; } = 1.0;
        public double Accuracy { get; set; } = 1.0;
        public double Consistency { get; set; } = 1.0;
        public double Validity { get; set; } = 1.0;
        public double Timeliness { get; set; } = 1.0;
        public double Uniqueness { get; set; } = 1.0;
        public double OverallScore { get; set; } = 1.0;
        public List<DataQualityIssueDto> Issues { get; set; } = new();
    }

    public class DataQualityIssueDto
    {
        public DataQualityIssueType Type { get; set; }
        public IssueSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Field { get; set; }
        public string? Recommendation { get; set; }
    }

    public class DataLineageDto
    {
        public List<string> SourceNodes { get; set; } = new();
        public List<string> TransformationPath { get; set; } = new();
        public List<DataDependencyDto> Dependencies { get; set; } = new();
        public List<DataSourceDto> OriginalSources { get; set; } = new();
    }

    public class DataDependencyDto
    {
        public string NodeId { get; set; } = string.Empty;
        public string OutputName { get; set; } = string.Empty;
        public DependencyType DependencyType { get; set; }
        public bool IsOptional { get; set; } = false;
    }

    public class DataSourceDto
    {
        public string SourceId { get; set; } = string.Empty;
        public DataSourceType SourceType { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime AccessedAt { get; set; }
        public string? Version { get; set; }
    }

    public class EncryptionInfoDto
    {
        public string Algorithm { get; set; } = string.Empty;
        public string KeyId { get; set; } = string.Empty;
        public bool IsEncrypted { get; set; } = false;
        public List<string> EncryptedFields { get; set; } = new();
    }

    public class DataAttachmentDto
    {
        public string AttachmentId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}