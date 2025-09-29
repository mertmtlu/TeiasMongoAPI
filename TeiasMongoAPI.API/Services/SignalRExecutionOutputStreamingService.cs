using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using TeiasMongoAPI.API.Hubs;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Services
{
    /// <summary>
    /// SignalR implementation for streaming live execution output in real-time
    /// Eliminates the need for clients to poll execution status every 2 seconds
    /// </summary>
    public class SignalRExecutionOutputStreamingService : IExecutionOutputStreamingService
    {
        private readonly IHubContext<ExecutionHub> _hubContext;
        private readonly ILogger<SignalRExecutionOutputStreamingService> _logger;
        private readonly Dictionary<string, DateTime> _activeStreams = new();
        private readonly SemaphoreSlim _streamingSemaphore = new(1, 1);

        // LOG CACHING: Store recent log lines for each execution to provide historical context
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _logCache = new();
        private const int MaxCacheSize = 200;

        public SignalRExecutionOutputStreamingService(
            IHubContext<ExecutionHub> hubContext,
            ILogger<SignalRExecutionOutputStreamingService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task StartExecutionStreamingAsync(string executionId, string userId, CancellationToken cancellationToken = default)
        {
            await _streamingSemaphore.WaitAsync(cancellationToken);
            try
            {
                _activeStreams[executionId] = DateTime.UtcNow;

                // LOG CACHING: Initialize log cache for this execution
                _logCache[executionId] = new ConcurrentQueue<string>();

                _logger.LogInformation("Started execution streaming for {ExecutionId} by user {UserId}", executionId, userId);

                // Notify execution group that streaming has started
                await _hubContext.Clients.Group($"execution_{executionId}")
                    .SendAsync("ExecutionStreamingStarted", new
                    {
                        executionId,
                        userId,
                        startedAt = DateTime.UtcNow
                    }, cancellationToken);
            }
            finally
            {
                _streamingSemaphore.Release();
            }
        }

        public async Task StopExecutionStreamingAsync(string executionId, CancellationToken cancellationToken = default)
        {
            await _streamingSemaphore.WaitAsync(cancellationToken);
            try
            {
                _activeStreams.Remove(executionId);

                // LOG CACHING: Clean up log cache to prevent memory leaks
                _logCache.TryRemove(executionId, out _);

                _logger.LogInformation("Stopped execution streaming for {ExecutionId}", executionId);

                // Notify execution group that streaming has stopped
                await _hubContext.Clients.Group($"execution_{executionId}")
                    .SendAsync("ExecutionStreamingStopped", new
                    {
                        executionId,
                        stoppedAt = DateTime.UtcNow
                    }, cancellationToken);
            }
            finally
            {
                _streamingSemaphore.Release();
            }
        }

        public async Task StreamExecutionStartedAsync(string executionId, ExecutionStartedEventArgs args, CancellationToken cancellationToken = default)
        {
            try
            {
                var eventData = new
                {
                    type = "execution_started",
                    executionId,
                    programId = args.ProgramId,
                    versionId = args.VersionId,
                    userId = args.UserId,
                    startedAt = args.StartedAt.ToString("O"), // ISO format
                    timeoutMinutes = args.TimeoutMinutes,
                    parameters = args.Parameters,
                    timestamp = DateTime.UtcNow.ToString("O")
                };

                // Send to specific execution group
                await _hubContext.Clients.Group($"execution_{executionId}")
                    .SendAsync("ExecutionStarted", eventData, cancellationToken);

                // Also send to user execution group for dashboard updates
                await _hubContext.Clients.Group($"user_executions_{args.UserId}")
                    .SendAsync("UserExecutionStarted", eventData, cancellationToken);

                _logger.LogDebug("Streamed execution started event for {ExecutionId}", executionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stream execution started event for {ExecutionId}", executionId);
                throw;
            }
        }

        public IEnumerable<string> GetCachedLogs(string executionId)
        {
            if (_logCache.TryGetValue(executionId, out var queue))
            {
                return queue.ToList();
            }
            return Enumerable.Empty<string>();
        }

        public async Task StreamOutputAsync(string executionId, string output, DateTime timestamp, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(output.Trim())) return; // Skip empty output

            try
            {
                var eventData = new
                {
                    type = "stdout",
                    executionId,
                    output,
                    timestamp = timestamp.ToString("O"),
                    receivedAt = DateTime.UtcNow.ToString("O")
                };

                await _hubContext.Clients.Group($"execution_{executionId}")
                    .SendAsync("ExecutionOutput", eventData, cancellationToken);

                // LOG CACHING: Add formatted log line to cache
                if (_logCache.TryGetValue(executionId, out var queue))
                {
                    var formattedLogLine = $"[{timestamp:HH:mm:ss.fff}] [STDOUT] {output}";
                    queue.Enqueue(formattedLogLine);

                    // Cap cache size to prevent memory leaks
                    while (queue.Count > MaxCacheSize)
                    {
                        queue.TryDequeue(out _);
                    }
                }

                _logger.LogTrace("Streamed stdout output for {ExecutionId}: {OutputLength} chars",
                    executionId, output.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stream stdout output for {ExecutionId}", executionId);
                // Don't throw - output streaming failures shouldn't break execution
            }
        }

        public async Task StreamErrorAsync(string executionId, string error, DateTime timestamp, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(error.Trim())) return; // Skip empty error output

            try
            {
                var eventData = new
                {
                    type = "stderr",
                    executionId,
                    error,
                    timestamp = timestamp.ToString("O"),
                    receivedAt = DateTime.UtcNow.ToString("O")
                };

                await _hubContext.Clients.Group($"execution_{executionId}")
                    .SendAsync("ExecutionError", eventData, cancellationToken);

                // LOG CACHING: Add formatted error line to cache
                if (_logCache.TryGetValue(executionId, out var queue))
                {
                    var formattedLogLine = $"[{timestamp:HH:mm:ss.fff}] [STDERR] {error}";
                    queue.Enqueue(formattedLogLine);

                    // Cap cache size
                    while (queue.Count > MaxCacheSize)
                    {
                        queue.TryDequeue(out _);
                    }
                }

                _logger.LogTrace("Streamed stderr output for {ExecutionId}: {ErrorLength} chars",
                    executionId, error.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stream stderr output for {ExecutionId}", executionId);
                // Don't throw - error output streaming failures shouldn't break execution
            }
        }

        public async Task StreamStatusChangeAsync(string executionId, ExecutionStatusChangedEventArgs args, CancellationToken cancellationToken = default)
        {
            try
            {
                var eventData = new
                {
                    type = "status_change",
                    executionId = args.ExecutionId,
                    oldStatus = args.OldStatus,
                    newStatus = args.NewStatus,
                    changedAt = args.ChangedAt.ToString("O"),
                    reason = args.Reason,
                    timestamp = DateTime.UtcNow.ToString("O")
                };

                // Send to specific execution group
                await _hubContext.Clients.Group($"execution_{executionId}")
                    .SendAsync("ExecutionStatusChanged", eventData, cancellationToken);

                // Also send to user execution group for dashboard updates
                await _hubContext.Clients.Group($"user_executions_{GetUserFromExecution(executionId)}")
                    .SendAsync("UserExecutionStatusChanged", eventData, cancellationToken);

                _logger.LogInformation("Streamed status change for {ExecutionId}: {OldStatus} -> {NewStatus}",
                    executionId, args.OldStatus, args.NewStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stream status change for {ExecutionId}", executionId);
                throw;
            }
        }

        public async Task StreamProgressAsync(string executionId, ExecutionProgressEventArgs args, CancellationToken cancellationToken = default)
        {
            try
            {
                var eventData = new
                {
                    type = "progress_update",
                    executionId = args.ExecutionId,
                    progressPercentage = args.ProgressPercentage,
                    currentStep = args.CurrentStep,
                    timestamp = args.Timestamp.ToString("O"),
                    estimatedTimeRemaining = args.EstimatedTimeRemaining?.TotalMinutes,
                    receivedAt = DateTime.UtcNow.ToString("O")
                };

                await _hubContext.Clients.Group($"execution_{executionId}")
                    .SendAsync("ExecutionProgress", eventData, cancellationToken);

                _logger.LogTrace("Streamed progress update for {ExecutionId}: {Progress}% - {Step}",
                    executionId, args.ProgressPercentage, args.CurrentStep);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stream progress update for {ExecutionId}", executionId);
                // Don't throw - progress streaming failures shouldn't break execution
            }
        }

        public async Task StreamExecutionCompletedAsync(string executionId, ExecutionCompletedEventArgs args, CancellationToken cancellationToken = default)
        {
            try
            {
                var eventData = new
                {
                    type = "execution_completed",
                    executionId = args.ExecutionId,
                    status = args.Status,
                    exitCode = args.ExitCode,
                    completedAt = args.CompletedAt.ToString("O"),
                    duration = args.Duration.TotalMinutes,
                    success = args.Success,
                    errorMessage = args.ErrorMessage,
                    outputFiles = args.OutputFiles,
                    timestamp = DateTime.UtcNow.ToString("O")
                };

                // Send to specific execution group
                await _hubContext.Clients.Group($"execution_{executionId}")
                    .SendAsync("ExecutionCompleted", eventData, cancellationToken);

                // Also send to user execution group for dashboard updates
                var userId = GetUserFromExecution(executionId);
                await _hubContext.Clients.Group($"user_executions_{userId}")
                    .SendAsync("UserExecutionCompleted", eventData, cancellationToken);

                _logger.LogInformation("Streamed execution completed for {ExecutionId}: {Status} (Exit: {ExitCode}, Duration: {Duration}min)",
                    executionId, args.Status, args.ExitCode, args.Duration.TotalMinutes);

                // Cleanup streaming
                await StopExecutionStreamingAsync(executionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stream execution completed for {ExecutionId}", executionId);
                throw;
            }
        }

        public async Task StreamResourceUsageAsync(string executionId, ExecutionResourceUsageEventArgs args, CancellationToken cancellationToken = default)
        {
            try
            {
                var eventData = new
                {
                    type = "resource_usage",
                    executionId = args.ExecutionId,
                    timestamp = args.Timestamp.ToString("O"),
                    cpuUsagePercentage = args.CpuUsagePercentage,
                    memoryUsedBytes = args.MemoryUsedBytes,
                    memoryUsedMB = args.MemoryUsedBytes / (1024.0 * 1024.0),
                    diskUsedBytes = args.DiskUsedBytes,
                    diskUsedMB = args.DiskUsedBytes / (1024.0 * 1024.0),
                    cpuTimeSeconds = args.CpuTimeSeconds,
                    receivedAt = DateTime.UtcNow.ToString("O")
                };

                await _hubContext.Clients.Group($"execution_{executionId}")
                    .SendAsync("ExecutionResourceUsage", eventData, cancellationToken);

                _logger.LogTrace("Streamed resource usage for {ExecutionId}: CPU: {Cpu}%, Memory: {Memory}MB",
                    executionId, args.CpuUsagePercentage, args.MemoryUsedBytes / (1024.0 * 1024.0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stream resource usage for {ExecutionId}", executionId);
                // Don't throw - resource usage streaming failures shouldn't break execution
            }
        }

        /// <summary>
        /// Helper method to extract user ID from execution context
        /// In a real implementation, this would lookup the execution in the database
        /// </summary>
        private string GetUserFromExecution(string executionId)
        {
            // TODO: Implement proper user lookup from execution
            // For now, return a placeholder
            return "unknown";
        }

        /// <summary>
        /// Get statistics about active streams
        /// </summary>
        public async Task<object> GetStreamingStatsAsync()
        {
            await _streamingSemaphore.WaitAsync();
            try
            {
                return new
                {
                    activeStreams = _activeStreams.Count,
                    oldestStream = _activeStreams.Values.DefaultIfEmpty().Min(),
                    newestStream = _activeStreams.Values.DefaultIfEmpty().Max(),
                    executionIds = _activeStreams.Keys.ToList()
                };
            }
            finally
            {
                _streamingSemaphore.Release();
            }
        }
    }
}