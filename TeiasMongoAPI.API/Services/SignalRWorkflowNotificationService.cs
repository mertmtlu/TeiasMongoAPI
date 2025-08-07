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
                            fields = TransformInputSchemaToFields(args.InputSchema),
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

                _logger.LogInformation("Attempting to send UI interaction notification for ExecutionID {ExecutionId} to workflow group {WorkflowId}. HubContext instance hash: {HubContextHashCode}", 
                    args.ExecutionId, workflowId, _hubContext.GetHashCode());
                
                await _hubContext.Clients.Group(workflowId).SendAsync("UIInteractionCreated", eventData, cancellationToken);
                
                _logger.LogInformation("Successfully sent UI interaction notification for ExecutionID {ExecutionId}", args.ExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send UI interaction notification for ExecutionID {ExecutionId}. Exception: {ExceptionMessage}", 
                    args.ExecutionId, ex.Message);
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

                _logger.LogInformation("Attempting to send UI interaction status change notification for ExecutionID {ExecutionId} to workflow group {WorkflowId}", 
                    args.ExecutionId, workflowId);
                
                await _hubContext.Clients.Group(workflowId).SendAsync("UIInteractionStatusChanged", eventData, cancellationToken);
                
                _logger.LogInformation("Successfully sent UI interaction status change notification for ExecutionID {ExecutionId}", args.ExecutionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send UI interaction status change notification for ExecutionID {ExecutionId}. Exception: {ExceptionMessage}", 
                    args.ExecutionId, ex.Message);
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

                _logger.LogInformation("Attempting to send UI interaction available notification for InteractionID {InteractionId} to workflow group {WorkflowId}", 
                    args.InteractionId, workflowId);
                
                await _hubContext.Clients.Group(workflowId).SendAsync("UIInteractionAvailable", eventData, cancellationToken);
                
                _logger.LogInformation("Successfully sent UI interaction available notification for InteractionID {InteractionId}", args.InteractionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send UI interaction available notification for InteractionID {InteractionId}. Exception: {ExceptionMessage}", 
                    args.InteractionId, ex.Message);
                throw;
            }
        }

        private object TransformInputSchemaToFields(Dictionary<string, object> inputSchema)
        {
            // If schema already has properly formatted fields, return them
            if (inputSchema.ContainsKey("fields") && inputSchema["fields"] is IEnumerable<object> existingFields)
            {
                return existingFields;
            }

            // Transform schema properties to UIInputField format
            var fields = new List<object>();

            foreach (var kvp in inputSchema)
            {
                // Skip non-field properties
                if (kvp.Key is "submitLabel" or "cancelLabel" or "allowSkip" or "title" or "description")
                    continue;

                // Create field object matching UIInputField interface
                var field = new Dictionary<string, object>
                {
                    ["name"] = kvp.Key,
                    ["type"] = GetFieldType(kvp.Value),
                    ["label"] = GetFieldLabel(kvp.Key, kvp.Value),
                    ["required"] = GetFieldRequired(kvp.Value),
                    ["placeholder"] = GetFieldPlaceholder(kvp.Value)
                };

                // Add validation if present
                var validation = GetFieldValidation(kvp.Value);
                if (validation.Count > 0)
                {
                    field["validation"] = validation;
                }

                // Add options for select fields
                var options = GetFieldOptions(kvp.Value);
                if (options.Count > 0)
                {
                    field["options"] = options;
                }

                fields.Add(field);
            }

            return fields;
        }

        private string GetFieldType(object fieldValue)
        {
            if (fieldValue is Dictionary<string, object> fieldDef)
            {
                if (fieldDef.ContainsKey("type"))
                    return fieldDef["type"]?.ToString() ?? "text";
            }
            return "text";
        }

        private string GetFieldLabel(string fieldName, object fieldValue)
        {
            if (fieldValue is Dictionary<string, object> fieldDef && fieldDef.ContainsKey("label"))
                return fieldDef["label"]?.ToString() ?? fieldName;

            // Convert camelCase/snake_case to readable label
            return string.Concat(fieldName.Select((x, i) => i > 0 && char.IsUpper(x) ? " " + x : x.ToString())).Trim();
        }

        private bool GetFieldRequired(object fieldValue)
        {
            if (fieldValue is Dictionary<string, object> fieldDef && fieldDef.ContainsKey("required"))
                return bool.TryParse(fieldDef["required"]?.ToString(), out bool required) && required;
            return false;
        }

        private string GetFieldPlaceholder(object fieldValue)
        {
            if (fieldValue is Dictionary<string, object> fieldDef && fieldDef.ContainsKey("placeholder"))
                return fieldDef["placeholder"]?.ToString() ?? "";
            return "";
        }

        private Dictionary<string, object> GetFieldValidation(object fieldValue)
        {
            var validation = new Dictionary<string, object>();
            if (fieldValue is Dictionary<string, object> fieldDef && fieldDef.ContainsKey("validation"))
            {
                if (fieldDef["validation"] is Dictionary<string, object> validationRules)
                    return validationRules;
            }
            return validation;
        }

        private List<object> GetFieldOptions(object fieldValue)
        {
            var options = new List<object>();
            if (fieldValue is Dictionary<string, object> fieldDef && fieldDef.ContainsKey("options"))
            {
                if (fieldDef["options"] is IEnumerable<object> optionsList)
                    return optionsList.ToList();
            }
            return options;
        }
    }
}