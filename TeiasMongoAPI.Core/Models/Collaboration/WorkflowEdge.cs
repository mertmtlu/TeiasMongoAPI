using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public class WorkflowEdge
    {
        [BsonElement("id")]
        public required string Id { get; set; } // Unique edge identifier within workflow

        [BsonElement("sourceNodeId")]
        public required string SourceNodeId { get; set; }

        [BsonElement("targetNodeId")]
        public required string TargetNodeId { get; set; }

        [BsonElement("sourceOutputName")]
        public string SourceOutputName { get; set; } = "default";

        [BsonElement("targetInputName")]
        public string TargetInputName { get; set; } = "default";

        [BsonElement("edgeType")]
        public WorkflowEdgeType EdgeType { get; set; } = WorkflowEdgeType.Data;

        [BsonElement("condition")]
        public EdgeCondition? Condition { get; set; }

        [BsonElement("transformation")]
        public EdgeTransformation? Transformation { get; set; }

        [BsonElement("uiConfiguration")]
        public EdgeUIConfiguration UIConfiguration { get; set; } = new();

        [BsonElement("metadata")]
        public BsonDocument Metadata { get; set; } = new();

        [BsonElement("isDisabled")]
        public bool IsDisabled { get; set; } = false;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }

    public class EdgeCondition
    {
        [BsonElement("expression")]
        public required string Expression { get; set; } // Condition expression

        [BsonElement("conditionType")]
        public EdgeConditionType ConditionType { get; set; } = EdgeConditionType.Expression;

        [BsonElement("evaluateOnSourceOutput")]
        public bool EvaluateOnSourceOutput { get; set; } = true;

        [BsonElement("defaultValue")]
        public BsonValue? DefaultValue { get; set; }

        [BsonElement("failureAction")]
        public EdgeFailureAction FailureAction { get; set; } = EdgeFailureAction.Skip;
    }

    public class EdgeTransformation
    {
        [BsonElement("transformationType")]
        public EdgeTransformationType TransformationType { get; set; } = EdgeTransformationType.JSONPath;

        [BsonElement("expression")]
        public required string Expression { get; set; } // JSONPath, JMESPath, or custom expression

        [BsonElement("inputSchema")]
        public BsonDocument? InputSchema { get; set; }

        [BsonElement("outputSchema")]
        public BsonDocument? OutputSchema { get; set; }

        [BsonElement("customFunction")]
        public string? CustomFunction { get; set; }

        [BsonElement("parameters")]
        public BsonDocument Parameters { get; set; } = new();

        [BsonElement("validateSchema")]
        public bool ValidateSchema { get; set; } = true;
    }

    public class EdgeUIConfiguration
    {
        [BsonElement("color")]
        public string Color { get; set; } = "#999999";

        [BsonElement("style")]
        public EdgeStyle Style { get; set; } = EdgeStyle.Solid;

        [BsonElement("width")]
        public int Width { get; set; } = 2;

        [BsonElement("label")]
        public string Label { get; set; } = string.Empty;

        [BsonElement("showLabel")]
        public bool ShowLabel { get; set; } = false;

        [BsonElement("animateFlow")]
        public bool AnimateFlow { get; set; } = false;

        [BsonElement("points")]
        public List<EdgePoint> Points { get; set; } = new();
    }

    public class EdgePoint
    {
        [BsonElement("x")]
        public double X { get; set; }

        [BsonElement("y")]
        public double Y { get; set; }

        [BsonElement("type")]
        public EdgePointType Type { get; set; } = EdgePointType.Bezier;
    }

    public enum WorkflowEdgeType
    {
        Data,           // Data flow connection
        Control,        // Control flow connection
        Conditional,    // Conditional execution
        Parallel,       // Parallel execution trigger
        Merge,          // Merge multiple inputs
        Loop           // Loop back connection
    }

    public enum EdgeConditionType
    {
        Expression,     // JavaScript-like expression
        OutputValue,    // Based on output value
        ExecutionStatus, // Based on execution status
        CustomFunction  // Custom condition function
    }

    public enum EdgeFailureAction
    {
        Skip,           // Skip the target node
        Stop,           // Stop workflow execution
        UseDefault,     // Use default value
        Retry,          // Retry the source node
        Continue        // Continue with null/empty value
    }

    public enum EdgeTransformationType
    {
        JSONPath,       // JSONPath expression
        JMESPath,       // JMESPath expression
        Expression,     // JavaScript-like expression
        CustomFunction, // Custom transformation function
        Template,       // Template-based transformation
        NoTransform     // Pass through without transformation
    }

    public enum EdgeStyle
    {
        Solid,
        Dashed,
        Dotted,
        Double
    }

    public enum EdgePointType
    {
        Straight,
        Bezier,
        Smooth,
        Step
    }
}