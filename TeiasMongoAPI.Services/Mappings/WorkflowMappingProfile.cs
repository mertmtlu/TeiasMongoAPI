using AutoMapper;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;

namespace TeiasMongoAPI.Services.Mappings
{
    public class WorkflowMappingProfile : Profile
    {
        public WorkflowMappingProfile()
        {
            CreateWorkflowMappings();
            CreateWorkflowNodeMappings();
            CreateWorkflowEdgeMappings();
            CreateWorkflowExecutionMappings();
            CreateWorkflowPermissionMappings();
            CreateWorkflowSettingsMappings();
        }

        private void CreateWorkflowMappings()
        {
            // Workflow mappings
            CreateMap<WorkflowCreateDto, Workflow>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore()) // Will be set by service
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore()) // Will be set by service
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Version, opt => opt.Ignore()) // Will be set by service
                .ForMember(dest => dest.LastExecutionId, opt => opt.Ignore())
                .ForMember(dest => dest.ExecutionCount, opt => opt.Ignore())
                .ForMember(dest => dest.AverageExecutionTime, opt => opt.Ignore());

            CreateMap<WorkflowUpdateDto, Workflow>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore()) // Will be set by service
                .ForMember(dest => dest.Version, opt => opt.Ignore())
                .ForMember(dest => dest.LastExecutionId, opt => opt.Ignore())
                .ForMember(dest => dest.ExecutionCount, opt => opt.Ignore())
                .ForMember(dest => dest.AverageExecutionTime, opt => opt.Ignore())
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<Workflow, WorkflowDetailDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.LastExecutionId, opt => opt.MapFrom(src => src.LastExecutionId != null ? src.LastExecutionId.ToString() : null))
                .ForMember(dest => dest.TemplateId, opt => opt.MapFrom(src => src.TemplateId != null ? src.TemplateId.ToString() : null))
                .ForMember(dest => dest.ValidationResult, opt => opt.Ignore()) // Will be set by service
                .ForMember(dest => dest.ComplexityMetrics, opt => opt.Ignore()); // Will be set by service

            CreateMap<Workflow, WorkflowListDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.NodeCount, opt => opt.MapFrom(src => src.Nodes.Count))
                .ForMember(dest => dest.EdgeCount, opt => opt.MapFrom(src => src.Edges.Count))
                .ForMember(dest => dest.IsPublic, opt => opt.MapFrom(src => src.Permissions.IsPublic))
                .ForMember(dest => dest.ComplexityLevel, opt => opt.Ignore()) // Will be calculated
                .ForMember(dest => dest.HasPermission, opt => opt.Ignore()); // Will be set by service
        }

        private void CreateWorkflowNodeMappings()
        {
            // Workflow Node mappings
            CreateMap<WorkflowNodeCreateDto, WorkflowNode>()
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => MongoDB.Bson.ObjectId.Parse(src.ProgramId)))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.VersionId) ? MongoDB.Bson.ObjectId.Parse(src.VersionId) : (MongoDB.Bson.ObjectId?)null))
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore()) // Will be set by service
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            CreateMap<WorkflowNodeUpdateDto, WorkflowNode>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.ProgramId) ? MongoDB.Bson.ObjectId.Parse(src.ProgramId) : MongoDB.Bson.ObjectId.Empty))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.VersionId) ? MongoDB.Bson.ObjectId.Parse(src.VersionId) : (MongoDB.Bson.ObjectId?)null))
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore()) // Will be set by service
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<WorkflowNode, WorkflowNodeDto>()
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId.ToString()))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => src.VersionId != null ? src.VersionId.ToString() : null))
                .ForMember(dest => dest.ProgramName, opt => opt.Ignore()) // Will be set by service
                .ForMember(dest => dest.ValidationResult, opt => opt.Ignore()); // Will be set by service

            // Node sub-component mappings
            CreateMap<NodePositionDto, NodePosition>().ReverseMap();
            CreateMap<NodeInputConfigurationDto, NodeInputConfiguration>().ReverseMap();
            CreateMap<NodeOutputConfigurationDto, NodeOutputConfiguration>().ReverseMap();
            CreateMap<NodeExecutionSettingsDto, NodeExecutionSettings>().ReverseMap();
            CreateMap<NodeConditionalExecutionDto, NodeConditionalExecution>().ReverseMap();
            CreateMap<NodeUIConfigurationDto, NodeUIConfiguration>().ReverseMap();
            CreateMap<NodeInputMappingDto, NodeInputMapping>().ReverseMap();
            CreateMap<NodeUserInputDto, NodeUserInput>().ReverseMap();
            CreateMap<NodeValidationRuleDto, NodeValidationRule>().ReverseMap();
            CreateMap<NodeOutputMappingDto, NodeOutputMapping>().ReverseMap();
            CreateMap<NodeResourceLimitsDto, NodeResourceLimits>().ReverseMap();
        }

        private void CreateWorkflowEdgeMappings()
        {
            // Workflow Edge mappings
            CreateMap<WorkflowEdgeCreateDto, WorkflowEdge>()
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore()) // Will be set by service
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            CreateMap<WorkflowEdgeUpdateDto, WorkflowEdge>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore()) // Will be set by service
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<WorkflowEdge, WorkflowEdgeDto>()
                .ForMember(dest => dest.ValidationResult, opt => opt.Ignore()); // Will be set by service

            // Edge sub-component mappings
            CreateMap<EdgeConditionDto, EdgeCondition>().ReverseMap();
            CreateMap<EdgeTransformationDto, EdgeTransformation>().ReverseMap();
            CreateMap<EdgeUIConfigurationDto, EdgeUIConfiguration>().ReverseMap();
            CreateMap<EdgePointDto, EdgePoint>().ReverseMap();
        }

        private void CreateWorkflowExecutionMappings()
        {
            // Workflow Execution mappings
            CreateMap<WorkflowExecution, WorkflowExecutionSummaryDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.Duration, opt => opt.MapFrom(src => src.CompletedAt.HasValue ? src.CompletedAt.Value - src.StartedAt : (TimeSpan?)null))
                .ForMember(dest => dest.ErrorMessage, opt => opt.MapFrom(src => src.Error != null ? src.Error.Message : null))
                .ForMember(dest => dest.NodeStatuses, opt => opt.MapFrom(src => src.NodeExecutions.ToDictionary(ne => ne.NodeId, ne => ne.Status)))
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata.ToDictionary()));

            CreateMap<NodeExecution, NodeExecutionDto>()
                .ForMember(dest => dest.ProgramExecutionId, opt => opt.MapFrom(src => src.ProgramExecutionId != null ? src.ProgramExecutionId.ToString() : null));

            // Execution sub-component mappings
            CreateMap<WorkflowExecutionProgress, WorkflowExecutionProgressDto>().ReverseMap();
            CreateMap<WorkflowExecutionResults, WorkflowExecutionResultsDto>().ReverseMap();
            CreateMap<WorkflowExecutionError, WorkflowExecutionErrorDto>().ReverseMap();
            CreateMap<NodeExecutionError, NodeExecutionErrorDto>().ReverseMap();
            CreateMap<WorkflowResourceUsage, WorkflowResourceUsageDto>().ReverseMap();
            CreateMap<WorkflowExecutionLog, WorkflowExecutionLogDto>().ReverseMap();
            CreateMap<WorkflowExecutionStatistics, WorkflowExecutionStatisticsDto>().ReverseMap();
        }

        private void CreateWorkflowPermissionMappings()
        {
            // Permission mappings
            CreateMap<WorkflowPermissions, WorkflowPermissionDto>()
                .ForMember(dest => dest.CurrentUserPermissions, opt => opt.Ignore()); // Will be set by service

            CreateMap<WorkflowPermissionsDto, WorkflowPermissions>().ReverseMap();
            CreateMap<WorkflowUserPermissionDto, WorkflowUserPermission>().ReverseMap();
            CreateMap<WorkflowPermissionUpdateDto, WorkflowPermissions>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        }

        private void CreateWorkflowSettingsMappings()
        {
            // Settings mappings
            CreateMap<WorkflowSettingsDto, WorkflowSettings>().ReverseMap();
            CreateMap<WorkflowRetryPolicyDto, WorkflowRetryPolicy>().ReverseMap();
            CreateMap<WorkflowNotificationSettingsDto, WorkflowNotificationSettings>().ReverseMap();
        }
    }

    // Supporting DTOs for execution mappings
    public class NodeExecutionDto
    {
        public string NodeId { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public string? ProgramExecutionId { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public NodeExecutionStatus Status { get; set; }
        public TimeSpan? Duration { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public bool WasSkipped { get; set; }
        public string? SkipReason { get; set; }
        public NodeExecutionErrorDto? Error { get; set; }
    }

    public class WorkflowExecutionProgressDto
    {
        public int TotalNodes { get; set; }
        public int CompletedNodes { get; set; }
        public int FailedNodes { get; set; }
        public int SkippedNodes { get; set; }
        public int RunningNodes { get; set; }
        public double PercentComplete { get; set; }
        public string CurrentPhase { get; set; } = string.Empty;
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }

    public class WorkflowExecutionResultsDto
    {
        public Dictionary<string, object> FinalOutputs { get; set; } = new();
        public Dictionary<string, object> IntermediateResults { get; set; } = new();
        public List<string> ArtifactsGenerated { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public List<WorkflowOutputFileDto> OutputFiles { get; set; } = new();
        public WorkflowExecutionStatisticsDto ExecutionStatistics { get; set; } = new();
    }

    public class WorkflowExecutionErrorDto
    {
        public WorkflowErrorType ErrorType { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? NodeId { get; set; }
        public string? StackTrace { get; set; }
        public string? InnerError { get; set; }
        public DateTime Timestamp { get; set; }
        public bool CanRetry { get; set; }
        public int RetryCount { get; set; }
    }

    public class NodeExecutionErrorDto
    {
        public NodeErrorType ErrorType { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? ExitCode { get; set; }
        public string? StackTrace { get; set; }
        public DateTime Timestamp { get; set; }
        public bool CanRetry { get; set; }
    }

    public class WorkflowResourceUsageDto
    {
        public double TotalCpuTimeSeconds { get; set; }
        public long TotalMemoryUsedBytes { get; set; }
        public long TotalDiskUsedBytes { get; set; }
        public int PeakConcurrentNodes { get; set; }
        public Dictionary<string, object> NodeResourceUsage { get; set; } = new();
    }

    public class WorkflowExecutionLogDto
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? NodeId { get; set; }
        public string Source { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class WorkflowExecutionStatisticsDto
    {
        public TimeSpan TotalExecutionTime { get; set; }
        public TimeSpan AverageNodeExecutionTime { get; set; }
        public string? SlowestNode { get; set; }
        public string? FastestNode { get; set; }
        public int TotalRetries { get; set; }
        public double ParallelizationEfficiency { get; set; }
    }

    public class WorkflowOutputFileDto
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}