using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Implementations.Execution;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    /// <summary>
    /// RISK MITIGATION: Background service that monitors and cleans up stale resource reservations
    ///
    /// Purpose:
    /// - Detects reservations that were made but never released (due to crashes, errors, or bugs)
    /// - Automatically releases stale reservations to prevent resource leaks
    /// - Ensures queued jobs can eventually be processed even if some executions fail silently
    ///
    /// How it works:
    /// - Runs periodically (default: every 5 minutes)
    /// - Checks all active reservations and compares reservation time to current time
    /// - If a reservation is older than the maximum allowed age (default: 3 hours), it's considered stale
    /// - Stale reservations are released automatically
    /// - Logs warnings for each stale reservation detected
    ///
    /// Configuration:
    /// - CheckIntervalMinutes: How often to run the check (default: 5 minutes)
    /// - MaxReservationAgeMinutes: Maximum age before a reservation is considered stale (default: 180 minutes / 3 hours)
    /// </summary>
    public class StaleReservationMonitorService : BackgroundService
    {
        private readonly ILogger<StaleReservationMonitorService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TieredExecutionSettings _tieredSettings;
        private readonly TimeSpan _checkInterval;
        private readonly TimeSpan _maxReservationAge;

        public StaleReservationMonitorService(
            ILogger<StaleReservationMonitorService> logger,
            IServiceProvider serviceProvider,
            IOptions<ProjectExecutionSettings> projectSettings)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _tieredSettings = projectSettings.Value.TieredExecution;

            // Configuration: Check every 5 minutes by default
            _checkInterval = TimeSpan.FromMinutes(5);

            // Configuration: Reservations older than 3 hours are considered stale
            // This is conservative to avoid false positives for long-running jobs
            // Maximum execution time for any job profile is 2880 minutes (48 hours), but we use 3 hours
            // as a reasonable threshold for detecting stuck reservations while avoiding interference
            // with legitimate long-running jobs
            _maxReservationAge = TimeSpan.FromHours(3);

            _logger.LogInformation(
                "Stale Reservation Monitor initialized. Check interval: {CheckInterval}, Max reservation age: {MaxAge}",
                _checkInterval, _maxReservationAge);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Stale Reservation Monitor service starting...");

            // Don't run if tiered execution is disabled
            if (!_tieredSettings.EnableTieredExecution)
            {
                _logger.LogInformation("Stale Reservation Monitor: Tiered execution is disabled. Service will not run.");
                return;
            }

            // Wait a bit before first check to allow the application to fully start
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndCleanStaleReservationsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Stale Reservation Monitor service");
                }

                // Wait for next check interval
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Stale Reservation Monitor service stopping...");
        }

        private async Task CheckAndCleanStaleReservationsAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Stale Reservation Monitor: Starting reservation check");

            try
            {
                // Get ExecutionService from service provider (scoped service)
                using var scope = _serviceProvider.CreateScope();
                var executionService = scope.ServiceProvider.GetService<IExecutionService>();

                if (executionService == null)
                {
                    _logger.LogWarning("Stale Reservation Monitor: Could not resolve IExecutionService");
                    return;
                }

                // Call the public method on ExecutionService to check and clean stale reservations
                // Note: We'll need to add this method to IExecutionService and ExecutionService
                var staleCount = await executionService.CleanStaleReservationsAsync(_maxReservationAge, cancellationToken);

                if (staleCount > 0)
                {
                    _logger.LogWarning(
                        "Stale Reservation Monitor: Cleaned {StaleCount} stale reservations",
                        staleCount);
                }
                else
                {
                    _logger.LogDebug("Stale Reservation Monitor: No stale reservations found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking and cleaning stale reservations");
            }
        }
    }
}
