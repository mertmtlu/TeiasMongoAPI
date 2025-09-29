using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TeiasMongoAPI.Services.Interfaces; // Your service interface

namespace TeiasMongoAPI.API.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time program execution output streaming.
    /// </summary>
    [Authorize]
    public class ExecutionHub : Hub
    {
        private readonly ILogger<ExecutionHub> _logger;
        private readonly IExecutionOutputStreamingService _streamingService;

        public ExecutionHub(ILogger<ExecutionHub> logger, IExecutionOutputStreamingService streamingService)
        {
            _logger = logger;
            _streamingService = streamingService;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier ?? "Unknown";
            _logger.LogInformation("Client connected to ExecutionHub with ConnectionID {ConnectionId} for UserID {UserId}",
                Context.ConnectionId, userId);
            await base.OnConnectedAsync();
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
        /// --- MODIFIED: THIS IS THE CORE OF THE SOLUTION ---
        /// Join execution group to receive real-time process output.
        /// Upon joining, the server will automatically send any cached logs for the execution.
        /// </summary>
        public async Task JoinExecutionGroup(string executionId)
        {
            var userId = Context.UserIdentifier ?? "Unknown";
            var connectionId = Context.ConnectionId;

            // 1. Add the client to the group for all *future* real-time updates
            await Groups.AddToGroupAsync(connectionId, $"execution_{executionId}");
            _logger.LogInformation("Client {ConnectionId} (User: {UserId}) joined execution group {ExecutionId}", connectionId, userId, executionId);

            // 2. Immediately get the current cached logs
            var cachedLogs = _streamingService.GetCachedLogs(executionId).ToList();
            _logger.LogInformation("Pushing {LogCount} cached log lines to client {ConnectionId} for execution {ExecutionId}", cachedLogs.Count, connectionId, executionId);

            // 3. Send the historical logs ONLY to the client that just called this method
            await Clients.Caller.SendAsync("InitialLogs", cachedLogs);
        }

        /// <summary>
        /// --- REMOVED ---
        /// This method is no longer needed as the logic is now inside JoinExecutionGroup to prevent race conditions.
        /// </summary>
        // public async Task RequestInitialLogs(string executionId) { ... }

        /// <summary>
        /// Leave execution group to stop receiving real-time process output.
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

        // --- ALL OTHER METHODS REMAIN UNCHANGED ---

        public async Task JoinUserExecutionGroup(string userId)
        {
            var currentUserId = Context.UserIdentifier ?? "Unknown";
            var groupName = $"user_executions_{userId}";

            if (currentUserId != userId && currentUserId != "admin")
            {
                await Clients.Caller.SendAsync("Error", new { message = "Unauthorized: Cannot join another user's execution group" });
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            await Clients.Caller.SendAsync("JoinedUserExecutionGroup", new { userId, message = "Successfully joined user execution group" });

            _logger.LogInformation("Client {ConnectionId} (User: {CurrentUserId}) joined user execution group for {UserId}",
                Context.ConnectionId, currentUserId, userId);
        }

        public async Task LeaveUserExecutionGroup(string userId)
        {
            var currentUserId = Context.UserIdentifier ?? "Unknown";
            var groupName = $"user_executions_{userId}";

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await Clients.Caller.SendAsync("LeftUserExecutionGroup", new { userId, message = "Successfully left user execution group" });

            _logger.LogInformation("Client {ConnectionId} (User: {CurrentUserId}) left user execution group for {UserId}",
                Context.ConnectionId, currentUserId, userId);
        }

        public async Task RequestExecutionStatus(string executionId)
        {
            var userId = Context.UserIdentifier ?? "Unknown";
            _logger.LogInformation("Client {ConnectionId} (User: {UserId}) requested status for execution {ExecutionId}", Context.ConnectionId, userId, executionId);
            await Clients.Caller.SendAsync("ExecutionStatusRequested", new { executionId, requestedBy = userId, requestedAt = DateTime.UtcNow });
        }

        public async Task Ping()
        {
            await Clients.Caller.SendAsync("Pong", new { timestamp = DateTime.UtcNow, connectionId = Context.ConnectionId });
        }
    }
}