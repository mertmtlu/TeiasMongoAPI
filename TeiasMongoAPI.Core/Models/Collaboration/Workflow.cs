using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public class Workflow : AEntityBase
    {
        [BsonElement("name")]
        public required string Name { get; set; }

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("creator")]
        public required string Creator { get; set; } // User ID

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [BsonElement("status")]
        public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;

        [BsonElement("version")]
        public int Version { get; set; } = 1;

        [BsonElement("nodes")]
        public List<WorkflowNode> Nodes { get; set; } = new();

        [BsonElement("edges")]
        public List<WorkflowEdge> Edges { get; set; } = new();

        [BsonElement("settings")]
        public WorkflowSettings Settings { get; set; } = new();

        [BsonElement("permissions")]
        public WorkflowPermissions Permissions { get; set; } = new();

        [BsonElement("isPublic")]
        public bool IsPublic { get; set; } = false;

        [BsonElement("tags")]
        public List<string> Tags { get; set; } = new();

        [BsonElement("metadata")]
        public BsonDocument Metadata { get; set; } = new();

        [BsonElement("isTemplate")]
        public bool IsTemplate { get; set; } = false;

        [BsonElement("templateId")]
        public ObjectId? TemplateId { get; set; }

        [BsonElement("lastExecutionId")]
        public ObjectId? LastExecutionId { get; set; }

        [BsonElement("executionCount")]
        public int ExecutionCount { get; set; } = 0;

        [BsonElement("averageExecutionTime")]
        public TimeSpan? AverageExecutionTime { get; set; }
    }

    public class WorkflowSettings
    {
        [BsonElement("maxConcurrentNodes")]
        public int MaxConcurrentNodes { get; set; } = 5;

        [BsonElement("timeoutMinutes")]
        public int TimeoutMinutes { get; set; } = 2880;

        [BsonElement("retryPolicy")]
        public WorkflowRetryPolicy RetryPolicy { get; set; } = new();

        [BsonElement("enableDebugging")]
        public bool EnableDebugging { get; set; } = false;

        [BsonElement("saveIntermediateResults")]
        public bool SaveIntermediateResults { get; set; } = true;

        [BsonElement("notificationSettings")]
        public WorkflowNotificationSettings NotificationSettings { get; set; } = new();
    }

    public class WorkflowRetryPolicy
    {
        [BsonElement("maxRetries")]
        public int MaxRetries { get; set; } = 3;

        [BsonElement("retryDelaySeconds")]
        public int RetryDelaySeconds { get; set; } = 30;

        [BsonElement("exponentialBackoff")]
        public bool ExponentialBackoff { get; set; } = true;

        [BsonElement("retryOnFailureTypes")]
        public List<string> RetryOnFailureTypes { get; set; } = new() { "Timeout", "ResourceError" };
    }

    public class WorkflowNotificationSettings
    {
        [BsonElement("notifyOnStart")]
        public bool NotifyOnStart { get; set; } = false;

        [BsonElement("notifyOnCompletion")]
        public bool NotifyOnCompletion { get; set; } = true;

        [BsonElement("notifyOnFailure")]
        public bool NotifyOnFailure { get; set; } = true;

        [BsonElement("recipients")]
        public List<string> Recipients { get; set; } = new();
    }

    public class WorkflowPermissions
    {
        [BsonElement("isPublic")]
        public bool IsPublic { get; set; } = false;

        [BsonElement("allowedUsers")]
        public List<string> AllowedUsers { get; set; } = new();

        [BsonElement("allowedRoles")]
        public List<string> AllowedRoles { get; set; } = new();

        [BsonElement("permissions")]
        public List<WorkflowUserPermission> Permissions { get; set; } = new();
    }

    public class WorkflowUserPermission
    {
        [BsonElement("userId")]
        public required string UserId { get; set; }

        [BsonElement("permissions")]
        public List<WorkflowPermissionType> Permissions { get; set; } = new();
    }

    public enum WorkflowStatus
    {
        Draft,
        Active,
        Paused,
        Archived,
        Deprecated
    }

    public enum WorkflowPermissionType
    {
        View,
        Edit,
        Execute,
        Delete,
        Share,
        Admin
    }
}