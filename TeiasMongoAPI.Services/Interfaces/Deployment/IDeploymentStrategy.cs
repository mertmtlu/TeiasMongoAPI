using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;

namespace TeiasMongoAPI.Services.Interfaces.Deployment
{
    /// <summary>
    /// Base interface for all deployment strategies
    /// </summary>
    public interface IDeploymentStrategy
    {
        AppDeploymentType SupportedType { get; }
        Task<DeploymentResult> DeployAsync(string programId, AppDeploymentRequestDto request, List<ProgramFileUploadDto> files, CancellationToken cancellationToken = default);
        Task<bool> StartAsync(string programId, CancellationToken cancellationToken = default);
        Task<bool> StopAsync(string programId, CancellationToken cancellationToken = default);
        Task<bool> RestartAsync(string programId, CancellationToken cancellationToken = default);
        Task<ApplicationHealthDto> GetHealthAsync(string programId, CancellationToken cancellationToken = default);
        Task<List<string>> GetLogsAsync(string programId, int lines, CancellationToken cancellationToken = default);
        Task<ApplicationMetricsDto> GetMetricsAsync(string programId, CancellationToken cancellationToken = default);
        Task<bool> UndeployAsync(string programId, CancellationToken cancellationToken = default);
        Task<DeploymentValidationResult> ValidateAsync(string programId, AppDeploymentRequestDto request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Strategy for deploying pre-built web applications
    /// </summary>
    public interface IPreBuiltAppDeploymentStrategy : IDeploymentStrategy
    {
        Task<string> GetApplicationUrlAsync(string programId, CancellationToken cancellationToken = default);
        Task<bool> InjectConfigurationAsync(string programId, Dictionary<string, object> config, CancellationToken cancellationToken = default);
        Task<bool> UpdateSecurityHeadersAsync(string programId, Dictionary<string, string> headers, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Strategy for deploying static sites
    /// </summary>
    public interface IStaticSiteDeploymentStrategy : IDeploymentStrategy
    {
        Task<bool> SetupCachingAsync(string programId, string strategy, CancellationToken cancellationToken = default);
        Task<bool> EnableCdnAsync(string programId, CancellationToken cancellationToken = default);
        Task<bool> UpdateCustomHeadersAsync(string programId, Dictionary<string, string> headers, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Strategy for deploying container applications
    /// </summary>
    public interface IContainerDeploymentStrategy : IDeploymentStrategy
    {
        Task<string> BuildImageAsync(string programId, ContainerDeploymentRequestDto request, CancellationToken cancellationToken = default);
        Task<bool> ScaleAsync(string programId, int replicas, CancellationToken cancellationToken = default);
        Task<List<ContainerInstanceDto>> GetInstancesAsync(string programId, CancellationToken cancellationToken = default);
        Task<bool> UpdateResourceLimitsAsync(string programId, ContainerResourceLimitsDto limits, CancellationToken cancellationToken = default);
    }

    // Supporting DTOs
    public class DeploymentResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ApplicationUrl { get; set; }
        public string? DeploymentId { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime DeployedAt { get; set; } = DateTime.UtcNow;
        public List<string> Logs { get; set; } = new();
    }

    public class ContainerInstanceDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public string ImageId { get; set; } = string.Empty;
        public Dictionary<string, object> Ports { get; set; } = new();
        public ContainerResourceUsageDto ResourceUsage { get; set; } = new();
    }

    public class ContainerResourceUsageDto
    {
        public double CpuPercent { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long MemoryLimitBytes { get; set; }
        public long NetworkRxBytes { get; set; }
        public long NetworkTxBytes { get; set; }
        public long DiskUsageBytes { get; set; }
    }
}