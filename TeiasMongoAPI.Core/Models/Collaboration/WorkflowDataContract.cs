using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    /// <summary>
    /// Standardized data contract for inter-node communication in workflows
    /// </summary>
    public class WorkflowDataContract
    {
        [BsonElement("contractId")]
        public string ContractId { get; set; } = Guid.NewGuid().ToString();

        [BsonElement("sourceNodeId")]
        public string SourceNodeId { get; set; } = string.Empty;

        [BsonElement("targetNodeId")]
        public string TargetNodeId { get; set; } = string.Empty;

        [BsonElement("dataType")]
        public WorkflowDataType DataType { get; set; } = WorkflowDataType.JSON;

        [BsonElement("data")]
        public BsonDocument Data { get; set; } = new();

        [BsonElement("metadata")]
        public DataContractMetadata Metadata { get; set; } = new();

        [BsonElement("schema")]
        public BsonDocument? Schema { get; set; }

        [BsonElement("version")]
        public string Version { get; set; } = "1.0";

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        [BsonElement("checksum")]
        public string? Checksum { get; set; }

        [BsonElement("compression")]
        public CompressionType Compression { get; set; } = CompressionType.None;

        [BsonElement("encryption")]
        public EncryptionInfo? Encryption { get; set; }

        [BsonElement("attachments")]
        public List<DataAttachment> Attachments { get; set; } = new();
    }

    public class DataContractMetadata
    {
        [BsonElement("contentType")]
        public string ContentType { get; set; } = "application/json";

        [BsonElement("encoding")]
        public string Encoding { get; set; } = "UTF-8";

        [BsonElement("size")]
        public long Size { get; set; }

        [BsonElement("originalFormat")]
        public string? OriginalFormat { get; set; }

        [BsonElement("transformations")]
        public List<DataTransformation> Transformations { get; set; } = new();

        [BsonElement("validationResults")]
        public List<DataValidationResult> ValidationResults { get; set; } = new();

        [BsonElement("quality")]
        public DataQualityMetrics Quality { get; set; } = new();

        [BsonElement("lineage")]
        public DataLineage Lineage { get; set; } = new();

        [BsonElement("customMetadata")]
        public BsonDocument CustomMetadata { get; set; } = new();
    }

    public class DataTransformation
    {
        [BsonElement("transformationId")]
        public string TransformationId { get; set; } = Guid.NewGuid().ToString();

        [BsonElement("type")]
        public TransformationType Type { get; set; }

        [BsonElement("expression")]
        public string Expression { get; set; } = string.Empty;

        [BsonElement("appliedAt")]
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("appliedBy")]
        public string AppliedBy { get; set; } = string.Empty;

        [BsonElement("inputSchema")]
        public BsonDocument? InputSchema { get; set; }

        [BsonElement("outputSchema")]
        public BsonDocument? OutputSchema { get; set; }

        [BsonElement("success")]
        public bool Success { get; set; } = true;

        [BsonElement("error")]
        public string? Error { get; set; }
    }

    public class DataValidationResult
    {
        [BsonElement("validationId")]
        public string ValidationId { get; set; } = Guid.NewGuid().ToString();

        [BsonElement("isValid")]
        public bool IsValid { get; set; } = true;

        [BsonElement("errors")]
        public List<string> Errors { get; set; } = new();

        [BsonElement("warnings")]
        public List<string> Warnings { get; set; } = new();

        [BsonElement("validatedAt")]
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("validationType")]
        public ValidationType ValidationType { get; set; }

        [BsonElement("schemaUsed")]
        public BsonDocument? SchemaUsed { get; set; }
    }

    public class DataQualityMetrics
    {
        [BsonElement("completeness")]
        public double Completeness { get; set; } = 1.0;

        [BsonElement("accuracy")]
        public double Accuracy { get; set; } = 1.0;

        [BsonElement("consistency")]
        public double Consistency { get; set; } = 1.0;

        [BsonElement("validity")]
        public double Validity { get; set; } = 1.0;

        [BsonElement("timeliness")]
        public double Timeliness { get; set; } = 1.0;

        [BsonElement("uniqueness")]
        public double Uniqueness { get; set; } = 1.0;

        [BsonElement("overallScore")]
        public double OverallScore { get; set; } = 1.0;

        [BsonElement("issues")]
        public List<DataQualityIssue> Issues { get; set; } = new();
    }

    public class DataQualityIssue
    {
        [BsonElement("type")]
        public DataQualityIssueType Type { get; set; }

        [BsonElement("severity")]
        public IssueSeverity Severity { get; set; }

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("field")]
        public string? Field { get; set; }

        [BsonElement("recommendation")]
        public string? Recommendation { get; set; }
    }

    public class DataLineage
    {
        [BsonElement("sourceNodes")]
        public List<string> SourceNodes { get; set; } = new();

        [BsonElement("transformationPath")]
        public List<string> TransformationPath { get; set; } = new();

        [BsonElement("dependencies")]
        public List<DataDependency> Dependencies { get; set; } = new();

        [BsonElement("originalSources")]
        public List<DataSource> OriginalSources { get; set; } = new();
    }

    public class DataDependency
    {
        [BsonElement("nodeId")]
        public string NodeId { get; set; } = string.Empty;

        [BsonElement("outputName")]
        public string OutputName { get; set; } = string.Empty;

        [BsonElement("dependencyType")]
        public DependencyType DependencyType { get; set; }

        [BsonElement("isOptional")]
        public bool IsOptional { get; set; } = false;
    }

    public class DataSource
    {
        [BsonElement("sourceId")]
        public string SourceId { get; set; } = string.Empty;

        [BsonElement("sourceType")]
        public DataSourceType SourceType { get; set; }

        [BsonElement("location")]
        public string Location { get; set; } = string.Empty;

        [BsonElement("accessedAt")]
        public DateTime AccessedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("version")]
        public string? Version { get; set; }
    }

    public class EncryptionInfo
    {
        [BsonElement("algorithm")]
        public string Algorithm { get; set; } = string.Empty;

        [BsonElement("keyId")]
        public string KeyId { get; set; } = string.Empty;

        [BsonElement("isEncrypted")]
        public bool IsEncrypted { get; set; } = false;

        [BsonElement("encryptedFields")]
        public List<string> EncryptedFields { get; set; } = new();
    }

    public class DataAttachment
    {
        [BsonElement("attachmentId")]
        public string AttachmentId { get; set; } = Guid.NewGuid().ToString();

        [BsonElement("fileName")]
        public string FileName { get; set; } = string.Empty;

        [BsonElement("contentType")]
        public string ContentType { get; set; } = string.Empty;

        [BsonElement("size")]
        public long Size { get; set; }

        [BsonElement("storagePath")]
        public string StoragePath { get; set; } = string.Empty;

        [BsonElement("checksum")]
        public string Checksum { get; set; } = string.Empty;

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum WorkflowDataType
    {
        JSON,
        XML,
        CSV,
        Binary,
        Text,
        Image,
        Video,
        Audio,
        Document,
        Archive,
        Custom
    }

    public enum CompressionType
    {
        None,
        Gzip,
        Deflate,
        Brotli,
        LZ4,
        Zstd
    }

    public enum TransformationType
    {
        JSONPath,
        JMESPath,
        XPath,
        Regex,
        Template,
        CustomFunction,
        Mapping,
        Aggregation,
        Filter,
        Sort
    }

    public enum ValidationType
    {
        Schema,
        DataType,
        Range,
        Pattern,
        Custom,
        Business,
        Integrity
    }

    public enum DataQualityIssueType
    {
        MissingValue,
        InvalidFormat,
        OutOfRange,
        Duplicate,
        Inconsistent,
        Outdated,
        Malformed,
        Incomplete
    }

    public enum IssueSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum DependencyType
    {
        Required,
        Optional,
        Conditional,
        Parallel
    }

    public enum DataSourceType
    {
        Database,
        File,
        API,
        Stream,
        Cache,
        UserInput,
        Generated,
        External
    }
}