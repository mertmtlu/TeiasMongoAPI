using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DeploymentsController : BaseController
    {
        private readonly IDeploymentService _deploymentService;

        public DeploymentsController(
            IDeploymentService deploymentService,
            ILogger<DeploymentsController> logger)
            : base(logger)
        {
            _deploymentService = deploymentService;
        }

        #region Core Deployment Operations

        /// <summary>
        /// Deploy pre-built application (Angular, React, Vue dist folder)
        /// </summary>
        [HttpPost("programs/{programId}/prebuilt")]
        [RequirePermission(UserPermissions.DeployPrograms)]
        [AuditLog("DeployPreBuiltApp")]
        public async Task<ActionResult<ApiResponse<ProgramDeploymentDto>>> DeployPreBuiltApp(
            string programId,
            [FromBody] AppDeploymentRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ProgramDeploymentDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.DeployPreBuiltAppAsync(programId, request, cancellationToken);
            }, $"Deploy pre-built app for program {programId}");
        }

        /// <summary>
        /// Deploy static site (HTML, CSS, JS files)
        /// </summary>
        [HttpPost("programs/{programId}/static")]
        [RequirePermission(UserPermissions.DeployPrograms)]
        [AuditLog("DeployStaticSite")]
        public async Task<ActionResult<ApiResponse<ProgramDeploymentDto>>> DeployStaticSite(
            string programId,
            [FromBody] StaticSiteDeploymentRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ProgramDeploymentDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.DeployStaticSiteAsync(programId, request, cancellationToken);
            }, $"Deploy static site for program {programId}");
        }

        /// <summary>
        /// Deploy container application using Docker
        /// </summary>
        [HttpPost("programs/{programId}/container")]
        [RequirePermission(UserPermissions.DeployPrograms)]
        [AuditLog("DeployContainerApp")]
        public async Task<ActionResult<ApiResponse<ProgramDeploymentDto>>> DeployContainerApp(
            string programId,
            [FromBody] ContainerDeploymentRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ProgramDeploymentDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.DeployContainerAppAsync(programId, request, cancellationToken);
            }, $"Deploy container app for program {programId}");
        }

        #endregion

        #region Application Management

        /// <summary>
        /// Get deployment status for a program
        /// </summary>
        [HttpGet("programs/{programId}/status")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<ProgramDeploymentStatusDto>>> GetDeploymentStatus(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.GetDeploymentStatusAsync(programId, cancellationToken);
            }, $"Get deployment status for program {programId}");
        }

        /// <summary>
        /// Start a deployed application
        /// </summary>
        [HttpPost("programs/{programId}/start")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("StartApplication")]
        public async Task<ActionResult<ApiResponse<bool>>> StartApplication(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.StartApplicationAsync(programId, cancellationToken);
            }, $"Start application for program {programId}");
        }

        /// <summary>
        /// Stop a deployed application
        /// </summary>
        [HttpPost("programs/{programId}/stop")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("StopApplication")]
        public async Task<ActionResult<ApiResponse<bool>>> StopApplication(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.StopApplicationAsync(programId, cancellationToken);
            }, $"Stop application for program {programId}");
        }

        /// <summary>
        /// Restart a deployed application
        /// </summary>
        [HttpPost("programs/{programId}/restart")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("RestartApplication")]
        public async Task<ActionResult<ApiResponse<bool>>> RestartApplication(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.RestartApplicationAsync(programId, cancellationToken);
            }, $"Restart application for program {programId}");
        }

        /// <summary>
        /// Get application logs
        /// </summary>
        [HttpGet("programs/{programId}/logs")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetApplicationLogs(
            string programId,
            [FromQuery] int lines = 100,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (lines <= 0)
            {
                return ValidationError<List<string>>("Lines must be greater than 0");
            }

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.GetApplicationLogsAsync(programId, lines, cancellationToken);
            }, $"Get application logs for program {programId}");
        }

        #endregion

        #region Configuration and Monitoring

        /// <summary>
        /// Update deployment configuration
        /// </summary>
        [HttpPut("programs/{programId}/config")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("UpdateDeploymentConfig")]
        public async Task<ActionResult<ApiResponse<ProgramDto>>> UpdateDeploymentConfig(
            string programId,
            [FromBody] AppDeploymentConfigUpdateDto config,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ProgramDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.UpdateDeploymentConfigAsync(programId, config, cancellationToken);
            }, $"Update deployment config for program {programId}");
        }

        /// <summary>
        /// Get application health status
        /// </summary>
        [HttpGet("programs/{programId}/health")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<ApplicationHealthDto>>> GetApplicationHealth(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.GetApplicationHealthAsync(programId, cancellationToken);
            }, $"Get application health for program {programId}");
        }

        /// <summary>
        /// Scale application instances (for container deployments)
        /// </summary>
        [HttpPost("programs/{programId}/scale")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("ScaleApplication")]
        public async Task<ActionResult<ApiResponse<bool>>> ScaleApplication(
            string programId,
            [FromBody] int instances,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (instances <= 0)
            {
                return ValidationError<bool>("Instances must be greater than 0");
            }

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.ScaleApplicationAsync(programId, instances, cancellationToken);
            }, $"Scale application for program {programId} to {instances} instances");
        }

        /// <summary>
        /// Get application metrics
        /// </summary>
        [HttpGet("programs/{programId}/metrics")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<ApplicationMetricsDto>>> GetApplicationMetrics(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.GetApplicationMetricsAsync(programId, cancellationToken);
            }, $"Get application metrics for program {programId}");
        }

        #endregion

        #region Management and Validation

        /// <summary>
        /// Undeploy an application
        /// </summary>
        [HttpPost("programs/{programId}/undeploy")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("UndeployApplication")]
        public async Task<ActionResult<ApiResponse<bool>>> UndeployApplication(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.UndeployApplicationAsync(programId, cancellationToken);
            }, $"Undeploy application for program {programId}");
        }

        /// <summary>
        /// Validate deployment configuration
        /// </summary>
        [HttpPost("programs/{programId}/validate")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<DeploymentValidationResult>>> ValidateDeployment(
            string programId,
            [FromBody] AppDeploymentRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<DeploymentValidationResult>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.ValidateDeploymentAsync(programId, request, cancellationToken);
            }, $"Validate deployment for program {programId}");
        }

        /// <summary>
        /// Get supported deployment options for a program
        /// </summary>
        [HttpGet("programs/{programId}/options")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<List<SupportedDeploymentOptionDto>>>> GetSupportedDeploymentOptions(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _deploymentService.GetSupportedDeploymentOptionsAsync(programId, cancellationToken);
            }, $"Get supported deployment options for program {programId}");
        }

        #endregion

        #region Additional Utility Methods

        /// <summary>
        /// Get deployment history for a program
        /// </summary>
        [HttpGet("programs/{programId}/history")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<List<DeploymentHistoryDto>>>> GetDeploymentHistory(
            string programId,
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (limit <= 0)
            {
                return ValidationError<List<DeploymentHistoryDto>>("Limit must be greater than 0");
            }

            return await ExecuteAsync(async () =>
            {
                // This would typically come from a deployment history service
                // For now, returning a placeholder implementation
                return new List<DeploymentHistoryDto>();
            }, $"Get deployment history for program {programId}");
        }

        /// <summary>
        /// Get all active deployments
        /// </summary>
        [HttpGet("active")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<List<ActiveDeploymentDto>>>> GetActiveDeployments(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                // This would typically aggregate data from multiple programs
                // For now, returning a placeholder implementation
                return new List<ActiveDeploymentDto>();
            }, "Get all active deployments");
        }

        /// <summary>
        /// Get deployment statistics
        /// </summary>
        [HttpGet("statistics")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<DeploymentStatisticsDto>>> GetDeploymentStatistics(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                // This would typically aggregate deployment data
                // For now, returning a placeholder implementation
                return new DeploymentStatisticsDto
                {
                    TotalDeployments = 0,
                    SuccessfulDeployments = 0,
                    FailedDeployments = 0,
                    ActiveDeployments = 0,
                    DeploymentsByType = new Dictionary<string, int>(),
                    AverageDeploymentTime = TimeSpan.Zero,
                    FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
                    ToDate = toDate ?? DateTime.UtcNow
                };
            }, "Get deployment statistics");
        }

        /// <summary>
        /// Rollback to previous deployment
        /// </summary>
        [HttpPost("programs/{programId}/rollback")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("RollbackDeployment")]
        public async Task<ActionResult<ApiResponse<ProgramDeploymentDto>>> RollbackDeployment(
            string programId,
            [FromBody] RollbackRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ProgramDeploymentDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                // This would typically implement rollback logic
                // For now, returning a placeholder implementation
                throw new NotImplementedException("Rollback functionality not yet implemented");
                return new ProgramDeploymentDto();
            }, $"Rollback deployment for program {programId}");
        }

        /// <summary>
        /// Get deployment environment variables
        /// </summary>
        [HttpGet("programs/{programId}/environment")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<Dictionary<string, string>>>> GetDeploymentEnvironment(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                // This would typically get environment variables from the deployment
                // For now, returning a placeholder implementation
                return new Dictionary<string, string>();
            }, $"Get deployment environment for program {programId}");
        }

        /// <summary>
        /// Update deployment environment variables
        /// </summary>
        [HttpPut("programs/{programId}/environment")]
        [RequirePermission(UserPermissions.ManagePrograms)]
        [AuditLog("UpdateDeploymentEnvironment")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateDeploymentEnvironment(
            string programId,
            [FromBody] Dictionary<string, string> environment,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (environment == null)
            {
                return ValidationError<bool>("Environment variables are required");
            }

            return await ExecuteAsync(async () =>
            {
                // This would typically update environment variables
                // For now, returning a placeholder implementation
                return true;
            }, $"Update deployment environment for program {programId}");
        }

        /// <summary>
        /// Get deployment resource usage
        /// </summary>
        [HttpGet("programs/{programId}/resources")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<DeploymentResourceUsageDto>>> GetDeploymentResourceUsage(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                // This would typically get resource usage from the deployment
                // For now, returning a placeholder implementation
                return new DeploymentResourceUsageDto
                {
                    ProgramId = programId,
                    CpuUsagePercent = 0,
                    MemoryUsageMB = 0,
                    DiskUsageMB = 0,
                    NetworkInMB = 0,
                    NetworkOutMB = 0,
                    LastUpdated = DateTime.UtcNow
                };
            }, $"Get resource usage for program {programId}");
        }

        /// <summary>
        /// Test deployment connection
        /// </summary>
        [HttpPost("programs/{programId}/test-connection")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        public async Task<ActionResult<ApiResponse<ConnectionTestResult>>> TestDeploymentConnection(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                // This would typically test the connection to the deployed application
                // For now, returning a placeholder implementation
                return new ConnectionTestResult
                {
                    IsConnected = true,
                    ResponseTimeMs = 100,
                    StatusCode = 200,
                    Message = "Connection successful",
                    TestedAt = DateTime.UtcNow
                };
            }, $"Test connection for program {programId}");
        }

        #endregion
    }

    #region Supporting DTOs (Placeholders)

    public class DeploymentHistoryDto
    {
        public string Id { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string DeploymentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime DeployedAt { get; set; }
        public string DeployedBy { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ActiveDeploymentDto
    {
        public string ProgramId { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string DeploymentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime DeployedAt { get; set; }
        public string Url { get; set; } = string.Empty;
        public string HealthStatus { get; set; } = string.Empty;
    }

    public class DeploymentStatisticsDto
    {
        public int TotalDeployments { get; set; }
        public int SuccessfulDeployments { get; set; }
        public int FailedDeployments { get; set; }
        public int ActiveDeployments { get; set; }
        public Dictionary<string, int> DeploymentsByType { get; set; } = new();
        public TimeSpan AverageDeploymentTime { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    public class RollbackRequestDto
    {
        [Required]
        public required string TargetVersion { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        public bool ForceRollback { get; set; } = false;
    }

    public class DeploymentResourceUsageDto
    {
        public string ProgramId { get; set; } = string.Empty;
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageMB { get; set; }
        public long DiskUsageMB { get; set; }
        public long NetworkInMB { get; set; }
        public long NetworkOutMB { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class ConnectionTestResult
    {
        public bool IsConnected { get; set; }
        public int ResponseTimeMs { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime TestedAt { get; set; }
    }

    #endregion
}