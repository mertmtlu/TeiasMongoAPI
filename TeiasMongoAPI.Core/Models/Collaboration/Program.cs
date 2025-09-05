using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TeiasMongoAPI.Core.Models.Base;
using System;
using System.Collections.Generic;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public class Program : AEntityBase
    {
        public required string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;  // Structural Analysis, Design Tool, etc.
        public string Language { get; set; } = string.Empty;  // Python, C#, Rust, etc.
        public string MainFile { get; set; } = string.Empty;  // Entry point
        public string UiType { get; set; } = string.Empty;  // web, desktop, console, custom
        public object UiConfiguration { get; set; } = new object();  // UI-specific settings
        public string CreatorId { get; set; } = string.Empty;  // User ObjectId
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "draft";  // draft, in_review, live
        public string? CurrentVersion { get; set; }  // Reference to version ID
        public ProgramPermissions Permissions { get; set; } = new ProgramPermissions();
        
        [BsonElement("isPublic")]
        public bool IsPublic { get; set; } = false;
        
        public object Metadata { get; set; } = new object();  // Extensible metadata
        public AppDeploymentInfo? DeploymentInfo { get; set; }
    }

    public class AppDeploymentInfo
    {
        public AppDeploymentType DeploymentType { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
        public DateTime? LastDeployed { get; set; }
        public string Status { get; set; } = "inactive"; // inactive, active, building, failed
        public List<string> SupportedFeatures { get; set; } = new();
    }

    public enum AppDeploymentType
    {
        SourceCode,        // Traditional code execution
        PreBuiltWebApp,    // Upload dist/ folder
        DockerContainer,   // Upload Dockerfile + source
        StaticSite,        // Pure HTML/CSS/JS
        MicroFrontend      // Web components/Angular Elements
    }

    public class ProgramPermissions
    {
        public List<GroupPermission> Groups { get; set; } = new List<GroupPermission>();
        public List<UserPermission> Users { get; set; } = new List<UserPermission>();
    }

    public class GroupPermission
    {
        public string GroupId { get; set; } = string.Empty;
        public string AccessLevel { get; set; } = string.Empty;
    }

    public class UserPermission
    {
        public string UserId { get; set; } = string.Empty;
        public string AccessLevel { get; set; } = string.Empty;
    }
}