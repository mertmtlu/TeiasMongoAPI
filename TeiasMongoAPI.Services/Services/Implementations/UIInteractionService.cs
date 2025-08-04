using AutoMapper;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.UIWorkflow;
using TeiasMongoAPI.Services.DTOs.Response.UIWorkflow;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class UIInteractionService : BaseService, IUIInteractionService
    {
        private readonly IWorkflowNotificationService _notificationService;

        public UIInteractionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<UIInteractionService> logger,
            IWorkflowNotificationService notificationService)
            : base(unitOfWork, mapper, logger)
        {
            _notificationService = notificationService;
        }

        public async Task<List<UIInteractionSession>> GetPendingUIInteractionsForUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var interactions = await _unitOfWork.UIInteractions.GetPendingForUserAsync(userId, cancellationToken);
                var sessions = new List<UIInteractionSession>();

                foreach (var interaction in interactions)
                {
                    var session = await MapToUIInteractionSessionAsync(interaction, cancellationToken);
                    if (session != null)
                    {
                        sessions.Add(session);
                    }
                }

                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending UI interactions for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<UIInteractionSession>> GetUIInteractionsForWorkflowExecutionAsync(string workflowId, string executionId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ObjectId.TryParse(executionId, out var executionObjectId))
                {
                    return new List<UIInteractionSession>();
                }

                var interactions = await _unitOfWork.UIInteractions.GetByWorkflowExecutionAsync(executionObjectId, cancellationToken);
                var sessions = new List<UIInteractionSession>();

                foreach (var interaction in interactions)
                {
                    var session = await MapToUIInteractionSessionAsync(interaction, cancellationToken);
                    if (session != null)
                    {
                        sessions.Add(session);
                    }
                }

                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting UI interactions for workflow execution {ExecutionId}", executionId);
                throw;
            }
        }

        public async Task<UIInteractionSession?> GetUIInteractionAsync(string interactionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var interaction = await _unitOfWork.UIInteractions.GetByIdAsync(interactionId, cancellationToken);
                if (interaction == null)
                {
                    return null;
                }

                return await MapToUIInteractionSessionAsync(interaction, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting UI interaction {InteractionId}", interactionId);
                throw;
            }
        }

        public async Task<bool> SubmitUIInteractionAsync(string interactionId, BsonDocument responseData, string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var interaction = await _unitOfWork.UIInteractions.GetByIdAsync(interactionId, cancellationToken);
                if (interaction == null)
                {
                    _logger.LogWarning("UI interaction {InteractionId} not found", interactionId);
                    return false;
                }

                if (interaction.Status != UIInteractionStatus.Pending && interaction.Status != UIInteractionStatus.InProgress)
                {
                    _logger.LogWarning("UI interaction {InteractionId} is not in a submittable state: {Status}", interactionId, interaction.Status);
                    return false;
                }

                // Check timeout
                if (interaction.Timeout.HasValue && DateTime.UtcNow > interaction.CreatedAt.Add(interaction.Timeout.Value))
                {
                    await _unitOfWork.UIInteractions.UpdateStatusAsync(interaction._ID, UIInteractionStatus.Timeout, cancellationToken: cancellationToken);
                    
                    // Emit timeout event
                    await _notificationService.NotifyUIInteractionStatusChangedAsync(
                        interaction.WorkflowExecutionId.ToString(),
                        new UIInteractionStatusChangedEventArgs
                        {
                            InteractionId = interactionId,
                            Status = UIInteractionStatus.Timeout.ToString(),
                            CompletedAt = DateTime.UtcNow
                        },
                        cancellationToken);

                    return false;
                }

                // Update interaction with response data
                var success = await _unitOfWork.UIInteractions.UpdateStatusAsync(
                    interaction._ID, 
                    UIInteractionStatus.Completed, 
                    responseData, 
                    cancellationToken);

                if (success)
                {
                    // Emit completion event
                    await _notificationService.NotifyUIInteractionStatusChangedAsync(
                        interaction.WorkflowExecutionId.ToString(),
                        new UIInteractionStatusChangedEventArgs
                        {
                            InteractionId = interactionId,
                            Status = UIInteractionStatus.Completed.ToString(),
                            OutputData = responseData,
                            CompletedAt = DateTime.UtcNow
                        },
                        cancellationToken);

                    _logger.LogInformation("UI interaction {InteractionId} completed by user {UserId}", interactionId, userId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting UI interaction {InteractionId}", interactionId);
                throw;
            }
        }

        public async Task<bool> CancelUIInteractionAsync(string interactionId, string userId, string? reason = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var interaction = await _unitOfWork.UIInteractions.GetByIdAsync(interactionId, cancellationToken);
                if (interaction == null)
                {
                    _logger.LogWarning("UI interaction {InteractionId} not found", interactionId);
                    return false;
                }

                if (interaction.Status != UIInteractionStatus.Pending && interaction.Status != UIInteractionStatus.InProgress)
                {
                    _logger.LogWarning("UI interaction {InteractionId} cannot be cancelled in state: {Status}", interactionId, interaction.Status);
                    return false;
                }

                var cancellationData = new BsonDocument();
                if (!string.IsNullOrEmpty(reason))
                {
                    cancellationData["cancellationReason"] = reason;
                    cancellationData["cancelledBy"] = userId;
                }

                var success = await _unitOfWork.UIInteractions.UpdateStatusAsync(
                    interaction._ID, 
                    UIInteractionStatus.Cancelled, 
                    cancellationData, 
                    cancellationToken);

                if (success)
                {
                    // Emit cancellation event
                    await _notificationService.NotifyUIInteractionStatusChangedAsync(
                        interaction.WorkflowExecutionId.ToString(),
                        new UIInteractionStatusChangedEventArgs
                        {
                            InteractionId = interactionId,
                            Status = UIInteractionStatus.Cancelled.ToString(),
                            OutputData = cancellationData,
                            CompletedAt = DateTime.UtcNow
                        },
                        cancellationToken);

                    _logger.LogInformation("UI interaction {InteractionId} cancelled by user {UserId}", interactionId, userId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling UI interaction {InteractionId}", interactionId);
                throw;
            }
        }

        public async Task<UIInteractionSession> CreateUIInteractionAsync(string workflowId, string executionId, string nodeId, UIInteractionRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!ObjectId.TryParse(executionId, out var executionObjectId))
                {
                    throw new ArgumentException("Invalid execution ID format", nameof(executionId));
                }

                // Parse interaction type
                if (!Enum.TryParse<UIInteractionType>(request.InteractionType, out var interactionType))
                {
                    interactionType = UIInteractionType.UserInput;
                }

                var interaction = new UIInteraction
                {
                    _ID = ObjectId.GenerateNewId(),
                    WorkflowExecutionId = executionObjectId,
                    NodeId = nodeId,
                    InteractionType = interactionType,
                    Status = UIInteractionStatus.Pending,
                    Title = request.Title,
                    Description = request.Description,
                    InputSchema = request.InputSchema,
                    InputData = request.InitialData ?? new BsonDocument(),
                    Timeout = request.Timeout,
                    CreatedAt = DateTime.UtcNow,
                    Metadata = request.Metadata.ToBsonDocument()
                };

                await _unitOfWork.UIInteractions.CreateAsync(interaction, cancellationToken);

                // Emit creation event
                await _notificationService.NotifyUIInteractionCreatedAsync(
                    workflowId,
                    new UIInteractionCreatedEventArgs
                    {
                        InteractionId = interaction._ID.ToString(),
                        NodeId = nodeId,
                        InteractionType = interactionType.ToString(),
                        Status = UIInteractionStatus.Pending.ToString(),
                        Title = request.Title,
                        Description = request.Description,
                        InputSchema = request.InputSchema,
                        CreatedAt = interaction.CreatedAt,
                        Timeout = request.Timeout
                    },
                    cancellationToken);

                // Also emit available event for legacy compatibility
                await _notificationService.NotifyUIInteractionAvailableAsync(
                    workflowId,
                    new UIInteractionAvailableEventArgs
                    {
                        NodeId = nodeId,
                        InteractionId = interaction._ID.ToString(),
                        Timestamp = interaction.CreatedAt
                    },
                    cancellationToken);

                return await MapToUIInteractionSessionAsync(interaction, cancellationToken) 
                    ?? throw new InvalidOperationException("Failed to map created interaction to session");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating UI interaction for workflow {WorkflowId}, execution {ExecutionId}", workflowId, executionId);
                throw;
            }
        }

        public async Task<List<UIInteractionSession>> GetActiveInteractionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var interactions = await _unitOfWork.UIInteractions.GetActiveInteractionsAsync(cancellationToken);
                var sessions = new List<UIInteractionSession>();

                foreach (var interaction in interactions)
                {
                    var session = await MapToUIInteractionSessionAsync(interaction, cancellationToken);
                    if (session != null)
                    {
                        sessions.Add(session);
                    }
                }

                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active UI interactions");
                throw;
            }
        }

        public async Task ProcessTimedOutInteractionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var timedOutInteractions = await _unitOfWork.UIInteractions.GetTimedOutInteractionsAsync(cancellationToken);
                
                foreach (var interaction in timedOutInteractions)
                {
                    await _unitOfWork.UIInteractions.UpdateStatusAsync(interaction._ID, UIInteractionStatus.Timeout, cancellationToken: cancellationToken);
                    
                    // Emit timeout event
                    await _notificationService.NotifyUIInteractionStatusChangedAsync(
                        interaction.WorkflowExecutionId.ToString(),
                        new UIInteractionStatusChangedEventArgs
                        {
                            InteractionId = interaction._ID.ToString(),
                            Status = UIInteractionStatus.Timeout.ToString(),
                            CompletedAt = DateTime.UtcNow
                        },
                        cancellationToken);

                    _logger.LogInformation("UI interaction {InteractionId} timed out", interaction._ID);
                }

                if (timedOutInteractions.Count > 0)
                {
                    _logger.LogInformation("Processed {Count} timed out UI interactions", timedOutInteractions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing timed out UI interactions");
                throw;
            }
        }

        private async Task<UIInteractionSession?> MapToUIInteractionSessionAsync(UIInteraction interaction, CancellationToken cancellationToken)
        {
            try
            {
                // Get workflow execution to resolve workflow ID
                var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(interaction.WorkflowExecutionId, cancellationToken);
                if (execution == null)
                {
                    _logger.LogWarning("Workflow execution {ExecutionId} not found for UI interaction {InteractionId}", 
                        interaction.WorkflowExecutionId, interaction._ID);
                    return null;
                }

                return new UIInteractionSession
                {
                    SessionId = interaction._ID.ToString(),
                    WorkflowId = execution.WorkflowId.ToString(),
                    ExecutionId = interaction.WorkflowExecutionId.ToString(),
                    NodeId = interaction.NodeId,
                    Status = interaction.Status.ToString(),
                    InteractionType = interaction.InteractionType.ToString(),
                    Title = interaction.Title,
                    Description = interaction.Description,
                    InputSchema = interaction.InputSchema,
                    InputData = interaction.InputData,
                    OutputData = interaction.OutputData,
                    TimeoutAt = interaction.Timeout.HasValue ? interaction.CreatedAt.Add(interaction.Timeout.Value) : null,
                    CreatedAt = interaction.CreatedAt,
                    CompletedAt = interaction.CompletedAt,
                    Metadata = interaction.Metadata.ToDictionary()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping UI interaction {InteractionId} to session", interaction._ID);
                return null;
            }
        }
    }
}