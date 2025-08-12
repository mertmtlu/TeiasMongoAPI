using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Services.DTOs.Request.Collaboration
{
    public class WorkflowCreateDto
    {
        [Required]
        [StringLength(200)]
        public required string Name { get; set; }

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;

        public List<WorkflowNodeCreateDto> Nodes { get; set; } = new();

        public List<WorkflowEdgeCreateDto> Edges { get; set; } = new();

        public WorkflowSettingsDto Settings { get; set; } = new();

        public WorkflowPermissionsDto Permissions { get; set; } = new();

        public List<string> Tags { get; set; } = new();

        public Dictionary<string, object> Metadata { get; set; } = new();

        public bool IsTemplate { get; set; } = false;

        public string? TemplateId { get; set; }
    }

    public class WorkflowUpdateDto
    {
        [StringLength(200)]
        public string? Name { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        public WorkflowStatus? Status { get; set; }

        public List<WorkflowNodeBulkUpdateDto>? Nodes { get; set; }

        public List<WorkflowEdgeBulkUpdateDto>? Edges { get; set; }

        public WorkflowSettingsDto? Settings { get; set; }

        public WorkflowPermissionsDto? Permissions { get; set; }

        public List<string>? Tags { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }

        public bool? IsTemplate { get; set; }
    }

    public class WorkflowNameDescriptionUpdateDto
    {
        [StringLength(200)]
        public string? Name { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }
    }

    public class WorkflowCloneDto
    {
        [Required]
        [StringLength(200)]
        public required string Name { get; set; }

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        public bool ClonePermissions { get; set; } = false;

        public bool CloneExecutionHistory { get; set; } = false;

        public List<string> Tags { get; set; } = new();
    }

    public class WorkflowNodeCreateDto
    {
        [Required]
        public required string Id { get; set; }

        [Required]
        [StringLength(200)]
        public required string Name { get; set; }

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public required string ProgramId { get; set; }

        public string? VersionId { get; set; }

        public WorkflowNodeType NodeType { get; set; } = WorkflowNodeType.Program;

        public NodePositionDto Position { get; set; } = new();

        public NodeInputConfigurationDto InputConfiguration { get; set; } = new();

        public NodeOutputConfigurationDto OutputConfiguration { get; set; } = new();

        public NodeExecutionSettingsDto ExecutionSettings { get; set; } = new();

        public NodeConditionalExecutionDto? ConditionalExecution { get; set; }

        public NodeUIConfigurationDto UIConfiguration { get; set; } = new();

        public Dictionary<string, object> Metadata { get; set; } = new();

        public bool IsDisabled { get; set; } = false;
    }

    public class WorkflowNodeUpdateDto
    {
        [StringLength(200)]
        public string? Name { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        public string? ProgramId { get; set; }

        public string? VersionId { get; set; }

        public WorkflowNodeType? NodeType { get; set; }

        public NodePositionDto? Position { get; set; }

        public NodeInputConfigurationDto? InputConfiguration { get; set; }

        public NodeOutputConfigurationDto? OutputConfiguration { get; set; }

        public NodeExecutionSettingsDto? ExecutionSettings { get; set; }

        public NodeConditionalExecutionDto? ConditionalExecution { get; set; }

        public NodeUIConfigurationDto? UIConfiguration { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }

        public bool? IsDisabled { get; set; }
    }

    public class WorkflowNodeBulkUpdateDto
    {
        public string? Id { get; set; }

        [StringLength(200)]
        public string? Name { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        public string? ProgramId { get; set; }

        public string? VersionId { get; set; }

        public WorkflowNodeType? NodeType { get; set; }

        public NodePositionDto? Position { get; set; }

        public NodeInputConfigurationDto? InputConfiguration { get; set; }

        public NodeOutputConfigurationDto? OutputConfiguration { get; set; }

        public NodeExecutionSettingsDto? ExecutionSettings { get; set; }

        public NodeConditionalExecutionDto? ConditionalExecution { get; set; }

        public NodeUIConfigurationDto? UIConfiguration { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }

        public bool? IsDisabled { get; set; }
    }

    public class WorkflowEdgeCreateDto
    {
        [Required]
        public required string Id { get; set; }

        [Required]
        public required string SourceNodeId { get; set; }

        [Required]
        public required string TargetNodeId { get; set; }

        public string SourceOutputName { get; set; } = "default";

        public string TargetInputName { get; set; } = "default";

        public WorkflowEdgeType EdgeType { get; set; } = WorkflowEdgeType.Data;

        public EdgeConditionDto? Condition { get; set; }

        public EdgeTransformationDto? Transformation { get; set; }

        public EdgeUIConfigurationDto UIConfiguration { get; set; } = new();

        public Dictionary<string, object> Metadata { get; set; } = new();

        public bool IsDisabled { get; set; } = false;
    }

    public class WorkflowEdgeUpdateDto
    {
        public string? SourceNodeId { get; set; }

        public string? TargetNodeId { get; set; }

        public string? SourceOutputName { get; set; }

        public string? TargetInputName { get; set; }

        public WorkflowEdgeType? EdgeType { get; set; }

        public EdgeConditionDto? Condition { get; set; }

        public EdgeTransformationDto? Transformation { get; set; }

        public EdgeUIConfigurationDto? UIConfiguration { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }

        public bool? IsDisabled { get; set; }
    }

    public class WorkflowEdgeBulkUpdateDto
    {
        public string? Id { get; set; }

        public string? SourceNodeId { get; set; }

        public string? TargetNodeId { get; set; }

        public string? SourceOutputName { get; set; }

        public string? TargetInputName { get; set; }

        public WorkflowEdgeType? EdgeType { get; set; }

        public EdgeConditionDto? Condition { get; set; }

        public EdgeTransformationDto? Transformation { get; set; }

        public EdgeUIConfigurationDto? UIConfiguration { get; set; }

        public Dictionary<string, object>? Metadata { get; set; }

        public bool? IsDisabled { get; set; }
    }

    public class WorkflowSettingsDto
    {
        [Range(1, 100)]
        public int MaxConcurrentNodes { get; set; } = 5;

        [Range(1, 1440)]
        public int TimeoutMinutes { get; set; } = 2880;

        public WorkflowRetryPolicyDto RetryPolicy { get; set; } = new();

        public bool EnableDebugging { get; set; } = false;

        public bool SaveIntermediateResults { get; set; } = true;

        public WorkflowNotificationSettingsDto NotificationSettings { get; set; } = new();
    }

    public class WorkflowRetryPolicyDto
    {
        [Range(0, 10)]
        public int MaxRetries { get; set; } = 3;

        [Range(1, 3600)]
        public int RetryDelaySeconds { get; set; } = 30;

        public bool ExponentialBackoff { get; set; } = true;

        public List<string> RetryOnFailureTypes { get; set; } = new() { "Timeout", "ResourceError" };
    }

    public class WorkflowNotificationSettingsDto
    {
        public bool NotifyOnStart { get; set; } = false;

        public bool NotifyOnCompletion { get; set; } = true;

        public bool NotifyOnFailure { get; set; } = true;

        public List<string> Recipients { get; set; } = new();
    }

    public class WorkflowPermissionsDto
    {
        public bool IsPublic { get; set; } = false;

        public List<string> AllowedUsers { get; set; } = new();

        public List<string> AllowedRoles { get; set; } = new();

        public List<WorkflowUserPermissionDto> Permissions { get; set; } = new();
    }

    public class WorkflowUserPermissionDto
    {
        [Required]
        public required string UserId { get; set; }

        public List<WorkflowPermissionType> Permissions { get; set; } = new();
    }

    public class WorkflowPermissionUpdateDto
    {
        public bool? IsPublic { get; set; }

        public List<string>? AllowedUsers { get; set; }

        public List<string>? AllowedRoles { get; set; }

        public List<WorkflowUserPermissionDto>? Permissions { get; set; }
    }

    public class NodePositionDto
    {
        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; } = 200;

        public double Height { get; set; } = 100;
    }

    public class NodeInputConfigurationDto
    {
        public List<NodeInputMappingDto> InputMappings { get; set; } = new();

        public Dictionary<string, object> StaticInputs { get; set; } = new();

        public List<NodeUserInputDto> UserInputs { get; set; } = new();

        public List<NodeValidationRuleDto> ValidationRules { get; set; } = new();
    }

    public class NodeInputMappingDto
    {
        [Required]
        public required string InputName { get; set; }

        [Required]
        public required string SourceNodeId { get; set; }

        [Required]
        public required string SourceOutputName { get; set; }

        public string? Transformation { get; set; }

        public bool IsOptional { get; set; } = false;

        public object? DefaultValue { get; set; }
    }

    public class NodeUserInputDto
    {
        [Required]
        public required string Name { get; set; }

        public string Type { get; set; } = "string";

        public string Label { get; set; } = string.Empty;

        public string Placeholder { get; set; } = string.Empty;

        public bool IsRequired { get; set; } = false;

        public List<string> Options { get; set; } = new();

        public object? DefaultValue { get; set; }

        public Dictionary<string, object> Validation { get; set; } = new();
    }

    public class NodeValidationRuleDto
    {
        [Required]
        public required string Field { get; set; }

        [Required]
        public required string Rule { get; set; }

        public object? Value { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    public class NodeOutputConfigurationDto
    {
        public List<NodeOutputMappingDto> OutputMappings { get; set; } = new();

        public Dictionary<string, object> OutputSchema { get; set; } = new();

        public bool CacheResults { get; set; } = true;

        public int CacheTtlMinutes { get; set; } = 60;
    }

    public class NodeOutputMappingDto
    {
        [Required]
        public required string OutputName { get; set; }

        [Required]
        public required string SourceField { get; set; }

        public string? Transformation { get; set; }

        public string DataType { get; set; } = "string";

        public bool IsArray { get; set; } = false;
    }

    public class NodeExecutionSettingsDto
    {
        [Range(1, 1440)]
        public int TimeoutMinutes { get; set; } = 2880;

        [Range(0, 10)]
        public int RetryCount { get; set; } = 3;

        [Range(1, 3600)]
        public int RetryDelaySeconds { get; set; } = 30;

        public NodeResourceLimitsDto ResourceLimits { get; set; } = new();

        public Dictionary<string, string> Environment { get; set; } = new();

        public bool RunInParallel { get; set; } = true;

        public int Priority { get; set; } = 0;
    }

    public class NodeResourceLimitsDto
    {
        [Range(1, 100)]
        public int MaxCpuPercentage { get; set; } = 80;

        [Range(1, 32768)]
        public long MaxMemoryMb { get; set; } = 1024;

        [Range(1, 102400)]
        public long MaxDiskMb { get; set; } = 2048;
    }

    public class NodeConditionalExecutionDto
    {
        [Required]
        public required string Condition { get; set; }

        public ConditionalType ConditionType { get; set; } = ConditionalType.Expression;

        public bool SkipIfConditionFails { get; set; } = true;

        public string? AlternativeNodeId { get; set; }
    }

    public class NodeUIConfigurationDto
    {
        public string Color { get; set; } = "#4A90E2";

        public string Icon { get; set; } = "program";

        public bool ShowProgress { get; set; } = true;

        public string CustomLabel { get; set; } = string.Empty;
    }

    public class EdgeConditionDto
    {
        [Required]
        public required string Expression { get; set; }

        public EdgeConditionType ConditionType { get; set; } = EdgeConditionType.Expression;

        public bool EvaluateOnSourceOutput { get; set; } = true;

        public object? DefaultValue { get; set; }

        public EdgeFailureAction FailureAction { get; set; } = EdgeFailureAction.Skip;
    }

    public class EdgeTransformationDto
    {
        public EdgeTransformationType TransformationType { get; set; } = EdgeTransformationType.JSONPath;

        [Required]
        public required string Expression { get; set; }

        public Dictionary<string, object>? InputSchema { get; set; }

        public Dictionary<string, object>? OutputSchema { get; set; }

        public string? CustomFunction { get; set; }

        public Dictionary<string, object> Parameters { get; set; } = new();

        public bool ValidateSchema { get; set; } = true;
    }

    public class EdgeUIConfigurationDto
    {
        public string Color { get; set; } = "#999999";

        public EdgeStyle Style { get; set; } = EdgeStyle.Solid;

        public int Width { get; set; } = 2;

        public string Label { get; set; } = string.Empty;

        public bool ShowLabel { get; set; } = false;

        public bool AnimateFlow { get; set; } = false;

        public List<EdgePointDto> Points { get; set; } = new();
    }

    public class EdgePointDto
    {
        public double X { get; set; }

        public double Y { get; set; }

        public EdgePointType Type { get; set; } = EdgePointType.Bezier;
    }

    public class WorkflowImportDto
    {
        [Required]
        public required string WorkflowData { get; set; }

        public string Format { get; set; } = "json";

        [Required]
        [StringLength(200)]
        public required string Name { get; set; }

        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        public bool ImportPermissions { get; set; } = false;

        public List<string> Tags { get; set; } = new();
    }
}