using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Interfaces.Deployment;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations.Deployment
{
    public class ContainerDeploymentStrategy : BaseDeploymentStrategy, IContainerDeploymentStrategy
    {
        public AppDeploymentType SupportedType => AppDeploymentType.DockerContainer;

        public ContainerDeploymentStrategy(
            ILogger<PreBuiltAppDeploymentStrategy> logger,
            IOptions<DeploymentSettings> settings) : base(logger, settings) { }

        public Task<string> BuildImageAsync(string programId, ContainerDeploymentRequestDto request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DeploymentResult> DeployAsync(string programId, AppDeploymentRequestDto request, List<ProgramFileUploadDto> files, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ApplicationHealthDto> GetHealthAsync(string programId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<List<ContainerInstanceDto>> GetInstancesAsync(string programId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> GetLogsAsync(string programId, int lines, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ApplicationMetricsDto> GetMetricsAsync(string programId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RestartAsync(string programId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ScaleAsync(string programId, int replicas, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> StartAsync(string programId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> StopAsync(string programId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UndeployAsync(string programId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateResourceLimitsAsync(string programId, ContainerResourceLimitsDto limits, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DeploymentValidationResult> ValidateAsync(string programId, AppDeploymentRequestDto request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
