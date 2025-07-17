using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IWorkflowValidationService
    {
        Task<WorkflowValidationResult> ValidateWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default);
        Task<WorkflowValidationResult> ValidateWorkflowStructureAsync(Workflow workflow, CancellationToken cancellationToken = default);
        Task<WorkflowValidationResult> ValidateWorkflowDependenciesAsync(Workflow workflow, CancellationToken cancellationToken = default);
        Task<WorkflowValidationResult> ValidateWorkflowNodesAsync(Workflow workflow, CancellationToken cancellationToken = default);
        Task<WorkflowValidationResult> ValidateWorkflowEdgesAsync(Workflow workflow, CancellationToken cancellationToken = default);
        Task<WorkflowValidationResult> ValidateWorkflowPermissionsAsync(Workflow workflow, string userId, CancellationToken cancellationToken = default);
        Task<WorkflowValidationResult> ValidateWorkflowExecutionAsync(Workflow workflow, WorkflowExecutionContext context, CancellationToken cancellationToken = default);
        Task<NodeValidationResult> ValidateNodeAsync(WorkflowNode node, CancellationToken cancellationToken = default);
        Task<EdgeValidationResult> ValidateEdgeAsync(WorkflowEdge edge, Workflow workflow, CancellationToken cancellationToken = default);
        Task<bool> HasCyclesAsync(Workflow workflow, CancellationToken cancellationToken = default);
        Task<List<string>> GetTopologicalOrderAsync(Workflow workflow, CancellationToken cancellationToken = default);
        Task<Dictionary<string, List<string>>> GetDependencyGraphAsync(Workflow workflow, CancellationToken cancellationToken = default);
        Task<List<string>> GetOrphanedNodesAsync(Workflow workflow, CancellationToken cancellationToken = default);
        Task<List<string>> GetUnreachableNodesAsync(Workflow workflow, CancellationToken cancellationToken = default);
        Task<WorkflowComplexityMetrics> CalculateComplexityAsync(Workflow workflow, CancellationToken cancellationToken = default);
    }

    public class WorkflowValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<WorkflowValidationError> Errors { get; set; } = new();
        public List<WorkflowValidationWarning> Warnings { get; set; } = new();
        public List<WorkflowValidationInfo> Info { get; set; } = new();
        public WorkflowComplexityMetrics? ComplexityMetrics { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class NodeValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<NodeValidationError> Errors { get; set; } = new();
        public List<NodeValidationWarning> Warnings { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class EdgeValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<EdgeValidationError> Errors { get; set; } = new();
        public List<EdgeValidationWarning> Warnings { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class WorkflowValidationError
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string? NodeId { get; set; }
        public string? EdgeId { get; set; }
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
        public string? SuggestedFix { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class WorkflowValidationWarning
    {
        public string WarningCode { get; set; } = string.Empty;
        public string WarningType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string? NodeId { get; set; }
        public string? EdgeId { get; set; }
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Warning;
        public string? Recommendation { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class WorkflowValidationInfo
    {
        public string InfoCode { get; set; } = string.Empty;
        public string InfoType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class NodeValidationError
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
        public string? SuggestedFix { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class NodeValidationWarning
    {
        public string WarningCode { get; set; } = string.Empty;
        public string WarningType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Warning;
        public string? Recommendation { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class EdgeValidationError
    {
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
        public string? SuggestedFix { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class EdgeValidationWarning
    {
        public string WarningCode { get; set; } = string.Empty;
        public string WarningType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Warning;
        public string? Recommendation { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }

    public class WorkflowComplexityMetrics
    {
        public int TotalNodes { get; set; }
        public int TotalEdges { get; set; }
        public int MaxDepth { get; set; }
        public int MaxWidth { get; set; }
        public int CyclomaticComplexity { get; set; }
        public double ConnectivityRatio { get; set; }
        public int ParallelBranches { get; set; }
        public int ConditionalNodes { get; set; }
        public int LoopNodes { get; set; }
        public ComplexityLevel ComplexityLevel { get; set; } = ComplexityLevel.Simple;
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }

    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public enum ComplexityLevel
    {
        Simple,
        Moderate,
        Complex,
        VeryComplex
    }
}