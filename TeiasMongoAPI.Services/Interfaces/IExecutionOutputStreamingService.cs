using System;
using System.Threading;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// Service for streaming live execution output in real-time via SignalR
    /// Eliminates need for polling execution status/output every 2 seconds
    /// </summary>
    public interface IExecutionOutputStreamingService
    {
        /// <summary>
        /// Start streaming for a specific execution ID
        /// Clients should join the execution group to receive updates
        /// </summary>
        Task StartExecutionStreamingAsync(string executionId, string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop streaming for a specific execution ID
        /// </summary>
        Task StopExecutionStreamingAsync(string executionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream execution started event
        /// </summary>
        Task StreamExecutionStartedAsync(string executionId, ExecutionStartedEventArgs args, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream live stdout output as it's received from the process
        /// </summary>
        Task StreamOutputAsync(string executionId, string output, DateTime timestamp, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream live stderr output as it's received from the process
        /// </summary>
        Task StreamErrorAsync(string executionId, string error, DateTime timestamp, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream execution status change (running -> completed/failed/stopped)
        /// </summary>
        Task StreamStatusChangeAsync(string executionId, ExecutionStatusChangedEventArgs args, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream execution progress updates for long-running processes
        /// </summary>
        Task StreamProgressAsync(string executionId, ExecutionProgressEventArgs args, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream execution completed event with final results
        /// </summary>
        Task StreamExecutionCompletedAsync(string executionId, ExecutionCompletedEventArgs args, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream execution resource usage updates (CPU, Memory, etc.)
        /// </summary>
        Task StreamResourceUsageAsync(string executionId, ExecutionResourceUsageEventArgs args, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Event arguments for execution started
    /// </summary>
    public class ExecutionStartedEventArgs
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public int TimeoutMinutes { get; set; }
        public object? Parameters { get; set; }
    }

    /// <summary>
    /// Event arguments for execution status change
    /// </summary>
    public class ExecutionStatusChangedEventArgs
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string OldStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Event arguments for execution progress
    /// </summary>
    public class ExecutionProgressEventArgs
    {
        public string ExecutionId { get; set; } = string.Empty;
        public double ProgressPercentage { get; set; }
        public string CurrentStep { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// Event arguments for execution completed
    /// </summary>
    public class ExecutionCompletedEventArgs
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // completed, failed, stopped
        public int ExitCode { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<string> OutputFiles { get; set; } = new();
    }

    /// <summary>
    /// Event arguments for resource usage updates
    /// </summary>
    public class ExecutionResourceUsageEventArgs
    {
        public string ExecutionId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double CpuUsagePercentage { get; set; }
        public long MemoryUsedBytes { get; set; }
        public long DiskUsedBytes { get; set; }
        public double CpuTimeSeconds { get; set; }
    }
}