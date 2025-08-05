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
                    sessionId = args.InteractionId, // Frontend expects sessionId
                    workflowId,
                    executionId = args.ExecutionId, // Add missing executionId
                    nodeId = args.NodeId,
                    status = args.Status,
                    uiComponent = new // Frontend expects uiComponent object
                    {
                        id = args.InteractionId,
                        name = args.Title,
                        type = args.InteractionType,
                        configuration = new
                        {
                            title = args.Title,
                            description = args.Description,
                            fields = args.InputSchema.ContainsKey("fields") ? args.InputSchema["fields"] : new object[0],
                            submitLabel = args.InputSchema.ContainsKey("submitLabel") ? args.InputSchema["submitLabel"] : "Submit",
                            cancelLabel = args.InputSchema.ContainsKey("cancelLabel") ? args.InputSchema["cancelLabel"] : "Cancel",
                            allowSkip = args.InputSchema.ContainsKey("allowSkip") ? args.InputSchema["allowSkip"] : false
                        }
                    },
                    contextData = args.ContextData ?? new Dictionary<string, object>(), // Add contextData
                    timeoutAt = args.CreatedAt.Add(args.Timeout ?? TimeSpan.FromMinutes(30)).ToString("o"), // Frontend expects ISO string
                    createdAt = args.CreatedAt.ToString("o"),
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
                    sessionId = args.InteractionId, // Frontend expects sessionId
                    workflowId,
                    executionId = args.ExecutionId, // Add if available
                    status = args.Status,
                    outputData = args.OutputData,
                    completedAt = args.CompletedAt?.ToString("o") // ISO string format
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