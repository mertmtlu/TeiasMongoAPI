using Microsoft.AspNetCore.SignalR;
using TeiasMongoAPI.API.Hubs;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Services
{
    public class SignalRWorkflowNotificationService : IWorkflowNotificationService
    {
        private readonly IHubContext<UIWorkflowHub> _hubContext;
        private readonly ILogger<SignalRWorkflowNotificationService> _logger;

        public SignalRWorkflowNotificationService(
            IHubContext<UIWorkflowHub> hubContext,
            ILogger<SignalRWorkflowNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyUIInteractionCreatedAsync(string workflowId, UIInteractionCreatedEventArgs args, CancellationToken cancellationToken = default)
        {
            try
            {
                var eventData = new
                {
                    workflowId,
                    interactionId = args.InteractionId,
                    nodeId = args.NodeId,
                    interactionType = args.InteractionType,
                    status = args.Status,
                    title = args.Title,
                    description = args.Description,
                    inputSchema = args.InputSchema,
                    createdAt = args.CreatedAt,
                    timeout = args.Timeout
                };

                await _hubContext.Clients.Group(workflowId).SendAsync("UIInteractionCreated", eventData, cancellationToken);
                _logger.LogDebug($"Sent UIInteractionCreated notification for workflow {workflowId}, interaction {args.InteractionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send UIInteractionCreated notification for workflow {workflowId}");
                throw;
            }
        }

        public async Task NotifyUIInteractionStatusChangedAsync(string workflowId, UIInteractionStatusChangedEventArgs args, CancellationToken cancellationToken = default)
        {
            try
            {
                var eventData = new
                {
                    workflowId,
                    interactionId = args.InteractionId,
                    status = args.Status,
                    outputData = args.OutputData,
                    completedAt = args.CompletedAt
                };

                await _hubContext.Clients.Group(workflowId).SendAsync("UIInteractionStatusChanged", eventData, cancellationToken);
                _logger.LogDebug($"Sent UIInteractionStatusChanged notification for workflow {workflowId}, interaction {args.InteractionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send UIInteractionStatusChanged notification for workflow {workflowId}");
                throw;
            }
        }

        public async Task NotifyUIInteractionAvailableAsync(string workflowId, UIInteractionAvailableEventArgs args, CancellationToken cancellationToken = default)
        {
            try
            {
                var eventData = new
                {
                    workflowId,
                    nodeId = args.NodeId,
                    interactionId = args.InteractionId,
                    timestamp = args.Timestamp
                };

                await _hubContext.Clients.Group(workflowId).SendAsync("UIInteractionAvailable", eventData, cancellationToken);
                _logger.LogDebug($"Sent UIInteractionAvailable notification for workflow {workflowId}, node {args.NodeId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send UIInteractionAvailable notification for workflow {workflowId}");
                throw;
            }
        }
    }
}