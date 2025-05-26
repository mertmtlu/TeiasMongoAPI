using Microsoft.Extensions.Options;
using TeiasMongoAPI.Services.Services.Implementations.Deployment;
using TeiasMongoAPI.Services.Services.Implementations;
using Microsoft.Extensions.Logging;

namespace TeiasMongoAPI.Services.Services.Base
{
    public abstract class BaseDeploymentStrategy
    {
        protected readonly ILogger _logger;
        protected readonly DeploymentSettings _settings;
        protected readonly Dictionary<string, AppInstance> _instances = new();

        protected BaseDeploymentStrategy(ILogger logger, IOptions<DeploymentSettings> settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        // Common helper methods
        protected int GetAvailablePort()
        {
            // Simple port allocation - in production, use a proper port manager
            var random = new Random();
            return random.Next(8000, 9000);
        }

        protected bool IsPortInUse(int port)
        {
            // Check if port is already in use by existing instances
            return _instances.Values.Any(i => i.Port == port && i.Status == "active");
        }

        protected long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path))
                return 0;

            try
            {
                return new DirectoryInfo(path)
                    .GetFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length);
            }
            catch
            {
                return 0;
            }
        }

        protected string GetDeploymentPath(string programId) { return string.Empty; } // TODO:

        protected AppInstance? GetInstance(string programId)
        {
            _instances.TryGetValue(programId, out var instance);
            return instance;
        }
        protected void RegisterInstance(string programId, AppInstance instance)
        {
            _instances.Add(programId, instance);
        }

        protected double GetSimulatedCpuUsage()
        {
            // TODO: WHAT IS THIS???
            // Simulate low CPU usage for static content
            var random = new Random();
            return random.NextDouble() * 10; // 0-10% CPU usage
        }

        protected long GetSimulatedMemoryUsage()
        {
            // TODO: WHAT IS THIS???
            // Simulate memory usage (50-100 MB for static content)
            var random = new Random();
            return (long)(random.NextDouble() * 50 + 50) * 1024 * 1024;
        }
    }
}
