using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Services.DTOs.Response.Collaboration
{
    public class ProgramDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string MainFile { get; set; } = string.Empty;
        public string UiType { get; set; } = string.Empty;
        public object UiConfiguration { get; set; } = new object();
        public string Creator { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CurrentVersion { get; set; }
        public bool IsPublic { get; set; }
        public object Metadata { get; set; } = new object();
        public AppDeploymentInfo? DeploymentInfo { get; set; }
    }

    public class ProgramListDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string UiType { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CurrentVersion { get; set; }
        public bool IsPublic { get; set; }
        public AppDeploymentType? DeploymentType { get; set; }
        public string? DeploymentStatus { get; set; }
    }

    public class ProgramDetailDto : ProgramDto
    {
        public List<ProgramPermissionDto> Permissions { get; set; } = new();
        public List<ProgramFileDto> Files { get; set; } = new();
        public ProgramDeploymentStatusDto? DeploymentStatus { get; set; }
        public ProgramStatsDto Stats { get; set; } = new();
    }

    public class ProgramPermissionDto
    {
        public string Type { get; set; } = string.Empty; // "user" or "group"
        public string Id { get; set; } = string.Empty; // User ID or Group ID
        public string Name { get; set; } = string.Empty; // User name or Group name
        public string AccessLevel { get; set; } = string.Empty;
    }

    public class ProgramFileDto
    {
        public string Path { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string? Description { get; set; }
        public string Hash { get; set; } = string.Empty;
    }

    public class ProgramFileContentDto
    {
        public string Path { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string? Description { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class ProgramDeploymentDto
    {
        public string Id { get; set; } = string.Empty;
        public AppDeploymentType DeploymentType { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? LastDeployed { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
        public List<string> SupportedFeatures { get; set; } = new();
        public string? ApplicationUrl { get; set; }
        public List<string> Logs { get; set; } = new();
    }

    public class ProgramDeploymentStatusDto
    {
        public AppDeploymentType DeploymentType { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? LastDeployed { get; set; }
        public string? ApplicationUrl { get; set; }
        public bool IsHealthy { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public List<string> RecentLogs { get; set; } = new();
    }

    public class ProgramStatsDto
    {
        public int TotalExecutions { get; set; }
        public int SuccessfulExecutions { get; set; }
        public int FailedExecutions { get; set; }
        public DateTime? LastExecution { get; set; }
        public double AverageExecutionTime { get; set; }
        public int TotalVersions { get; set; }
        public DateTime? LastUpdate { get; set; }
    }
}