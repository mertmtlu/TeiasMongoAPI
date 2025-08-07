using Microsoft.AspNetCore.SignalR;

namespace TeiasMongoAPI.API.Hubs
{
    public class UIWorkflowHub : Hub
    {
        private readonly ILogger<UIWorkflowHub> _logger;

        public UIWorkflowHub(ILogger<UIWorkflowHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier ?? "Unknown";
            _logger.LogInformation("Client connected to uiWorkflowHub with ConnectionID {ConnectionId} for UserID {UserId}", 
                Context.ConnectionId, userId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier ?? "Unknown";
            _logger.LogInformation("Client disconnected from uiWorkflowHub with ConnectionID {ConnectionId} for UserID {UserId}", 
                Context.ConnectionId, userId);
            await base.OnDisconnectedAsync(exception);
        }
        public async Task JoinWorkflowGroup(string workflowId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, workflowId);
            await Clients.Caller.SendAsync("JoinedWorkflowGroup", workflowId);
        }

        public async Task LeaveWorkflowGroup(string workflowId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, workflowId);
            await Clients.Caller.SendAsync("LeftWorkflowGroup", workflowId);
        }
    }
}