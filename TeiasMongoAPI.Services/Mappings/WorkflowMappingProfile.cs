using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;

namespace TeiasMongoAPI.Services.Mappings
{
    public class WorkflowMappingProfile : Profile
    {
        public WorkflowMappingProfile()
        {
            // Initialize type converters for AutoMapper
            CreateTypeConverters();

            CreateWorkflowMappings();
            CreateWorkflowNodeMappings();
            CreateWorkflowEdgeMappings();
            CreateWorkflowExecutionMappings();
            CreateWorkflowPermissionMappings();
            CreateWorkflowSettingsMappings();
            CreateWorkflowDataContractMappings();
        }

        private void CreateTypeConverters()
        {
            // Type converters are no longer needed since both DTOs and domain models now use Dictionary<string, object>
            // Direct mapping will work for Dictionary<string, object> properties
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

            CreateMap<NodeExecution, NodeExecutionResponseDto>()
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId.ToString()))
                .ForMember(dest => dest.Duration, opt => opt.MapFrom(src => src.CompletedAt.HasValue && src.StartedAt.HasValue ? src.CompletedAt.Value - src.StartedAt.Value : (TimeSpan?)null));

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

            CreateMap<WorkflowNameDescriptionUpdateDto, Workflow>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore()) // Will be set by service
                .ForMember(dest => dest.Version, opt => opt.Ignore())
                .ForMember(dest => dest.LastExecutionId, opt => opt.Ignore())
                .ForMember(dest => dest.ExecutionCount, opt => opt.Ignore())
                .ForMember(dest => dest.AverageExecutionTime, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore()) // Don't update status in name/description update
                .ForMember(dest => dest.Nodes, opt => opt.Ignore()) // Don't update nodes
                .ForMember(dest => dest.Edges, opt => opt.Ignore()) // Don't update edges
                .ForMember(dest => dest.Settings, opt => opt.Ignore()) // Don't update settings
                .ForMember(dest => dest.Permissions, opt => opt.Ignore()) // Don't update permissions
                .ForMember(dest => dest.Tags, opt => opt.Ignore()) // Don't update tags
                .ForMember(dest => dest.Metadata, opt => opt.Ignore()) // Don't update metadata
                .ForMember(dest => dest.IsTemplate, opt => opt.Ignore()) // Don't update template flag
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<Workflow, WorkflowDetailDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.LastExecutionId, opt => opt.MapFrom(src => src.LastExecutionId != null ? src.LastExecutionId.ToString() : null))
                .ForMember(dest => dest.TemplateId, opt => opt.MapFrom(src => src.TemplateId != null ? src.TemplateId.ToString() : null))
                .ForMember(dest => dest.ValidationResult, opt => opt.Ignore()) // Will be set by service
                .ForMember(dest => dest.ComplexityMetrics, opt => opt.Ignore()); // Will be set by service

            CreateMap<Workflow, WorkflowListDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.NodeCount, opt => opt.MapFrom(src => src.Nodes != null ? src.Nodes.Count : 0))
                .ForMember(dest => dest.EdgeCount, opt => opt.MapFrom(src => src.Edges != null ? src.Edges.Count : 0))
                .ForMember(dest => dest.IsPublic, opt => opt.MapFrom(src => src.Permissions != null ? src.Permissions.IsPublic : false))
                .ForMember(dest => dest.ComplexityLevel, opt => opt.Ignore()) // Will be calculated
                .ForMember(dest => dest.HasPermission, opt => opt.Ignore()); // Will be set by service
        }

        private void CreateWorkflowNodeMappings()
        {
            // Workflow Node mappings
            CreateMap<WorkflowNodeCreateDto, WorkflowNode>()
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => ObjectId.Parse(src.ProgramId)))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.VersionId) ? ObjectId.Parse(src.VersionId) : (ObjectId?)null))
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore()) // Will be set by service
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            CreateMap<WorkflowNodeUpdateDto, WorkflowNode>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.ProgramId) ? ObjectId.Parse(src.ProgramId) : ObjectId.Empty))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.VersionId) ? ObjectId.Parse(src.VersionId) : (ObjectId?)null))
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore()) // Will be set by service
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<WorkflowNodeBulkUpdateDto, WorkflowNode>()
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.ProgramId) ? ObjectId.Parse(src.ProgramId) : ObjectId.Empty))
                .ForMember(dest => dest.VersionId, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.VersionId) ? ObjectId.Parse(src.VersionId) : (ObjectId?)null))
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

            CreateMap<WorkflowEdgeBulkUpdateDto, WorkflowEdge>()
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
            CreateMap<WorkflowExecution, WorkflowExecutionResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.WorkflowId, opt => opt.MapFrom(src => src.WorkflowId.ToString()));

            // Workflow Execution mappings
            CreateMap<WorkflowExecution, WorkflowExecutionSummaryDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.Duration, opt => opt.MapFrom(src => src.CompletedAt.HasValue ? src.CompletedAt.Value - src.StartedAt : (TimeSpan?)null))
                .ForMember(dest => dest.ErrorMessage, opt => opt.MapFrom(src => src.Error != null ? src.Error.Message : null))
                .ForMember(dest => dest.NodeStatuses, opt => opt.MapFrom(src => src.NodeExecutions != null ? src.NodeExecutions.ToDictionary(ne => ne.NodeId, ne => ne.Status) : new Dictionary<string, NodeExecutionStatus>()))
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata)); // Direct mapping since both are Dictionary<string, object>

            CreateMap<NodeExecution, NodeExecutionDto>()
                .ForMember(dest => dest.ProgramExecutionId, opt => opt.MapFrom(src => src.ProgramExecutionId != null ? src.ProgramExecutionId.ToString() : null))
                .ForMember(dest => dest.Duration, opt => opt.MapFrom(src => src.CompletedAt.HasValue && src.StartedAt.HasValue ? src.CompletedAt.Value - src.StartedAt.Value : (TimeSpan?)null));

            // Execution sub-component mappings - handle Dictionary conversions explicitly
            CreateMap<WorkflowExecutionProgress, WorkflowExecutionProgressDto>().ReverseMap();

            CreateMap<WorkflowExecutionResults, WorkflowExecutionResultsDto>()
                .ForMember(dest => dest.FinalOutputs, opt => opt.MapFrom(src => ConvertBsonDocumentToDictionary(src.FinalOutputs)))
                .ForMember(dest => dest.IntermediateResults, opt => opt.MapFrom(src => src.IntermediateResults.ToDictionary(kvp => kvp.Key, kvp => ConvertBsonDocumentToDictionary(kvp.Value))))
                .ReverseMap();

            CreateMap<WorkflowExecutionError, WorkflowExecutionErrorDto>().ReverseMap();
            CreateMap<NodeExecutionError, NodeExecutionErrorDto>().ReverseMap();

            CreateMap<WorkflowResourceUsage, WorkflowResourceUsageDto>()
                .ForMember(dest => dest.NodeResourceUsage, opt => opt.MapFrom(src => src.NodeResourceUsage)) // Direct mapping
                .ReverseMap();

            CreateMap<WorkflowExecutionLog, WorkflowExecutionLogDto>()
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata)) // Direct mapping
                .ReverseMap();

            CreateMap<WorkflowExecutionLog, WorkflowExecutionLogResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Set by service
                .ForMember(dest => dest.ExecutionId, opt => opt.Ignore()) // Set by service 
                .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()))
                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => ConvertBsonDocumentToDictionary(src.Metadata)))
                .ForMember(dest => dest.Exception, opt => opt.Ignore()); // Set by service if needed

            CreateMap<WorkflowExecutionContext, WorkflowExecutionContextDto>()
                .ForMember(dest => dest.UserInputs, opt => opt.MapFrom(src => ConvertBsonDocumentToDictionary(src.UserInputs)))
                .ForMember(dest => dest.GlobalVariables, opt => opt.MapFrom(src => ConvertBsonDocumentToDictionary(src.GlobalVariables)));

            CreateMap<WorkflowExecutionContextDto, WorkflowExecutionContext>()
                .ForMember(dest => dest.UserInputs, opt => opt.MapFrom(src => ConvertDictionaryToBsonDocument(src.UserInputs)))
                .ForMember(dest => dest.GlobalVariables, opt => opt.MapFrom(src => ConvertDictionaryToBsonDocument(src.GlobalVariables)));

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

        private void CreateWorkflowDataContractMappings()
        {
            // WorkflowDataContract to DTO mappings
            CreateMap<WorkflowDataContract, WorkflowDataContractDto>()
                .ForMember(dest => dest.Data, opt => opt.MapFrom(src => ConvertBsonDocumentToDictionary(src.Data)))
                .ForMember(dest => dest.Schema, opt => opt.MapFrom(src => src.Schema != null ? ConvertBsonDocumentToDictionary(src.Schema) : null));

            CreateMap<DataContractMetadata, DataContractMetadataDto>()
                .ForMember(dest => dest.CustomMetadata, opt => opt.MapFrom(src => ConvertBsonDocumentToDictionary(src.CustomMetadata)));

            CreateMap<DataTransformation, DataTransformationDto>()
                .ForMember(dest => dest.InputSchema, opt => opt.MapFrom(src => src.InputSchema != null ? ConvertBsonDocumentToDictionary(src.InputSchema) : null))
                .ForMember(dest => dest.OutputSchema, opt => opt.MapFrom(src => src.OutputSchema != null ? ConvertBsonDocumentToDictionary(src.OutputSchema) : null));

            CreateMap<DataValidationResult, DataValidationResultDto>()
                .ForMember(dest => dest.SchemaUsed, opt => opt.MapFrom(src => src.SchemaUsed != null ? ConvertBsonDocumentToDictionary(src.SchemaUsed) : null));

            CreateMap<DataQualityMetrics, DataQualityMetricsDto>().ReverseMap();
            CreateMap<DataQualityIssue, DataQualityIssueDto>().ReverseMap();
            CreateMap<DataLineage, DataLineageDto>().ReverseMap();
            CreateMap<DataDependency, DataDependencyDto>().ReverseMap();
            CreateMap<DataSource, DataSourceDto>().ReverseMap();
            CreateMap<EncryptionInfo, EncryptionInfoDto>().ReverseMap();
            CreateMap<DataAttachment, DataAttachmentDto>().ReverseMap();
        }

        private static Dictionary<string, object> ConvertBsonDocumentToDictionary(BsonDocument bsonDoc)
        {
            if (bsonDoc == null) return new Dictionary<string, object>();
            
            var result = new Dictionary<string, object>();
            foreach (var element in bsonDoc)
            {
                result[element.Name] = ConvertBsonValueToObject(element.Value);
            }
            return result;
        }

        private static object ConvertBsonValueToObject(BsonValue bsonValue)
        {
            return bsonValue.BsonType switch
            {
                BsonType.Document => ConvertBsonDocumentToDictionary(bsonValue.AsBsonDocument),
                BsonType.Array => bsonValue.AsBsonArray.Select(ConvertBsonValueToObject).ToList(),
                BsonType.String => bsonValue.AsString,
                BsonType.Int32 => bsonValue.AsInt32,
                BsonType.Int64 => bsonValue.AsInt64,
                BsonType.Double => bsonValue.AsDouble,
                BsonType.Boolean => bsonValue.AsBoolean,
                BsonType.DateTime => bsonValue.ToUniversalTime(),
                BsonType.Null => null,
                BsonType.ObjectId => bsonValue.AsObjectId.ToString(),
                _ => bsonValue.ToString()
            };
        }

        private static BsonDocument ConvertDictionaryToBsonDocument(Dictionary<string, object> dictionary)
        {
            if (dictionary == null) return new BsonDocument();
            
            var bsonDoc = new BsonDocument();
            foreach (var kvp in dictionary)
            {
                bsonDoc[kvp.Key] = ConvertObjectToBsonValue(kvp.Value);
            }
            return bsonDoc;
        }

        private static BsonValue ConvertObjectToBsonValue(object obj)
        {
            return obj switch
            {
                null => BsonNull.Value,
                string s => new BsonString(s),
                int i => new BsonInt32(i),
                long l => new BsonInt64(l),
                double d => new BsonDouble(d),
                bool b => new BsonBoolean(b),
                DateTime dt => new BsonDateTime(dt),
                Dictionary<string, object> dict => ConvertDictionaryToBsonDocument(dict),
                IEnumerable<object> list => new BsonArray(list.Select(ConvertObjectToBsonValue)),
                _ => new BsonString(obj.ToString() ?? string.Empty)
            };
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