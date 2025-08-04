using Microsoft.AspNetCore.SignalR;

namespace TeiasMongoAPI.API.Hubs
{
    public class UIWorkflowHub : Hub
    {
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