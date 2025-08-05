using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Services.DTOs.Request.UIWorkflow;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.UIWorkflow;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;

namespace TeiasMongoAPI.API.Controllers
{
    /// <summary>
    /// Controller for managing UI workflow interactions during workflow execution
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UIWorkflowController : BaseController
    {
        private readonly IUIInteractionService _uiInteractionService;
        private readonly IWorkflowExecutionEngine _workflowExecutionEngine;

        public UIWorkflowController(
            IUIInteractionService uiInteractionService,
            IWorkflowExecutionEngine workflowExecutionEngine,
            ILogger<UIWorkflowController> logger) : base(logger)
        {
            _uiInteractionService = uiInteractionService;
            _workflowExecutionEngine = workflowExecutionEngine;
        }

        /// <summary>
        /// Gets all pending UI interactions for the current user
        /// </summary>
        /// <returns>List of pending UI interactions</returns>
        [HttpGet("pending")]
        public async Task<ActionResult<ApiResponse<UIInteractionSessionListApiResponse>>> GetPendingUIInteractions()
        {
            return await ExecuteActionAsync(async () =>
            {
                if (!CurrentUserId.HasValue)
                {
                    return Unauthorized<UIInteractionSessionListApiResponse>("User not authenticated");
                }

                var sessions = await _uiInteractionService.GetPendingUIInteractionsForUserAsync(
                    CurrentUserId.Value.ToString(), 
                    HttpContext.RequestAborted);

                var response = new UIInteractionSessionListApiResponse
                {
                    Sessions = sessions.Select(s => new UIInteractionSessionApiResponse
                    {
                        SessionId = s.SessionId,
                        WorkflowId = s.WorkflowId,
                        ExecutionId = s.ExecutionId,
                        NodeId = s.NodeId,
                        Status = s.Status,
                        InteractionType = s.InteractionType,
                        Title = s.Title,
                        Description = s.Description,
                        InputSchema = s.InputSchema,
                        InputData = s.InputData,
                        OutputData = s.OutputData,
                        ContextData = s.ContextData,
                        TimeoutAt = s.TimeoutAt,
                        CreatedAt = s.CreatedAt,
                        CompletedAt = s.CompletedAt,
                        Metadata = s.Metadata
                    }).ToList(),
                    TotalCount = sessions.Count
                };

                return Success(response, "Pending UI interactions retrieved successfully");
            }, "GetPendingUIInteractions");
        }

        /// <summary>
        /// Gets all UI interactions for a specific workflow execution
        /// </summary>
        /// <param name="workflowId">The workflow ID</param>
        /// <param name="executionId">The execution ID</param>
        /// <returns>List of UI interactions for the workflow execution</returns>
        [HttpGet("workflows/{workflowId}/executions/{executionId}/ui-interactions")]
        public async Task<ActionResult<ApiResponse<UIInteractionSessionListApiResponse>>> GetWorkflowUIInteractions(
            string workflowId, 
            string executionId)
        {
            return await ExecuteActionAsync(async () =>
            {
                if (!CurrentUserId.HasValue)
                {
                    return Unauthorized<UIInteractionSessionListApiResponse>("User not authenticated");
                }

                // TODO: Validate user has access to this workflow execution
                
                var sessions = await _uiInteractionService.GetUIInteractionsForWorkflowExecutionAsync(
                    workflowId, 
                    executionId, 
                    HttpContext.RequestAborted);

                var response = new UIInteractionSessionListApiResponse
                {
                    Sessions = sessions.Select(s => new UIInteractionSessionApiResponse
                    {
                        SessionId = s.SessionId,
                        WorkflowId = s.WorkflowId,
                        ExecutionId = s.ExecutionId,
                        NodeId = s.NodeId,
                        Status = s.Status,
                        InteractionType = s.InteractionType,
                        Title = s.Title,
                        Description = s.Description,
                        InputSchema = s.InputSchema,
                        InputData = s.InputData,
                        OutputData = s.OutputData,
                        ContextData = s.ContextData,
                        TimeoutAt = s.TimeoutAt,
                        CreatedAt = s.CreatedAt,
                        CompletedAt = s.CompletedAt,
                        Metadata = s.Metadata
                    }).ToList(),
                    TotalCount = sessions.Count
                };

                return Success(response, "Workflow UI interactions retrieved successfully");
            }, "GetWorkflowUIInteractions");
        }

        /// <summary>
        /// Gets details of a specific UI interaction
        /// </summary>
        /// <param name="interactionId">The interaction ID</param>
        /// <returns>UI interaction details</returns>
        [HttpGet("interactions/{interactionId}")]
        public async Task<ActionResult<ApiResponse<UIInteractionDetailApiResponse>>> GetUIInteraction(string interactionId)
        {
            return await ExecuteActionAsync(async () =>
            {
                if (!CurrentUserId.HasValue)
                {
                    return Unauthorized<UIInteractionDetailApiResponse>("User not authenticated");
                }

                var session = await _uiInteractionService.GetUIInteractionAsync(interactionId, HttpContext.RequestAborted);
                if (session == null)
                {
                    return NotFound<UIInteractionDetailApiResponse>("UI interaction not found");
                }

                // TODO: Validate user has access to this interaction

                var response = new UIInteractionDetailApiResponse
                {
                    Session = new UIInteractionSessionApiResponse
                    {
                        SessionId = session.SessionId,
                        WorkflowId = session.WorkflowId,
                        ExecutionId = session.ExecutionId,
                        NodeId = session.NodeId,
                        Status = session.Status,
                        InteractionType = session.InteractionType,
                        Title = session.Title,
                        Description = session.Description,
                        InputSchema = session.InputSchema,
                        InputData = session.InputData,
                        OutputData = session.OutputData,
                        ContextData = session.ContextData,
                        TimeoutAt = session.TimeoutAt,
                        CreatedAt = session.CreatedAt,
                        CompletedAt = session.CompletedAt,
                        Metadata = session.Metadata
                    },
                    WorkflowContext = null // TODO: Populate workflow context info
                };

                return Success(response, "UI interaction details retrieved successfully");
            }, "GetUIInteraction");
        }

        /// <summary>
        /// Submits a response to a UI interaction
        /// </summary>
        /// <param name="interactionId">The interaction ID</param>
        /// <param name="request">The submission request</param>
        /// <returns>Success response</returns>
        [HttpPost("interactions/{interactionId}/submit")]
        public async Task<ActionResult<ApiResponse<string>>> SubmitUIInteraction(
            string interactionId, 
            [FromBody] UIInteractionSubmissionRequest request)
        {
            return await ExecuteActionAsync(async () =>
            {
                var validationResult = ValidateModelState<string>();
                if (validationResult != null) return validationResult;

                if (!CurrentUserId.HasValue)
                {
                    return Unauthorized<string>("User not authenticated");
                }

                if (request.ResponseData == null || !request.ResponseData.Any())
                {
                    return ValidationError<string>("Response data is required", new List<string> { "ResponseData cannot be empty" });
                }

                // TODO: Validate user has access to this interaction

                var responseData = request.ResponseData;
                if (!string.IsNullOrEmpty(request.Comments))
                {
                    responseData["comments"] = request.Comments;
                }
                responseData["action"] = request.Action;
                responseData["submittedBy"] = CurrentUserId.Value.ToString();
                responseData["submittedAt"] = DateTime.UtcNow;

                var success = await _uiInteractionService.SubmitUIInteractionAsync(
                    interactionId, 
                    responseData, 
                    CurrentUserId.Value.ToString(), 
                    HttpContext.RequestAborted);

                if (!success)
                {
                    return Error<string>("Failed to submit UI interaction", new List<string> { "The interaction may no longer be available or may have timed out" });
                }

                return Success("submitted", "UI interaction submitted successfully");
            }, "SubmitUIInteraction");
        }

        /// <summary>
        /// Completes a UI interaction and continues workflow execution
        /// </summary>
        /// <param name="interactionId">The interaction ID</param>
        /// <param name="outputData">The output data from user interaction</param>
        /// <returns>Node execution response</returns>
        [HttpPost("interactions/{interactionId}/complete")]
        public async Task<ActionResult<ApiResponse<NodeExecutionResponseDto>>> CompleteUIInteraction(
            string interactionId,
            [FromBody] Dictionary<string, object> outputData)
        {
            return await ExecuteActionAsync(async () =>
            {
                if (!CurrentUserId.HasValue)
                {
                    return Unauthorized<NodeExecutionResponseDto>("User not authenticated");
                }

                try
                {
                    // Get interaction to find execution and node info
                    var interaction = await _uiInteractionService.GetUIInteractionAsync(interactionId, HttpContext.RequestAborted);
                    if (interaction == null)
                    {
                        return NotFound<NodeExecutionResponseDto>($"UI interaction {interactionId} not found");
                    }

                    var result = await _workflowExecutionEngine.CompleteUIInteractionAsync(
                        interaction.ExecutionId,
                        interaction.NodeId,
                        interactionId,
                        outputData,
                        HttpContext.RequestAborted);

                    return Success(result, "UI interaction completed and workflow execution continued");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to complete UI interaction {InteractionId}", interactionId);
                    return Error<NodeExecutionResponseDto>($"Failed to complete UI interaction: {ex.Message}");
                }
            }, "CompleteUIInteraction");
        }

        /// <summary>
        /// Cancels a UI interaction
        /// </summary>
        /// <param name="interactionId">The interaction ID</param>
        /// <param name="reason">Optional cancellation reason</param>
        /// <returns>Success response</returns>
        [HttpPost("interactions/{interactionId}/cancel")]
        public async Task<ActionResult<ApiResponse<string>>> CancelUIInteraction(
            string interactionId, 
            [FromBody] CancelUIInteractionRequest? request = null)
        {
            return await ExecuteActionAsync(async () =>
            {
                if (!CurrentUserId.HasValue)
                {
                    return Unauthorized<string>("User not authenticated");
                }

                // TODO: Validate user has access to this interaction

                var success = await _uiInteractionService.CancelUIInteractionAsync(
                    interactionId, 
                    CurrentUserId.Value.ToString(), 
                    request?.Reason, 
                    HttpContext.RequestAborted);

                if (!success)
                {
                    return Error<string>("Failed to cancel UI interaction", new List<string> { "The interaction may not be in a cancellable state" });
                }

                return Success("cancelled", "UI interaction cancelled successfully");
            }, "CancelUIInteraction");
        }

        /// <summary>
        /// Gets all active UI interactions (admin endpoint)
        /// </summary>
        /// <returns>List of active UI interactions</returns>
        [HttpGet("active")]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<ActionResult<ApiResponse<UIInteractionSessionListApiResponse>>> GetActiveUIInteractions()
        {
            return await ExecuteActionAsync(async () =>
            {
                var sessions = await _uiInteractionService.GetActiveInteractionsAsync(HttpContext.RequestAborted);

                var response = new UIInteractionSessionListApiResponse
                {
                    Sessions = sessions.Select(s => new UIInteractionSessionApiResponse
                    {
                        SessionId = s.SessionId,
                        WorkflowId = s.WorkflowId,
                        ExecutionId = s.ExecutionId,
                        NodeId = s.NodeId,
                        Status = s.Status,
                        InteractionType = s.InteractionType,
                        Title = s.Title,
                        Description = s.Description,
                        InputSchema = s.InputSchema,
                        InputData = s.InputData,
                        OutputData = s.OutputData,
                        ContextData = s.ContextData,
                        TimeoutAt = s.TimeoutAt,
                        CreatedAt = s.CreatedAt,
                        CompletedAt = s.CompletedAt,
                        Metadata = s.Metadata
                    }).ToList(),
                    TotalCount = sessions.Count
                };

                return Success(response, "Active UI interactions retrieved successfully");
            }, "GetActiveUIInteractions");
        }

        /// <summary>
        /// Processes timed out interactions (admin endpoint)
        /// </summary>
        /// <returns>Success response</returns>
        [HttpPost("process-timeouts")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<string>>> ProcessTimedOutInteractions()
        {
            return await ExecuteActionAsync(async () =>
            {
                await _uiInteractionService.ProcessTimedOutInteractionsAsync(HttpContext.RequestAborted);
                return Success("processed", "Timed out interactions processed successfully");
            }, "ProcessTimedOutInteractions");
        }
    }

    /// <summary>
    /// Request model for cancelling UI interaction
    /// </summary>
    public class CancelUIInteractionRequest
    {
        public string? Reason { get; set; }
    }
}