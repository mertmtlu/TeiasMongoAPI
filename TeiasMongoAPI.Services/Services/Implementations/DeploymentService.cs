using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Interfaces.Deployment;
using AutoMapper;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class DeploymentService : BaseService, IDeploymentService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly DeploymentSettings _settings;
        private readonly Dictionary<AppDeploymentType, IDeploymentStrategy> _strategies;

        public DeploymentService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<DeploymentService> logger,
            IFileStorageService fileStorageService,
            IOptions<DeploymentSettings> settings,
            IEnumerable<IDeploymentStrategy> strategies) : base(unitOfWork, mapper, logger) 
        {
            _fileStorageService = fileStorageService;
            _settings = settings.Value;
            _strategies = strategies.ToDictionary(s => s.SupportedType, s => s);
        }

        public async Task<ProgramDeploymentDto> DeployPreBuiltAppAsync(string programId, AppDeploymentRequestDto request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting pre-built app deployment for program {ProgramId}", programId);

            var program = await GetProgramAsync(programId, cancellationToken);

            // Get deployment strategy
            if (!_strategies.TryGetValue(AppDeploymentType.PreBuiltWebApp, out var strategy))
            {
                throw new NotSupportedException("Pre-built web app deployment is not supported");
            }

            // Get program files
            var files = await GetProgramFilesForDeployment(programId, cancellationToken);

            // Deploy using strategy
            var result = await strategy.DeployAsync(programId, request, files, cancellationToken);

            if (!result.Success)
            {
                throw new InvalidOperationException($"Deployment failed: {result.ErrorMessage}");
            }

            // Update program deployment info
            await UpdateProgramDeploymentInfo(program, AppDeploymentType.PreBuiltWebApp, request, result, cancellationToken);

            return new ProgramDeploymentDto
            {
                Id = result.DeploymentId ?? Guid.NewGuid().ToString(),
                DeploymentType = AppDeploymentType.PreBuiltWebApp,
                Status = "active",
                LastDeployed = result.DeployedAt,
                Configuration = request.Configuration,
                SupportedFeatures = request.SupportedFeatures,
                ApplicationUrl = result.ApplicationUrl,
                Logs = result.Logs
            };
        }

        public async Task<ProgramDeploymentDto> DeployStaticSiteAsync(string programId, StaticSiteDeploymentRequestDto request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting static site deployment for program {ProgramId}", programId);

            var program = await GetProgramAsync(programId, cancellationToken);

            // Get deployment strategy
            if (!_strategies.TryGetValue(AppDeploymentType.StaticSite, out var strategy))
            {
                throw new NotSupportedException("Static site deployment is not supported");
            }

            // Get program files
            var files = await GetProgramFilesForDeployment(programId, cancellationToken);

            // Deploy using strategy
            var result = await strategy.DeployAsync(programId, request, files, cancellationToken);

            if (!result.Success)
            {
                throw new InvalidOperationException($"Deployment failed: {result.ErrorMessage}");
            }

            // Update program deployment info
            await UpdateProgramDeploymentInfo(program, AppDeploymentType.StaticSite, request, result, cancellationToken);

            return new ProgramDeploymentDto
            {
                Id = result.DeploymentId ?? Guid.NewGuid().ToString(),
                DeploymentType = AppDeploymentType.StaticSite,
                Status = "active",
                LastDeployed = result.DeployedAt,
                Configuration = request.Configuration,
                SupportedFeatures = request.SupportedFeatures,
                ApplicationUrl = result.ApplicationUrl,
                Logs = result.Logs
            };
        }

        public async Task<ProgramDeploymentDto> DeployContainerAppAsync(string programId, ContainerDeploymentRequestDto request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting container app deployment for program {ProgramId}", programId);

            var program = await GetProgramAsync(programId, cancellationToken);

            // Get deployment strategy
            if (!_strategies.TryGetValue(AppDeploymentType.DockerContainer, out var strategy))
            {
                throw new NotSupportedException("Container app deployment is not supported");
            }

            // Get program files
            var files = await GetProgramFilesForDeployment(programId, cancellationToken);

            // Deploy using strategy
            var result = await strategy.DeployAsync(programId, request, files, cancellationToken);

            if (!result.Success)
            {
                throw new InvalidOperationException($"Deployment failed: {result.ErrorMessage}");
            }

            // Update program deployment info
            await UpdateProgramDeploymentInfo(program, AppDeploymentType.DockerContainer, request, result, cancellationToken);

            return new ProgramDeploymentDto
            {
                Id = result.DeploymentId ?? Guid.NewGuid().ToString(),
                DeploymentType = AppDeploymentType.DockerContainer,
                Status = "active",
                LastDeployed = result.DeployedAt,
                Configuration = request.Configuration,
                SupportedFeatures = request.SupportedFeatures,
                ApplicationUrl = result.ApplicationUrl,
                Logs = result.Logs
            };
        }

        public async Task<ProgramDeploymentStatusDto> GetDeploymentStatusAsync(string programId, CancellationToken cancellationToken = default)
        {
            var program = await GetProgramAsync(programId, cancellationToken);

            if (program.DeploymentInfo == null)
            {
                return new ProgramDeploymentStatusDto
                {
                    DeploymentType = AppDeploymentType.SourceCode,
                    Status = "not_deployed",
                    IsHealthy = false,
                    LastHealthCheck = DateTime.UtcNow,
                    RecentLogs = new List<string>()
                };
            }

            // Get strategy and check health
            if (_strategies.TryGetValue(program.DeploymentInfo.DeploymentType, out var strategy))
            {
                var health = await strategy.GetHealthAsync(programId, cancellationToken);
                var logs = await strategy.GetLogsAsync(programId, 10, cancellationToken);

                return new ProgramDeploymentStatusDto
                {
                    DeploymentType = program.DeploymentInfo.DeploymentType,
                    Status = program.DeploymentInfo.Status,
                    LastDeployed = program.DeploymentInfo.LastDeployed,
                    ApplicationUrl = GetApplicationUrl(programId, program.DeploymentInfo.DeploymentType),
                    IsHealthy = health.Status == "healthy",
                    LastHealthCheck = health.LastCheck,
                    RecentLogs = logs
                };
            }

            return new ProgramDeploymentStatusDto
            {
                DeploymentType = program.DeploymentInfo.DeploymentType,
                Status = program.DeploymentInfo.Status,
                LastDeployed = program.DeploymentInfo.LastDeployed,
                IsHealthy = false,
                LastHealthCheck = DateTime.UtcNow,
                RecentLogs = new List<string>()
            };
        }

        public async Task<bool> StartApplicationAsync(string programId, CancellationToken cancellationToken = default)
        {
            var program = await GetProgramAsync(programId, cancellationToken);

            if (program.DeploymentInfo == null)
            {
                throw new InvalidOperationException("Program is not deployed");
            }

            if (_strategies.TryGetValue(program.DeploymentInfo.DeploymentType, out var strategy))
            {
                var success = await strategy.StartAsync(programId, cancellationToken);

                if (success)
                {
                    program.DeploymentInfo.Status = "active";
                    await _unitOfWork.Programs.UpdateAsync(program._ID, program, cancellationToken);
                    _logger.LogInformation("Started application for program {ProgramId}", programId);
                }

                return success;
            }

            return false;
        }

        public async Task<bool> StopApplicationAsync(string programId, CancellationToken cancellationToken = default)
        {
            var program = await GetProgramAsync(programId, cancellationToken);

            if (program.DeploymentInfo == null)
            {
                throw new InvalidOperationException("Program is not deployed");
            }

            if (_strategies.TryGetValue(program.DeploymentInfo.DeploymentType, out var strategy))
            {
                var success = await strategy.StopAsync(programId, cancellationToken);

                if (success)
                {
                    program.DeploymentInfo.Status = "inactive";
                    await _unitOfWork.Programs.UpdateAsync(program._ID, program, cancellationToken);
                    _logger.LogInformation("Stopped application for program {ProgramId}", programId);
                }

                return success;
            }

            return false;
        }

        public async Task<bool> RestartApplicationAsync(string programId, CancellationToken cancellationToken = default)
        {
            var program = await GetProgramAsync(programId, cancellationToken);

            if (program.DeploymentInfo == null)
            {
                throw new InvalidOperationException("Program is not deployed");
            }

            if (_strategies.TryGetValue(program.DeploymentInfo.DeploymentType, out var strategy))
            {
                var success = await strategy.RestartAsync(programId, cancellationToken);

                if (success)
                {
                    program.DeploymentInfo.Status = "active";
                    await _unitOfWork.Programs.UpdateAsync(program._ID, program, cancellationToken);
                    _logger.LogInformation("Restarted application for program {ProgramId}", programId);
                }

                return success;
            }

            return false;
        }

        public async Task<List<string>> GetApplicationLogsAsync(string programId, int lines = 100, CancellationToken cancellationToken = default)
        {
            var program = await GetProgramAsync(programId, cancellationToken);

            if (program.DeploymentInfo == null)
            {
                return new List<string> { "Program is not deployed" };
            }

            if (_strategies.TryGetValue(program.DeploymentInfo.DeploymentType, out var strategy))
            {
                return await strategy.GetLogsAsync(programId, lines, cancellationToken);
            }

            return new List<string> { "Deployment strategy not found" };
        }

        public async Task<ProgramDto> UpdateDeploymentConfigAsync(string programId, AppDeploymentConfigUpdateDto config, CancellationToken cancellationToken = default)
        {
            var program = await GetProgramAsync(programId, cancellationToken);

            if (program.DeploymentInfo == null)
            {
                throw new InvalidOperationException("Program is not deployed");
            }

            // Update deployment configuration
            foreach (var kvp in config.Configuration)
            {
                program.DeploymentInfo.Configuration[kvp.Key] = kvp.Value;
            }

            // Update supported features
            if (config.SupportedFeatures.Any())
            {
                program.DeploymentInfo.SupportedFeatures = config.SupportedFeatures;
            }

            await _unitOfWork.Programs.UpdateAsync(program._ID, program, cancellationToken);

            _logger.LogInformation("Updated deployment configuration for program {ProgramId}", programId);

            return _mapper.Map<ProgramDto>(program);
        }

        public async Task<ApplicationHealthDto> GetApplicationHealthAsync(string programId, CancellationToken cancellationToken = default)
        {
            var program = await GetProgramAsync(programId, cancellationToken);

            if (program.DeploymentInfo == null)
            {
                return new ApplicationHealthDto
                {
                    Status = "unknown",
                    LastCheck = DateTime.UtcNow,
                    ErrorMessage = "Program is not deployed"
                };
            }

            if (_strategies.TryGetValue(program.DeploymentInfo.DeploymentType, out var strategy))
            {
                return await strategy.GetHealthAsync(programId, cancellationToken);
            }

            return new ApplicationHealthDto
            {
                Status = "unknown",
                LastCheck = DateTime.UtcNow,
                ErrorMessage = "Deployment strategy not found"
            };
        }

        public async Task<bool> ScaleApplicationAsync(string programId, int instances, CancellationToken cancellationToken = default)
        {
            var program = await GetProgramAsync(programId, cancellationToken);

            if (program.DeploymentInfo == null)
            {
                throw new InvalidOperationException("Program is not deployed");
            }

            if (program.DeploymentInfo.DeploymentType != AppDeploymentType.DockerContainer)
            {
                throw new InvalidOperationException("Scaling is only supported for container deployments");
            }

            if (_strategies.TryGetValue(AppDeploymentType.DockerContainer, out var strategy) &&
                strategy is IContainerDeploymentStrategy containerStrategy)
            {
                var success = await containerStrategy.ScaleAsync(programId, instances, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Scaled application for program {ProgramId} to {Instances} instances", programId, instances);
                }

                return success;
            }

            return false;
        }

        public async Task<ApplicationMetricsDto> GetApplicationMetricsAsync(string programId, CancellationToken cancellationToken = default)
        {
            var program = await GetProgramAsync(programId, cancellationToken);

            if (program.DeploymentInfo == null)
            {
                return new ApplicationMetricsDto
                {
                    ProgramId = programId,
                    CollectedAt = DateTime.UtcNow,
                    ActiveInstances = 0
                };
            }

            if (_strategies.TryGetValue(program.DeploymentInfo.DeploymentType, out var strategy))
            {
                return await strategy.GetMetricsAsync(programId, cancellationToken);
            }

            return new ApplicationMetricsDto
            {
                ProgramId = programId,
                CollectedAt = DateTime.UtcNow,
                ActiveInstances = 0
            };
        }

        public async Task<bool> UndeployApplicationAsync(string programId, CancellationToken cancellationToken = default)
        {
            var program = await GetProgramAsync(programId, cancellationToken);

            if (program.DeploymentInfo == null)
            {
                return true; // Already undeployed
            }

            if (_strategies.TryGetValue(program.DeploymentInfo.DeploymentType, out var strategy))
            {
                var success = await strategy.UndeployAsync(programId, cancellationToken);

                if (success)
                {
                    program.DeploymentInfo = null;
                    await _unitOfWork.Programs.UpdateAsync(program._ID, program, cancellationToken);
                    _logger.LogInformation("Undeployed application for program {ProgramId}", programId);
                }

                return success;
            }

            return false;
        }

        public async Task<DeploymentValidationResult> ValidateDeploymentAsync(string programId, AppDeploymentRequestDto request, CancellationToken cancellationToken = default)
        {
            var program = await GetProgramAsync(programId, cancellationToken);

            // Basic validation
            var result = new DeploymentValidationResult { IsValid = true };

            // Check if deployment type is supported
            if (!_strategies.ContainsKey(request.DeploymentType))
            {
                result.IsValid = false;
                result.Errors.Add($"Deployment type {request.DeploymentType} is not supported");
                return result;
            }

            // Check if program has files
            var files = await _fileStorageService.ListProgramFilesAsync(programId, null, cancellationToken);
            if (!files.Any())
            {
                result.IsValid = false;
                result.Errors.Add("Program has no files to deploy");
                return result;
            }

            // Validate using specific strategy
            var strategy = _strategies[request.DeploymentType];
            var strategyValidation = await strategy.ValidateAsync(programId, request, cancellationToken);

            result.IsValid = result.IsValid && strategyValidation.IsValid;
            result.Errors.AddRange(strategyValidation.Errors);
            result.Warnings.AddRange(strategyValidation.Warnings);
            result.Recommendations.AddRange(strategyValidation.Recommendations);

            foreach (var kvp in strategyValidation.ValidatedConfiguration)
            {
                result.ValidatedConfiguration[kvp.Key] = kvp.Value;
            }

            return result;
        }

        public async Task<List<SupportedDeploymentOptionDto>> GetSupportedDeploymentOptionsAsync(string programId, CancellationToken cancellationToken = default)
        {
            var program = await GetProgramAsync(programId, cancellationToken);
            var options = new List<SupportedDeploymentOptionDto>();

            // Get program files to determine what deployment types are suitable
            var files = await _fileStorageService.ListProgramFilesAsync(programId, null, cancellationToken);
            var fileExtensions = files.Select(f => Path.GetExtension(f.FilePath).ToLowerInvariant()).Distinct().ToList();

            // Check each supported deployment type
            foreach (var strategy in _strategies.Values)
            {
                var option = new SupportedDeploymentOptionDto
                {
                    DeploymentType = strategy.SupportedType,
                    Name = GetDeploymentTypeName(strategy.SupportedType),
                    Description = GetDeploymentTypeDescription(strategy.SupportedType),
                    IsRecommended = IsRecommendedForProgram(strategy.SupportedType, program, fileExtensions),
                    RequiredFeatures = GetRequiredFeatures(strategy.SupportedType),
                    SupportedFeatures = GetSupportedFeatures(strategy.SupportedType),
                    DefaultConfiguration = GetDefaultConfiguration(strategy.SupportedType)
                };

                options.Add(option);
            }

            return options.OrderByDescending(o => o.IsRecommended).ToList();
        }

        #region Private Helper Methods

        private async Task<Program> GetProgramAsync(string programId, CancellationToken cancellationToken)
        {
            var objectId = MongoDB.Bson.ObjectId.Parse(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found");
            }

            return program;
        }

        private async Task<List<ProgramFileUploadDto>> GetProgramFilesForDeployment(string programId, CancellationToken cancellationToken)
        {
            var files = await _fileStorageService.ListProgramFilesAsync(programId, null, cancellationToken);
            var uploadFiles = new List<ProgramFileUploadDto>();

            foreach (var file in files)
            {
                try
                {
                    var content = await _fileStorageService.GetFileContentAsync(file.StorageKey, cancellationToken);
                    uploadFiles.Add(new ProgramFileUploadDto
                    {
                        Path = file.FilePath,
                        Content = content,
                        ContentType = file.ContentType,
                        Description = $"Deployment file for {file.FilePath}"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get file {FilePath} for deployment", file.FilePath);
                }
            }

            return uploadFiles;
        }

        private async Task UpdateProgramDeploymentInfo(Program program, AppDeploymentType deploymentType, AppDeploymentRequestDto request, DeploymentResult result, CancellationToken cancellationToken)
        {
            program.DeploymentInfo = new AppDeploymentInfo
            {
                DeploymentType = deploymentType,
                Configuration = request.Configuration,
                LastDeployed = result.DeployedAt,
                Status = "active",
                SupportedFeatures = request.SupportedFeatures
            };

            await _unitOfWork.Programs.UpdateAsync(program._ID, program, cancellationToken);
        }

        private string GetApplicationUrl(string programId, AppDeploymentType deploymentType)
        {
            return deploymentType switch
            {
                AppDeploymentType.PreBuiltWebApp => $"{_settings.BaseUrl}/programs/{programId}/app/",
                AppDeploymentType.StaticSite => $"{_settings.BaseUrl}/programs/{programId}/site/",
                AppDeploymentType.DockerContainer => $"{_settings.BaseUrl}/programs/{programId}/container/",
                _ => string.Empty
            };
        }

        private string GetDeploymentTypeName(AppDeploymentType type)
        {
            return type switch
            {
                AppDeploymentType.PreBuiltWebApp => "Pre-built Web Application",
                AppDeploymentType.StaticSite => "Static Site",
                AppDeploymentType.DockerContainer => "Container Application",
                AppDeploymentType.MicroFrontend => "Micro Frontend",
                _ => "Source Code Execution"
            };
        }

        private string GetDeploymentTypeDescription(AppDeploymentType type)
        {
            return type switch
            {
                AppDeploymentType.PreBuiltWebApp => "Deploy pre-built Angular, React, Vue, or other SPA applications",
                AppDeploymentType.StaticSite => "Deploy static HTML, CSS, and JavaScript sites",
                AppDeploymentType.DockerContainer => "Deploy applications using Docker containers",
                AppDeploymentType.MicroFrontend => "Deploy micro frontend components",
                _ => "Execute source code on demand"
            };
        }

        private bool IsRecommendedForProgram(AppDeploymentType type, Program program, List<string> fileExtensions)
        {
            return type switch
            {
                AppDeploymentType.PreBuiltWebApp when program.Language is "angular" or "react" or "vue" => true,
                AppDeploymentType.StaticSite when fileExtensions.Contains(".html") => true,
                AppDeploymentType.DockerContainer when fileExtensions.Contains(".dockerfile") ||
                                                          program.Language is "docker" => true,
                _ => false
            };
        }

        private List<string> GetRequiredFeatures(AppDeploymentType type)
        {
            return type switch
            {
                AppDeploymentType.PreBuiltWebApp => new List<string> { "web_server", "static_files" },
                AppDeploymentType.StaticSite => new List<string> { "web_server", "static_files" },
                AppDeploymentType.DockerContainer => new List<string> { "docker_runtime", "networking" },
                _ => new List<string>()
            };
        }

        private List<string> GetSupportedFeatures(AppDeploymentType type)
        {
            return type switch
            {
                AppDeploymentType.PreBuiltWebApp => new List<string>
                {
                    "spa_routing", "api_integration", "authentication", "custom_headers", "ssl_termination"
                },
                AppDeploymentType.StaticSite => new List<string>
                {
                    "caching", "compression", "custom_headers", "ssl_termination", "cdn"
                },
                AppDeploymentType.DockerContainer => new List<string>
                {
                    "scaling", "health_checks", "resource_limits", "networking", "volumes", "environment_variables"
                },
                _ => new List<string>()
            };
        }

        private Dictionary<string, object> GetDefaultConfiguration(AppDeploymentType type)
        {
            return type switch
            {
                AppDeploymentType.PreBuiltWebApp => new Dictionary<string, object>
                {
                    { "spaRouting", true },
                    { "apiIntegration", true },
                    { "authenticationMode", "jwt_injection" },
                    { "autoStart", true }
                },
                AppDeploymentType.StaticSite => new Dictionary<string, object>
                {
                    { "cachingStrategy", "aggressive" },
                    { "compression", true },
                    { "autoStart", true }
                },
                AppDeploymentType.DockerContainer => new Dictionary<string, object>
                {
                    { "replicas", 1 },
                    { "autoRestart", true },
                    { "healthCheckEnabled", true },
                    { "resourceLimits", new { cpu = "0.5", memory = "512M" } }
                },
                _ => new Dictionary<string, object>()
            };
        }

        #endregion
    }

    public class DeploymentSettings
    {
        public string BaseUrl { get; set; } = "https://localhost:5001";
        public string DeploymentPath { get; set; } = "./deployments";
        public int MaxConcurrentDeployments { get; set; } = 5;
        public int HealthCheckIntervalSeconds { get; set; } = 30;
        public int DeploymentTimeoutMinutes { get; set; } = 30;
        public bool EnableMetricsCollection { get; set; } = true;
        public Dictionary<string, object> DefaultResourceLimits { get; set; } = new();
    }
}