using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IDeploymentService
    {
        /// <summary>
        /// Deploy a pre-built web application (Angular, React, Vue dist folder)
        /// </summary>
        Task<ProgramDeploymentDto> DeployPreBuiltAppAsync(string programId, AppDeploymentRequestDto request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deploy a static site (HTML, CSS, JS files)
        /// </summary>
        Task<ProgramDeploymentDto> DeployStaticSiteAsync(string programId, StaticSiteDeploymentRequestDto request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deploy a container application using Docker
        /// </summary>
        Task<ProgramDeploymentDto> DeployContainerAppAsync(string programId, ContainerDeploymentRequestDto request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get deployment status for a program
        /// </summary>
        Task<ProgramDeploymentStatusDto> GetDeploymentStatusAsync(string programId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Start a deployed application
        /// </summary>
        Task<bool> StartApplicationAsync(string programId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop a deployed application
        /// </summary>
        Task<bool> StopApplicationAsync(string programId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restart a deployed application
        /// </summary>
        Task<bool> RestartApplicationAsync(string programId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get application logs
        /// </summary>
        Task<List<string>> GetApplicationLogsAsync(string programId, int lines = 100, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update deployment configuration
        /// </summary>
        Task<ProgramDto> UpdateDeploymentConfigAsync(string programId, AppDeploymentConfigUpdateDto config, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get application health status
        /// </summary>
        Task<ApplicationHealthDto> GetApplicationHealthAsync(string programId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Scale application instances (for container deployments)
        /// </summary>
        Task<bool> ScaleApplicationAsync(string programId, int instances, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get application metrics
        /// </summary>
        Task<ApplicationMetricsDto> GetApplicationMetricsAsync(string programId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Undeploy an application
        /// </summary>
        Task<bool> UndeployApplicationAsync(string programId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate deployment configuration
        /// </summary>
        Task<DeploymentValidationResult> ValidateDeploymentAsync(string programId, AppDeploymentRequestDto request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get supported deployment options for a program
        /// </summary>
        Task<List<SupportedDeploymentOptionDto>> GetSupportedDeploymentOptionsAsync(string programId, CancellationToken cancellationToken = default);
    }

    // Supporting DTOs for deployment requests
    public class AppDeploymentRequestDto
    {
        public AppDeploymentType DeploymentType { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
        public Dictionary<string, string> Environment { get; set; } = new();
        public List<string> SupportedFeatures { get; set; } = new();
        public bool AutoStart { get; set; } = true;
        public string? DomainName { get; set; }
        public int? Port { get; set; }
        public string? BaseHref { get; set; }
        public bool SpaRouting { get; set; } = false;
        public bool ApiIntegration { get; set; } = true;
        public string AuthenticationMode { get; set; } = "jwt_injection";
    }

    public class StaticSiteDeploymentRequestDto : AppDeploymentRequestDto
    {
        public string EntryPoint { get; set; } = "index.html";
        public string CachingStrategy { get; set; } = "aggressive";
        public bool CdnEnabled { get; set; } = false;
        public Dictionary<string, string> Headers { get; set; } = new();
    }

    public class ContainerDeploymentRequestDto : AppDeploymentRequestDto
    {
        public string DockerfilePath { get; set; } = "Dockerfile";
        public string? ImageName { get; set; }
        public string? ImageTag { get; set; }
        public Dictionary<string, string> BuildArgs { get; set; } = new();
        public List<ContainerPortMappingDto> PortMappings { get; set; } = new();
        public List<ContainerVolumeMountDto> VolumeMounts { get; set; } = new();
        public ContainerResourceLimitsDto ResourceLimits { get; set; } = new();
        public int Replicas { get; set; } = 1;
        public ContainerHealthCheckDto? HealthCheck { get; set; }
    }

    public class AppDeploymentConfigUpdateDto
    {
        public Dictionary<string, object> Configuration { get; set; } = new();
        public Dictionary<string, string> Environment { get; set; } = new();
        public List<string> SupportedFeatures { get; set; } = new();
        public string? DomainName { get; set; }
        public int? Port { get; set; }
    }

    public class ContainerPortMappingDto
    {
        public int ContainerPort { get; set; }
        public int? HostPort { get; set; }
        public string Protocol { get; set; } = "tcp";
    }

    public class ContainerVolumeMountDto
    {
        public string ContainerPath { get; set; } = string.Empty;
        public string HostPath { get; set; } = string.Empty;
        public string Type { get; set; } = "bind"; // bind, volume, tmpfs
        public bool ReadOnly { get; set; } = false;
    }

    public class ContainerResourceLimitsDto
    {
        public string? CpuLimit { get; set; } // e.g., "0.5" for half a CPU
        public string? MemoryLimit { get; set; } // e.g., "512M", "1G"
        public string? CpuRequest { get; set; }
        public string? MemoryRequest { get; set; }
    }

    public class ContainerHealthCheckDto
    {
        public string Command { get; set; } = string.Empty;
        public int IntervalSeconds { get; set; } = 30;
        public int TimeoutSeconds { get; set; } = 10;
        public int Retries { get; set; } = 3;
        public int StartPeriodSeconds { get; set; } = 60;
    }

    // Response DTOs
    public class ApplicationHealthDto
    {
        public string Status { get; set; } = string.Empty; // "healthy", "unhealthy", "starting", "unknown"
        public DateTime LastCheck { get; set; }
        public int ResponseTimeMs { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
        public List<HealthCheckResultDto> Checks { get; set; } = new();
    }

    public class HealthCheckResultDto
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CheckedAt { get; set; }
        public int DurationMs { get; set; }
        public string? Message { get; set; }
    }

    public class ApplicationMetricsDto
    {
        public string ProgramId { get; set; } = string.Empty;
        public DateTime CollectedAt { get; set; }
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long DiskUsageBytes { get; set; }
        public int NetworkConnectionsCount { get; set; }
        public double RequestsPerSecond { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public int ActiveInstances { get; set; }
        public Dictionary<string, object> CustomMetrics { get; set; } = new();
    }

    public class DeploymentValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public Dictionary<string, object> ValidatedConfiguration { get; set; } = new();
    }

    public class SupportedDeploymentOptionDto
    {
        public AppDeploymentType DeploymentType { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsRecommended { get; set; }
        public List<string> RequiredFeatures { get; set; } = new();
        public List<string> SupportedFeatures { get; set; } = new();
        public Dictionary<string, object> DefaultConfiguration { get; set; } = new();
    }
}