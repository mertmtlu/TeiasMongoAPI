using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public class WorkflowNode
    {
        [BsonElement("id")]
        public required string Id { get; set; } // Unique node identifier within workflow

        [BsonElement("name")]
        public required string Name { get; set; }

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("programId")]
        public required ObjectId ProgramId { get; set; }

        [BsonElement("versionId")]
        public ObjectId? VersionId { get; set; } // If null, uses latest version

        [BsonElement("nodeType")]
        public WorkflowNodeType NodeType { get; set; } = WorkflowNodeType.Program;

        [BsonElement("position")]
        public NodePosition Position { get; set; } = new();

        [BsonElement("inputConfiguration")]
        public NodeInputConfiguration InputConfiguration { get; set; } = new();

        [BsonElement("outputConfiguration")]
        public NodeOutputConfiguration OutputConfiguration { get; set; } = new();

        [BsonElement("executionSettings")]
        public NodeExecutionSettings ExecutionSettings { get; set; } = new();

        [BsonElement("conditionalExecution")]
        public NodeConditionalExecution? ConditionalExecution { get; set; }

        [BsonElement("uiConfiguration")]
        public NodeUIConfiguration UIConfiguration { get; set; } = new();

        [BsonElement("metadata")]
        public BsonDocument Metadata { get; set; } = new();

        [BsonElement("isDisabled")]
        public bool IsDisabled { get; set; } = false;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }

    public class NodePosition
    {
        [BsonElement("x")]
        public double X { get; set; }

        [BsonElement("y")]
        public double Y { get; set; }

        [BsonElement("width")]
        public double Width { get; set; } = 200;

        [BsonElement("height")]
        public double Height { get; set; } = 100;
    }

    public class NodeInputConfiguration
    {
        [BsonElement("inputMappings")]
        public List<NodeInputMapping> InputMappings { get; set; } = new();

        [BsonElement("staticInputs")]
        public BsonDocument StaticInputs { get; set; } = new();

        [BsonElement("userInputs")]
        public List<NodeUserInput> UserInputs { get; set; } = new();

        [BsonElement("validationRules")]
        public List<NodeValidationRule> ValidationRules { get; set; } = new();
    }

    public class NodeInputMapping
    {
        [BsonElement("inputName")]
        public required string InputName { get; set; }

        [BsonElement("sourceNodeId")]
        public required string SourceNodeId { get; set; }

        [BsonElement("sourceOutputName")]
        public required string SourceOutputName { get; set; }

        [BsonElement("transformation")]
        public string? Transformation { get; set; } // JSONPath or transformation expression

        [BsonElement("isOptional")]
        public bool IsOptional { get; set; } = false;

        [BsonElement("defaultValue")]
        public BsonValue? DefaultValue { get; set; }
    }

    public class NodeUserInput
    {
        [BsonElement("name")]
        public required string Name { get; set; }

        [BsonElement("type")]
        public string Type { get; set; } = "string";

        [BsonElement("label")]
        public string Label { get; set; } = string.Empty;

        [BsonElement("placeholder")]
        public string Placeholder { get; set; } = string.Empty;

        [BsonElement("isRequired")]
        public bool IsRequired { get; set; } = false;

        [BsonElement("options")]
        public List<string> Options { get; set; } = new();

        [BsonElement("defaultValue")]
        public BsonValue? DefaultValue { get; set; }

        [BsonElement("validation")]
        public BsonDocument Validation { get; set; } = new();
    }

    public class NodeValidationRule
    {
        [BsonElement("field")]
        public required string Field { get; set; }

        [BsonElement("rule")]
        public required string Rule { get; set; }

        [BsonElement("value")]
        public BsonValue Value { get; set; } = BsonNull.Value;

        [BsonElement("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class NodeOutputConfiguration
    {
        [BsonElement("outputMappings")]
        public List<NodeOutputMapping> OutputMappings { get; set; } = new();

        [BsonElement("outputSchema")]
        public BsonDocument OutputSchema { get; set; } = new();

        [BsonElement("cacheResults")]
        public bool CacheResults { get; set; } = true;

        [BsonElement("cacheTtlMinutes")]
        public int CacheTtlMinutes { get; set; } = 60;
    }

    public class NodeOutputMapping
    {
        [BsonElement("outputName")]
        public required string OutputName { get; set; }

        [BsonElement("sourceField")]
        public required string SourceField { get; set; }

        [BsonElement("transformation")]
        public string? Transformation { get; set; }

        [BsonElement("dataType")]
        public string DataType { get; set; } = "string";

        [BsonElement("isArray")]
        public bool IsArray { get; set; } = false;
    }

    public class NodeExecutionSettings
    {
        [BsonElement("timeoutMinutes")]
        public int TimeoutMinutes { get; set; } = 2880;

        [BsonElement("retryCount")]
        public int RetryCount { get; set; } = 3;

        [BsonElement("retryDelaySeconds")]
        public int RetryDelaySeconds { get; set; } = 30;

        [BsonElement("resourceLimits")]
        public NodeResourceLimits ResourceLimits { get; set; } = new();

        [BsonElement("environment")]
        public Dictionary<string, string> Environment { get; set; } = new();

        [BsonElement("runInParallel")]
        public bool RunInParallel { get; set; } = true;

        [BsonElement("priority")]
        public int Priority { get; set; } = 0;
    }

    public class NodeResourceLimits
    {
        [BsonElement("maxCpuPercentage")]
        public int MaxCpuPercentage { get; set; } = 80;

        [BsonElement("maxMemoryMb")]
        public long MaxMemoryMb { get; set; } = 1024;

        [BsonElement("maxDiskMb")]
        public long MaxDiskMb { get; set; } = 2048;
    }

    public class NodeConditionalExecution
    {
        [BsonElement("condition")]
        public required string Condition { get; set; } // Expression to evaluate

        [BsonElement("conditionType")]
        public ConditionalType ConditionType { get; set; } = ConditionalType.Expression;

        [BsonElement("skipIfConditionFails")]
        public bool SkipIfConditionFails { get; set; } = true;

        [BsonElement("alternativeNodeId")]
        public string? AlternativeNodeId { get; set; }
    }

    public class NodeUIConfiguration
    {
        [BsonElement("color")]
        public string Color { get; set; } = "#4A90E2";

        [BsonElement("icon")]
        public string Icon { get; set; } = "program";

        [BsonElement("showProgress")]
        public bool ShowProgress { get; set; } = true;

        [BsonElement("customLabel")]
        public string CustomLabel { get; set; } = string.Empty;
    }

    public enum WorkflowNodeType
    {
        Program,
        StartNode,
        EndNode,
        DecisionNode,
        MergeNode,
        SubWorkflow,
        CustomFunction
    }

    public enum ConditionalType
    {
        Expression,
        OutputValue,
        ExecutionStatus,
        CustomFunction
    }
}