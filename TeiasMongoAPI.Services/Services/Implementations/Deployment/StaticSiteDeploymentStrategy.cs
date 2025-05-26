using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Interfaces.Deployment;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations.Deployment
{
    public class StaticSiteDeploymentStrategy : BaseDeploymentStrategy, IStaticSiteDeploymentStrategy
    {

        AppDeploymentType IDeploymentStrategy.SupportedType => AppDeploymentType.StaticSite;

        public StaticSiteDeploymentStrategy(
            ILogger<PreBuiltAppDeploymentStrategy> logger,
            IOptions<DeploymentSettings> settings):base(logger, settings){ }

        public Task<bool> SetupCachingAsync(string programId, string strategy, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> EnableCdnAsync(string programId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateCustomHeadersAsync(string programId, Dictionary<string, string> headers, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DeploymentResult> DeployAsync(string programId, AppDeploymentRequestDto request, List<ProgramFileUploadDto> files, CancellationToken cancellationToken = default)
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

        public Task<bool> RestartAsync(string programId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ApplicationHealthDto> GetHealthAsync(string programId, CancellationToken cancellationToken = default)
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

        public Task<bool> UndeployAsync(string programId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<DeploymentValidationResult> ValidateAsync(string programId, AppDeploymentRequestDto request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
