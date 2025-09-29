using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time program execution output streaming
    /// Eliminates the need to poll execution status every 2 seconds
    /// Clients receive live stdout/stderr output, status changes, and progress updates
    /// </summary>
    [Authorize]
    public class ExecutionHub : Hub
    {
        private readonly ILogger<ExecutionHub> _logger;
        private readonly IExecutionOutputStreamingService _streamingService;

        public ExecutionHub(ILogger<ExecutionHub> logger, IExecutionOutputStreamingService streamingService)
        {
            _logger = logger; _streamingService = streamingService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier ?? "Unknown";
            _logger.LogInformation("Client connected to ExecutionHub with ConnectionID {ConnectionId} for UserID {UserId}",
                Context.ConnectionId, userId);
            await base.OnConnectedAsync();
        }

        public async Task RequestInitialLogs(string executionId)
        {
            var userId = Context.UserIdentifier ?? "Unknown";
            _logger.LogInformation("Client {ConnectionId} (User: {UserId}) requested initial logs for execution {ExecutionId}",
                Context.ConnectionId, userId, executionId);

            var cachedLogs = _streamingService.GetCachedLogs(executionId);
            await Clients.Caller.SendAsync("InitialLogs", cachedLogs);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier ?? "Unknown";
            _logger.LogInformation("Client disconnected from ExecutionHub with ConnectionID {ConnectionId} for UserID {UserId}",
                Context.ConnectionId, userId);

            if (exception != null)
            {
                _logger.LogWarning(exception, "Client {ConnectionId} disconnected with exception", Context.ConnectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// LIVE STREAMING: Join execution group to receive real-time process output
        /// No more polling needed - output streams automatically!
        /// Usage: await connection.invoke("JoinExecutionGroup", executionId)
        /// </summary>
        public async Task JoinExecutionGroup(string executionId)
        {
            var userId = Context.UserIdentifier ?? "Unknown";
            var groupName = $"execution_{executionId}";

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            await Clients.Caller.SendAsync("JoinedExecutionGroup", executionId);

            _logger.LogInformation("Client {ConnectionId} (User: {UserId}) joined execution group {ExecutionId} for live output streaming",
                Context.ConnectionId, userId, executionId);
        }

        /// <summary>
        /// LIVE STREAMING: Leave execution group to stop receiving real-time process output
        /// Usage: await connection.invoke("LeaveExecutionGroup", executionId)
        /// </summary>
        public async Task LeaveExecutionGroup(string executionId)
        {
            var userId = Context.UserIdentifier ?? "Unknown";
            var groupName = $"execution_{executionId}";

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await Clients.Caller.SendAsync("LeftExecutionGroup", executionId);

            _logger.LogInformation("Client {ConnectionId} (User: {UserId}) left execution group {ExecutionId}",
                Context.ConnectionId, userId, executionId);
        }

        /// <summary>
        /// LIVE STREAMING: Join user group to receive notifications for all user's executions
        /// Useful for dashboard views showing all user executions
        /// Usage: await connection.invoke("JoinUserExecutionGroup", userId)
        /// </summary>
        public async Task JoinUserExecutionGroup(string userId)
        {
            var currentUserId = Context.UserIdentifier ?? "Unknown";
            var groupName = $"user_executions_{userId}";

            // Security check: users can only join their own execution groups
            if (currentUserId != userId && currentUserId != "admin")
            {
                await Clients.Caller.SendAsync("Error", new {
                    message = "Unauthorized: Cannot join another user's execution group"
                });
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            await Clients.Caller.SendAsync("JoinedUserExecutionGroup", new {
                userId,
                message = "Successfully joined user execution group"
            });

            _logger.LogInformation("Client {ConnectionId} (User: {CurrentUserId}) joined user execution group for {UserId}",
                Context.ConnectionId, currentUserId, userId);
        }

        /// <summary>
        /// LIVE STREAMING: Leave user execution group
        /// Usage: await connection.invoke("LeaveUserExecutionGroup", userId)
        /// </summary>
        public async Task LeaveUserExecutionGroup(string userId)
        {
            var currentUserId = Context.UserIdentifier ?? "Unknown";
            var groupName = $"user_executions_{userId}";

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await Clients.Caller.SendAsync("LeftUserExecutionGroup", new {
                userId,
                message = "Successfully left user execution group"
            });

            _logger.LogInformation("Client {ConnectionId} (User: {CurrentUserId}) left user execution group for {UserId}",
                Context.ConnectionId, currentUserId, userId);
        }

        /// <summary>
        /// LIVE STREAMING: Request current status and recent output for a specific execution
        /// Useful for clients joining mid-execution to get current state
        /// Usage: await connection.invoke("RequestExecutionStatus", executionId)
        /// </summary>
        public async Task RequestExecutionStatus(string executionId)
        {
            var userId = Context.UserIdentifier ?? "Unknown";

            _logger.LogInformation("Client {ConnectionId} (User: {UserId}) requested status for execution {ExecutionId}",
                Context.ConnectionId, userId, executionId);

            // Signal to the execution service that a client wants current status
            // This will be handled by IExecutionOutputStreamingService
            await Clients.Caller.SendAsync("ExecutionStatusRequested", new {
                executionId,
                requestedBy = userId,
                requestedAt = DateTime.UtcNow
            });
        }

        /// <summary>
        /// LIVE STREAMING: Ping to keep connection alive during long executions
        /// Usage: await connection.invoke("Ping")
        /// </summary>
        public async Task Ping()
        {
            await Clients.Caller.SendAsync("Pong", new {
                timestamp = DateTime.UtcNow,
                connectionId = Context.ConnectionId
            });
        }
    }
}