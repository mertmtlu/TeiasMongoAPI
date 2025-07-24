using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class QueuedHostedService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<QueuedHostedService> _logger;

        public QueuedHostedService(
            IBackgroundTaskQueue taskQueue,
            IServiceProvider serviceProvider,
            ILogger<QueuedHostedService> logger)
        {
            _taskQueue = taskQueue;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is running.");

            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                    // Create a new scope for each work item to ensure proper DI lifecycle
                    using var scope = _serviceProvider.CreateScope();
                    var scopedServiceProvider = scope.ServiceProvider;

                    try
                    {
                        await workItem(scopedServiceProvider, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred executing background work item.");
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation token is cancelled
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in background processing.");
                    
                    // Wait a bit before retrying to avoid tight error loops
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");

            await base.StopAsync(stoppingToken);
        }
    }
}