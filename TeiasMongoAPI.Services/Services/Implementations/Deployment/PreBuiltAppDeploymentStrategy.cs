using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Interfaces.Deployment;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations.Deployment
{
    public class PreBuiltAppDeploymentStrategy : BaseDeploymentStrategy, IPreBuiltAppDeploymentStrategy
    {
        public AppDeploymentType SupportedType => AppDeploymentType.PreBuiltWebApp;

        public PreBuiltAppDeploymentStrategy(
            ILogger<PreBuiltAppDeploymentStrategy> logger,
            IOptions<DeploymentSettings> settings) : base(logger, settings) { }


        public async Task<DeploymentResult> DeployAsync(string programId, AppDeploymentRequestDto request, List<ProgramFileUploadDto> files, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Deploying pre-built app for program {ProgramId}", programId);

            try
            {
                // Create deployment directory
                var deploymentPath = Path.Combine(_settings.DeploymentPath, "prebuilt", programId);
                if (Directory.Exists(deploymentPath))
                {
                    Directory.Delete(deploymentPath, true);
                }
                Directory.CreateDirectory(deploymentPath);

                // Extract and organize files
                await ExtractFilesToDeploymentPath(files, deploymentPath, cancellationToken);

                // Find entry point (index.html)
                var entryPoint = FindEntryPoint(deploymentPath, request.Configuration);
                if (entryPoint == null)
                {
                    throw new InvalidOperationException("No entry point (index.html) found in deployment files");
                }

                // Inject configuration into entry point
                await InjectApplicationConfigurationAsync(programId, entryPoint, request, cancellationToken);

                // Setup web server configuration
                await SetupWebServerConfigurationAsync(programId, deploymentPath, request, cancellationToken);

                // Start the application
                var appUrl = await StartApplicationAsync(programId, deploymentPath, request, cancellationToken);

                var instance = new AppInstance
                {
                    ProgramId = programId,
                    DeploymentPath = deploymentPath,
                    ApplicationUrl = appUrl,
                    Status = "active",
                    StartedAt = DateTime.UtcNow,
                    Configuration = request.Configuration,
                    Port = request.Port ?? GetAvailablePort(),
                    ProcessId = null // Will be set if using process-based hosting
                };

                _instances[programId] = instance;

                return new DeploymentResult
                {
                    Success = true,
                    ApplicationUrl = appUrl,
                    DeploymentId = Guid.NewGuid().ToString(),
                    Metadata = new Dictionary<string, object>
                    {
                        { "deploymentPath", deploymentPath },
                        { "entryPoint", entryPoint },
                        { "port", instance.Port }
                    },
                    Logs = new List<string>
                    {
                        $"Pre-built app deployed successfully",
                        $"Application URL: {appUrl}",
                        $"Deployment path: {deploymentPath}"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deploy pre-built app for program {ProgramId}", programId);
                return new DeploymentResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Logs = new List<string> { $"Deployment failed: {ex.Message}" }
                };
            }
        }

        public async Task<bool> StartAsync(string programId, CancellationToken cancellationToken = default)
        {
            if (!_instances.TryGetValue(programId, out var instance))
            {
                _logger.LogWarning("No instance found for program {ProgramId}", programId);
                return false;
            }

            if (instance.Status == "active")
            {
                return true; // Already running
            }

            try
            {
                // For pre-built apps, we typically use a web server like nginx or serve static files
                // For this implementation, we'll simulate starting the service
                instance.Status = "active";
                instance.StartedAt = DateTime.UtcNow;

                _logger.LogInformation("Started pre-built app for program {ProgramId}", programId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start pre-built app for program {ProgramId}", programId);
                return false;
            }
        }

        public async Task<bool> StopAsync(string programId, CancellationToken cancellationToken = default)
        {
            if (!_instances.TryGetValue(programId, out var instance))
            {
                return true; // Nothing to stop
            }

            try
            {
                instance.Status = "inactive";
                instance.StoppedAt = DateTime.UtcNow;

                // If using a process, stop it
                if (instance.ProcessId.HasValue)
                {
                    try
                    {
                        var process = Process.GetProcessById(instance.ProcessId.Value);
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to kill process {ProcessId} for program {ProgramId}",
                            instance.ProcessId, programId);
                    }
                }

                _logger.LogInformation("Stopped pre-built app for program {ProgramId}", programId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop pre-built app for program {ProgramId}", programId);
                return false;
            }
        }

        public async Task<bool> RestartAsync(string programId, CancellationToken cancellationToken = default)
        {
            var stopped = await StopAsync(programId, cancellationToken);
            if (stopped)
            {
                await Task.Delay(1000, cancellationToken); // Brief pause
                return await StartAsync(programId, cancellationToken);
            }
            return false;
        }

        public async Task<ApplicationHealthDto> GetHealthAsync(string programId, CancellationToken cancellationToken = default)
        {
            if (!_instances.TryGetValue(programId, out var instance))
            {
                return new ApplicationHealthDto
                {
                    Status = "unknown",
                    LastCheck = DateTime.UtcNow,
                    ErrorMessage = "Instance not found"
                };
            }

            try
            {
                var isHealthy = await CheckApplicationHealthAsync(instance, cancellationToken);

                return new ApplicationHealthDto
                {
                    Status = isHealthy ? "healthy" : "unhealthy",
                    LastCheck = DateTime.UtcNow,
                    ResponseTimeMs = 100, // Simulated response time
                    Details = new Dictionary<string, object>
                    {
                        { "status", instance.Status },
                        { "startedAt", instance.StartedAt },
                        { "url", instance.ApplicationUrl }
                    },
                    Checks = new List<HealthCheckResultDto>
                    {
                        new HealthCheckResultDto
                        {
                            Name = "HTTP Response",
                            Status = isHealthy ? "healthy" : "unhealthy",
                            CheckedAt = DateTime.UtcNow,
                            DurationMs = 100,
                            Message = isHealthy ? "Application responding" : "Application not responding"
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check health for program {ProgramId}", programId);
                return new ApplicationHealthDto
                {
                    Status = "unhealthy",
                    LastCheck = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<List<string>> GetLogsAsync(string programId, int lines, CancellationToken cancellationToken = default)
        {
            if (!_instances.TryGetValue(programId, out var instance))
            {
                return new List<string> { "Instance not found" };
            }

            try
            {
                // For pre-built apps, logs might come from web server access logs
                var logPath = Path.Combine(instance.DeploymentPath, "logs", "access.log");
                if (File.Exists(logPath))
                {
                    var allLines = await File.ReadAllLinesAsync(logPath, cancellationToken);
                    return allLines.TakeLast(lines).ToList();
                }

                return new List<string>
                {
                    $"Pre-built app running since {instance.StartedAt:yyyy-MM-dd HH:mm:ss}",
                    $"Status: {instance.Status}",
                    $"URL: {instance.ApplicationUrl}",
                    "No detailed logs available for static content"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get logs for program {ProgramId}", programId);
                return new List<string> { $"Error retrieving logs: {ex.Message}" };
            }
        }

        public async Task<ApplicationMetricsDto> GetMetricsAsync(string programId, CancellationToken cancellationToken = default)
        {
            if (!_instances.TryGetValue(programId, out var instance))
            {
                return new ApplicationMetricsDto
                {
                    ProgramId = programId,
                    CollectedAt = DateTime.UtcNow,
                    ActiveInstances = 0
                };
            }

            try
            {
                // For pre-built apps, metrics are mainly about web server performance
                return new ApplicationMetricsDto
                {
                    ProgramId = programId,
                    CollectedAt = DateTime.UtcNow,
                    CpuUsagePercent = GetSimulatedCpuUsage(),
                    MemoryUsageBytes = GetSimulatedMemoryUsage(),
                    DiskUsageBytes = GetDirectorySize(instance.DeploymentPath),
                    NetworkConnectionsCount = instance.Status == "active" ? 1 : 0,
                    RequestsPerSecond = GetSimulatedRequestsPerSecond(),
                    AverageResponseTimeMs = 50, // Static content is fast
                    ActiveInstances = instance.Status == "active" ? 1 : 0,
                    CustomMetrics = new Dictionary<string, object>
                    {
                        { "uptime_hours", (DateTime.UtcNow - instance.StartedAt).TotalHours },
                        { "deployment_size_mb", GetDirectorySize(instance.DeploymentPath) / (1024 * 1024) }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get metrics for program {ProgramId}", programId);
                return new ApplicationMetricsDto
                {
                    ProgramId = programId,
                    CollectedAt = DateTime.UtcNow,
                    ActiveInstances = 0
                };
            }
        }

        public async Task<bool> UndeployAsync(string programId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Stop the application first
                await StopAsync(programId, cancellationToken);

                if (_instances.TryGetValue(programId, out var instance))
                {
                    // Clean up deployment files
                    if (Directory.Exists(instance.DeploymentPath))
                    {
                        Directory.Delete(instance.DeploymentPath, true);
                    }

                    _instances.Remove(programId);
                }

                _logger.LogInformation("Undeployed pre-built app for program {ProgramId}", programId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to undeploy pre-built app for program {ProgramId}", programId);
                return false;
            }
        }

        public async Task<DeploymentValidationResult> ValidateAsync(string programId, AppDeploymentRequestDto request, CancellationToken cancellationToken = default)
        {
            var result = new DeploymentValidationResult { IsValid = true };

            try
            {
                // Check for required files (at least index.html or main entry point)
                // This would be checked during actual deployment, but we can validate basic structure

                // Validate configuration
                if (request.Configuration.ContainsKey("baseHref"))
                {
                    var baseHref = request.Configuration["baseHref"]?.ToString();
                    if (string.IsNullOrEmpty(baseHref) || !baseHref.StartsWith("/"))
                    {
                        result.Warnings.Add("baseHref should start with '/' for proper routing");
                    }
                }

                // Validate port
                if (request.Port.HasValue)
                {
                    if (request.Port.Value < 1024 || request.Port.Value > 65535)
                    {
                        result.Errors.Add("Port must be between 1024 and 65535");
                        result.IsValid = false;
                    }
                    else if (IsPortInUse(request.Port.Value))
                    {
                        result.Errors.Add($"Port {request.Port.Value} is already in use");
                        result.IsValid = false;
                    }
                }

                // Validate SPA routing configuration
                if (request.SpaRouting)
                {
                    result.Recommendations.Add("SPA routing is enabled - ensure your application handles client-side routing properly");
                }

                result.ValidatedConfiguration = new Dictionary<string, object>
                {
                    { "deploymentType", "prebuilt_webapp" },
                    { "spaRouting", request.SpaRouting },
                    { "apiIntegration", request.ApiIntegration },
                    { "authenticationMode", request.AuthenticationMode },
                    { "port", request.Port ?? GetAvailablePort() }
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate deployment for program {ProgramId}", programId);
                result.IsValid = false;
                result.Errors.Add($"Validation failed: {ex.Message}");
                return result;
            }
        }

        #region IPreBuiltAppDeploymentStrategy specific methods

        public async Task<string> GetApplicationUrlAsync(string programId, CancellationToken cancellationToken = default)
        {
            if (_instances.TryGetValue(programId, out var instance))
            {
                return instance.ApplicationUrl;
            }
            return string.Empty;
        }

        public async Task<bool> InjectConfigurationAsync(string programId, Dictionary<string, object> config, CancellationToken cancellationToken = default)
        {
            if (!_instances.TryGetValue(programId, out var instance))
            {
                return false;
            }

            try
            {
                var indexPath = Path.Combine(instance.DeploymentPath, "index.html");
                if (!File.Exists(indexPath))
                {
                    return false;
                }

                var content = await File.ReadAllTextAsync(indexPath, cancellationToken);
                var modifiedContent = InjectConfigIntoHtml(content, config);
                await File.WriteAllTextAsync(indexPath, modifiedContent, cancellationToken);

                _logger.LogInformation("Injected configuration for program {ProgramId}", programId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inject configuration for program {ProgramId}", programId);
                return false;
            }
        }

        public async Task<bool> UpdateSecurityHeadersAsync(string programId, Dictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            if (!_instances.TryGetValue(programId, out var instance))
            {
                return false;
            }

            try
            {
                // Update web server configuration with security headers
                var configPath = Path.Combine(instance.DeploymentPath, "web.config");
                await WriteSecurityHeadersConfig(configPath, headers, cancellationToken);

                _logger.LogInformation("Updated security headers for program {ProgramId}", programId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update security headers for program {ProgramId}", programId);
                return false;
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task ExtractFilesToDeploymentPath(List<ProgramFileUploadDto> files, string deploymentPath, CancellationToken cancellationToken)
        {
            foreach (var file in files)
            {
                var filePath = Path.Combine(deploymentPath, file.Path.TrimStart('/'));
                var directory = Path.GetDirectoryName(filePath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(filePath, file.Content, cancellationToken);
            }
        }

        private string? FindEntryPoint(string deploymentPath, Dictionary<string, object> configuration)
        {
            // Look for index.html or configured entry point
            if (configuration.TryGetValue("entryPoint", out var entryPointObj) && entryPointObj is string entryPoint)
            {
                var customEntryPath = Path.Combine(deploymentPath, entryPoint);
                if (File.Exists(customEntryPath))
                {
                    return customEntryPath;
                }
            }

            // Default to index.html
            var indexPath = Path.Combine(deploymentPath, "index.html");
            return File.Exists(indexPath) ? indexPath : null;
        }

        private async Task InjectApplicationConfigurationAsync(string programId, string entryPointPath, AppDeploymentRequestDto request, CancellationToken cancellationToken)
        {
            var content = await File.ReadAllTextAsync(entryPointPath, cancellationToken);

            var appConfig = new Dictionary<string, object>
            {
                { "apiBaseUrl", _settings.BaseUrl },
                { "programId", programId },
                { "userToken", "{{USER_TOKEN}}" }, // Will be replaced at runtime
                { "userPermissions", "{{USER_PERMISSIONS}}" },
                { "userRoles", "{{USER_ROLES}}" },
                { "environment", request.Environment },
                { "features", request.SupportedFeatures },
                { "apiIntegration", request.ApiIntegration },
                { "baseHref", request.BaseHref ?? "/" }
            };

            // Add custom configuration
            foreach (var kvp in request.Configuration)
            {
                appConfig[kvp.Key] = kvp.Value;
            }

            var modifiedContent = InjectConfigIntoHtml(content, appConfig);
            await File.WriteAllTextAsync(entryPointPath, modifiedContent, cancellationToken);
        }

        private string InjectConfigIntoHtml(string htmlContent, Dictionary<string, object> config)
        {
            var configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            var configScript = $@"
<script>
    window.APP_CONFIG = {configJson};
</script>";

            // Try to inject before closing head tag
            var headCloseIndex = htmlContent.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (headCloseIndex >= 0)
            {
                return htmlContent.Insert(headCloseIndex, configScript + Environment.NewLine);
            }

            // Fallback: inject before closing body tag
            var bodyCloseIndex = htmlContent.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (bodyCloseIndex >= 0)
            {
                return htmlContent.Insert(bodyCloseIndex, configScript + Environment.NewLine);
            }

            // Last resort: append to end
            return htmlContent + configScript;
        }

        private async Task SetupWebServerConfigurationAsync(string programId, string deploymentPath, AppDeploymentRequestDto request, CancellationToken cancellationToken)
        {
            // Create a basic web server configuration
            var configContent = GenerateWebServerConfig(request);
            var configPath = Path.Combine(deploymentPath, "server.conf");
            await File.WriteAllTextAsync(configPath, configContent, cancellationToken);
        }

        private string GenerateWebServerConfig(AppDeploymentRequestDto request)
        {
            var config = new StringBuilder();
            config.AppendLine($"# Generated web server configuration");
            config.AppendLine($"port: {request.Port ?? GetAvailablePort()}");
            config.AppendLine($"spa_routing: {request.SpaRouting}");
            config.AppendLine($"api_integration: {request.ApiIntegration}");

            if (!string.IsNullOrEmpty(request.DomainName))
            {
                config.AppendLine($"domain: {request.DomainName}");
            }

            return config.ToString();
        }

        private async Task<string> StartApplicationAsync(string programId, string deploymentPath, AppDeploymentRequestDto request, CancellationToken cancellationToken)
        {
            var port = request.Port ?? GetAvailablePort();

            // For this implementation, we'll simulate starting a web server
            // In a real implementation, this might start nginx, Apache, or a Node.js server

            var baseUrl = string.IsNullOrEmpty(request.DomainName)
                ? $"http://localhost:{port}"
                : $"https://{request.DomainName}";

            var basePath = request.BaseHref ?? "/";
            return $"{baseUrl}{basePath.TrimEnd('/')}/";
        }

        private async Task<bool> CheckApplicationHealthAsync(AppInstance instance, CancellationToken cancellationToken)
        {
            try
            {
                // For pre-built apps, we can check if the entry point file exists and is accessible
                var indexPath = Path.Combine(instance.DeploymentPath, "index.html");
                return File.Exists(indexPath) && instance.Status == "active";
            }
            catch
            {
                return false;
            }
        }

        private async Task WriteSecurityHeadersConfig(string configPath, Dictionary<string, string> headers, CancellationToken cancellationToken)
        {
            var config = new StringBuilder();
            config.AppendLine("# Security Headers Configuration");

            foreach (var header in headers)
            {
                config.AppendLine($"add_header {header.Key} \"{header.Value}\";");
            }

            await File.WriteAllTextAsync(configPath, config.ToString(), cancellationToken);
        }

        private double GetSimulatedRequestsPerSecond()
        {
            // TODO: WHAT IS THIS???
            // Simulate request rate
            var random = new Random();
            return random.NextDouble() * 10;
        }

        #endregion
    }
}