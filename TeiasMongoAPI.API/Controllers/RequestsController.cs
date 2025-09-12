using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RequestsController : BaseController
    {
        private readonly IRequestService _requestService;

        public RequestsController(
            IRequestService requestService,
            ILogger<RequestsController> logger)
            : base(logger)
        {
            _requestService = requestService;
        }

        #region Basic CRUD Operations

        /// <summary>
        /// Get all requests with pagination
        /// </summary>
        [HttpGet]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> GetAll(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetAllAsync(pagination, cancellationToken);
            }, "Get all requests");
        }

        /// <summary>
        /// Get request by ID
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<RequestDetailDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetByIdAsync(id, cancellationToken);
            }, $"Get request {id}");
        }

        /// <summary>
        /// Create new request
        /// </summary>
        [HttpPost]
        [RequirePermission(UserPermissions.CreateRequests)]
        [AuditLog("CreateRequest")]
        public async Task<ActionResult<ApiResponse<RequestDto>>> Create(
            [FromBody] RequestCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<RequestDto>();
            if (validationResult != null) return validationResult;

            // Set the requesting user if not provided
            if (string.IsNullOrEmpty(dto.RequestedBy))
            {
                dto.RequestedBy = CurrentUserId?.ToString();
            }

            return await ExecuteAsync(async () =>
            {
                return await _requestService.CreateAsync(dto, CurrentUserId, cancellationToken);
            }, "Create request");
        }

        /// <summary>
        /// Update request
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.UpdateRequests)]
        [AuditLog("UpdateRequest")]
        public async Task<ActionResult<ApiResponse<RequestDto>>> Update(
            string id,
            [FromBody] RequestUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<RequestDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.UpdateAsync(id, dto, CurrentUserId, cancellationToken);
            }, $"Update request {id}");
        }

        /// <summary>
        /// Delete request
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.DeleteRequests)]
        [AuditLog("DeleteRequest")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.DeleteAsync(id, cancellationToken);
            }, $"Delete request {id}");
        }

        #endregion

        #region Request Filtering and Categorization

        /// <summary>
        /// Advanced request search
        /// </summary>
        [HttpPost("search")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> Search(
            [FromBody] RequestSearchDto searchDto,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.SearchAsync(searchDto, pagination, cancellationToken);
            }, "Search requests");
        }

        /// <summary>
        /// Get requests by type
        /// </summary>
        [HttpGet("by-type/{type}")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> GetByType(
            string type,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetByTypeAsync(type, pagination, cancellationToken);
            }, $"Get requests by type {type}");
        }

        /// <summary>
        /// Get requests by status
        /// </summary>
        [HttpGet("by-status/{status}")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> GetByStatus(
            string status,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetByStatusAsync(status, pagination, cancellationToken);
            }, $"Get requests by status {status}");
        }

        /// <summary>
        /// Get requests by priority
        /// </summary>
        [HttpGet("by-priority/{priority}")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> GetByPriority(
            string priority,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetByPriorityAsync(priority, pagination, cancellationToken);
            }, $"Get requests by priority {priority}");
        }

        /// <summary>
        /// Get requests by requester
        /// </summary>
        [HttpGet("by-requester/{userId}")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> GetByRequester(
            string userId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(userId, "userId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetByRequestedByAsync(userId, pagination, cancellationToken);
            }, $"Get requests by requester {userId}");
        }

        /// <summary>
        /// Get requests by assignee
        /// </summary>
        [HttpGet("by-assignee/{userId}")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> GetByAssignee(
            string userId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(userId, "userId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetByAssignedToAsync(userId, pagination, cancellationToken);
            }, $"Get requests by assignee {userId}");
        }

        /// <summary>
        /// Get requests by program
        /// </summary>
        [HttpGet("by-program/{programId}")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> GetByProgram(
            string programId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetByProgramIdAsync(programId, pagination, cancellationToken);
            }, $"Get requests by program {programId}");
        }

        /// <summary>
        /// Get unassigned requests
        /// </summary>
        [HttpGet("unassigned")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> GetUnassignedRequests(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetUnassignedRequestsAsync(pagination, cancellationToken);
            }, "Get unassigned requests");
        }

        #endregion

        #region Request Status and Assignment Management

        /// <summary>
        /// Update request status
        /// </summary>
        [HttpPut("{id}/status")]
        [RequirePermission(UserPermissions.UpdateRequests)]
        [AuditLog("UpdateRequestStatus")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateStatus(
            string id,
            [FromBody] RequestStatusUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.UpdateStatusAsync(id, dto, CurrentUserId, cancellationToken);
            }, $"Update status for request {id}");
        }

        /// <summary>
        /// Assign request
        /// </summary>
        [HttpPost("{id}/assign")]
        [RequirePermission(UserPermissions.AssignRequests)]
        [AuditLog("AssignRequest")]
        public async Task<ActionResult<ApiResponse<RequestDto>>> AssignRequest(
            string id,
            [FromBody] RequestAssignmentDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<RequestDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.AssignRequestAsync(id, dto, CurrentUserId, cancellationToken);
            }, $"Assign request {id}");
        }

        /// <summary>
        /// Unassign request
        /// </summary>
        [HttpPost("{id}/unassign")]
        [RequirePermission(UserPermissions.AssignRequests)]
        [AuditLog("UnassignRequest")]
        public async Task<ActionResult<ApiResponse<bool>>> UnassignRequest(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.UnassignRequestAsync(id, CurrentUserId, cancellationToken);
            }, $"Unassign request {id}");
        }

        /// <summary>
        /// Update request priority
        /// </summary>
        [HttpPut("{id}/priority")]
        [RequirePermission(UserPermissions.UpdateRequests)]
        [AuditLog("UpdateRequestPriority")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdatePriority(
            string id,
            [FromBody] RequestPriorityUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.UpdatePriorityAsync(id, dto, CurrentUserId, cancellationToken);
            }, $"Update priority for request {id}");
        }

        #endregion

        #region Request Response and Communication

        /// <summary>
        /// Add response to request
        /// </summary>
        [HttpPost("{id}/responses")]
        [RequirePermission(UserPermissions.UpdateRequests)]
        [AuditLog("AddRequestResponse")]
        public async Task<ActionResult<ApiResponse<RequestResponseDto>>> AddResponse(
            string id,
            [FromBody] RequestResponseCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<RequestResponseDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.AddResponseAsync(id, dto, CurrentUserId, cancellationToken);
            }, $"Add response to request {id}");
        }

        /// <summary>
        /// Get responses for request
        /// </summary>
        [HttpGet("{id}/responses")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<List<RequestResponseDto>>>> GetResponses(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetResponsesAsync(id, cancellationToken);
            }, $"Get responses for request {id}");
        }

        /// <summary>
        /// Update request response
        /// </summary>
        [HttpPut("{id}/responses/{responseId}")]
        [RequirePermission(UserPermissions.UpdateRequests)]
        [AuditLog("UpdateRequestResponse")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateResponse(
            string id,
            string responseId,
            [FromBody] RequestResponseUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var responseObjectIdResult = ParseObjectId(responseId, "responseId");
            if (responseObjectIdResult.Result != null) return responseObjectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.UpdateResponseAsync(id, responseId, dto, cancellationToken);
            }, $"Update response {responseId} for request {id}");
        }

        /// <summary>
        /// Delete request response
        /// </summary>
        [HttpDelete("{id}/responses/{responseId}")]
        [RequirePermission(UserPermissions.UpdateRequests)]
        [AuditLog("DeleteRequestResponse")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteResponse(
            string id,
            string responseId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            var responseObjectIdResult = ParseObjectId(responseId, "responseId");
            if (responseObjectIdResult.Result != null) return responseObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.DeleteResponseAsync(id, responseId, cancellationToken);
            }, $"Delete response {responseId} for request {id}");
        }

        #endregion

        #region Request Workflow Management

        /// <summary>
        /// Open request
        /// </summary>
        [HttpPost("{id}/open")]
        [RequirePermission(UserPermissions.UpdateRequests)]
        [AuditLog("OpenRequest")]
        public async Task<ActionResult<ApiResponse<bool>>> OpenRequest(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.OpenRequestAsync(id, CurrentUserId, cancellationToken);
            }, $"Open request {id}");
        }

        /// <summary>
        /// Start work on request
        /// </summary>
        [HttpPost("{id}/start-work")]
        [RequirePermission(UserPermissions.AssignRequests)]
        [AuditLog("StartWorkOnRequest")]
        public async Task<ActionResult<ApiResponse<bool>>> StartWorkOnRequest(
            string id,
            [FromBody] string assignedTo,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(assignedTo))
            {
                return ValidationError<bool>("Assigned to user ID is required");
            }

            var assignedToObjectIdResult = ParseObjectId(assignedTo, "assignedTo");
            if (assignedToObjectIdResult.Result != null) return assignedToObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.StartWorkOnRequestAsync(id, assignedTo, CurrentUserId, cancellationToken);
            }, $"Start work on request {id}");
        }

        /// <summary>
        /// Complete request
        /// </summary>
        [HttpPost("{id}/complete")]
        [RequirePermission(UserPermissions.UpdateRequests)]
        [AuditLog("CompleteRequest")]
        public async Task<ActionResult<ApiResponse<RequestDto>>> CompleteRequest(
            string id,
            [FromBody] RequestCompletionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<RequestDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.CompleteRequestAsync(id, dto, CurrentUserId, cancellationToken);
            }, $"Complete request {id}");
        }

        /// <summary>
        /// Reject request
        /// </summary>
        [HttpPost("{id}/reject")]
        [RequirePermission(UserPermissions.UpdateRequests)]
        [AuditLog("RejectRequest")]
        public async Task<ActionResult<ApiResponse<RequestDto>>> RejectRequest(
            string id,
            [FromBody] RequestRejectionDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<RequestDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.RejectRequestAsync(id, dto, CurrentUserId, cancellationToken);
            }, $"Reject request {id}");
        }

        /// <summary>
        /// Reopen request
        /// </summary>
        [HttpPost("{id}/reopen")]
        [RequirePermission(UserPermissions.UpdateRequests)]
        [AuditLog("ReopenRequest")]
        public async Task<ActionResult<ApiResponse<bool>>> ReopenRequest(
            string id,
            [FromBody] string reason,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(reason))
            {
                return ValidationError<bool>("Reason is required for reopening request");
            }

            return await ExecuteAsync(async () =>
            {
                return await _requestService.ReopenRequestAsync(id, reason, CurrentUserId, cancellationToken);
            }, $"Reopen request {id}");
        }

        #endregion

        #region Request Analytics and Reporting

        /// <summary>
        /// Get request statistics
        /// </summary>
        [HttpGet("stats")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<RequestStatsDto>>> GetRequestStats(
            [FromQuery] RequestStatsFilterDto? filter = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetRequestStatsAsync(filter, cancellationToken);
            }, "Get request statistics");
        }

        /// <summary>
        /// Get request trends
        /// </summary>
        [HttpGet("trends")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<List<RequestTrendDto>>>> GetRequestTrends(
            [FromQuery] int days = 30,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0)
            {
                return ValidationError<List<RequestTrendDto>>("Days must be greater than 0");
            }

            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetRequestTrendsAsync(days, cancellationToken);
            }, $"Get request trends for {days} days");
        }

        /// <summary>
        /// Get request metrics by type
        /// </summary>
        [HttpGet("metrics/by-type")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<List<RequestMetricDto>>>> GetRequestMetricsByType(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetRequestMetricsByTypeAsync(cancellationToken);
            }, "Get request metrics by type");
        }

        /// <summary>
        /// Get request metrics by status
        /// </summary>
        [HttpGet("metrics/by-status")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<List<RequestMetricDto>>>> GetRequestMetricsByStatus(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetRequestMetricsByStatusAsync(cancellationToken);
            }, "Get request metrics by status");
        }

        /// <summary>
        /// Get assignee performance
        /// </summary>
        [HttpGet("performance")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<List<RequestPerformanceDto>>>> GetAssigneePerformance(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetAssigneePerformanceAsync(cancellationToken);
            }, "Get assignee performance");
        }

        #endregion

        #region Request Templates and Categories

        /// <summary>
        /// Get request templates
        /// </summary>
        [HttpGet("templates")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<List<RequestTemplateDto>>>> GetRequestTemplates(
            [FromQuery] string? type = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetRequestTemplatesAsync(type, cancellationToken);
            }, "Get request templates");
        }

        /// <summary>
        /// Create request template
        /// </summary>
        [HttpPost("templates")]
        [RequirePermission(UserPermissions.CreateRequests)]
        [AuditLog("CreateRequestTemplate")]
        public async Task<ActionResult<ApiResponse<RequestTemplateDto>>> CreateRequestTemplate(
            [FromBody] RequestTemplateCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<RequestTemplateDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.CreateRequestTemplateAsync(dto, CurrentUserId, cancellationToken);
            }, "Create request template");
        }

        /// <summary>
        /// Create request from template
        /// </summary>
        [HttpPost("templates/{templateId}/create")]
        [RequirePermission(UserPermissions.CreateRequests)]
        [AuditLog("CreateRequestFromTemplate")]
        public async Task<ActionResult<ApiResponse<RequestDto>>> CreateFromTemplate(
            string templateId,
            [FromBody] RequestFromTemplateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(templateId, "templateId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<RequestDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.CreateFromTemplateAsync(templateId, dto, CurrentUserId, cancellationToken);
            }, $"Create request from template {templateId}");
        }

        #endregion

        #region Request Notifications and Subscriptions

        /// <summary>
        /// Subscribe to request updates
        /// </summary>
        [HttpPost("{id}/subscribe")]
        [RequirePermission(UserPermissions.ViewRequests)]
        [AuditLog("SubscribeToRequestUpdates")]
        public async Task<ActionResult<ApiResponse<bool>>> SubscribeToRequestUpdates(
            string id,
            [FromBody] string userId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return ValidationError<bool>("User ID is required");
            }

            var userObjectIdResult = ParseObjectId(userId, "userId");
            if (userObjectIdResult.Result != null) return userObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.SubscribeToRequestUpdatesAsync(id, userId, cancellationToken);
            }, $"Subscribe user {userId} to request {id} updates");
        }

        /// <summary>
        /// Unsubscribe from request updates
        /// </summary>
        [HttpPost("{id}/unsubscribe")]
        [RequirePermission(UserPermissions.ViewRequests)]
        [AuditLog("UnsubscribeFromRequestUpdates")]
        public async Task<ActionResult<ApiResponse<bool>>> UnsubscribeFromRequestUpdates(
            string id,
            [FromBody] string userId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return ValidationError<bool>("User ID is required");
            }

            var userObjectIdResult = ParseObjectId(userId, "userId");
            if (userObjectIdResult.Result != null) return userObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.UnsubscribeFromRequestUpdatesAsync(id, userId, cancellationToken);
            }, $"Unsubscribe user {userId} from request {id} updates");
        }

        /// <summary>
        /// Get request subscribers
        /// </summary>
        [HttpGet("{id}/subscribers")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetRequestSubscribers(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetRequestSubscribersAsync(id, cancellationToken);
            }, $"Get subscribers for request {id}");
        }

        #endregion

        #region Request Validation and Rules

        /// <summary>
        /// Validate request
        /// </summary>
        [HttpPost("validate")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<RequestValidationResult>>> ValidateRequest(
            [FromBody] RequestCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<RequestValidationResult>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.ValidateRequestAsync(dto, cancellationToken);
            }, "Validate request");
        }

        /// <summary>
        /// Check if user can modify request
        /// </summary>
        [HttpGet("{id}/can-modify")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<bool>>> CanUserModifyRequest(
            string id,
            [FromQuery] string userId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return ValidationError<bool>("User ID is required");
            }

            var userObjectIdResult = ParseObjectId(userId, "userId");
            if (userObjectIdResult.Result != null) return userObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _requestService.CanUserModifyRequestAsync(id, userId, cancellationToken);
            }, $"Check if user {userId} can modify request {id}");
        }

        #endregion

        #region Metadata

        /// <summary>
        /// Get available request types
        /// </summary>
        [HttpGet("types")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetAvailableRequestTypes(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetAvailableRequestTypesAsync(cancellationToken);
            }, "Get available request types");
        }

        /// <summary>
        /// Get available request statuses
        /// </summary>
        [HttpGet("statuses")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetAvailableRequestStatuses(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetAvailableRequestStatusesAsync(cancellationToken);
            }, "Get available request statuses");
        }

        /// <summary>
        /// Get available request priorities
        /// </summary>
        [HttpGet("priorities")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetAvailableRequestPriorities(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetAvailableRequestPrioritiesAsync(cancellationToken);
            }, "Get available request priorities");
        }

        #endregion

        #region Additional Utility Methods

        /// <summary>
        /// Get my requests (current user)
        /// </summary>
        [HttpGet("my-requests")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> GetMyRequests(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                var userId = CurrentUserId?.ToString();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                return await _requestService.GetByRequestedByAsync(userId, pagination, cancellationToken);
            }, "Get my requests");
        }

        /// <summary>
        /// Get my assigned requests (current user)
        /// </summary>
        [HttpGet("my-assignments")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> GetMyAssignments(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                var userId = CurrentUserId?.ToString();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                return await _requestService.GetByAssignedToAsync(userId, pagination, cancellationToken);
            }, "Get my assigned requests");
        }

        /// <summary>
        /// Get recent requests
        /// </summary>
        [HttpGet("recent")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<List<RequestListDto>>>> GetRecentRequests(
            [FromQuery] int count = 10,
            CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return ValidationError<List<RequestListDto>>("Count must be greater than 0");
            }

            return await ExecuteAsync(async () =>
            {
                var pagination = new PaginationRequestDto { PageNumber = 1, PageSize = count };
                var result = await _requestService.GetAllAsync(pagination, cancellationToken);
                return result.Items?.ToList() ?? new List<RequestListDto>();
            }, $"Get recent requests (count: {count})");
        }

        /// <summary>
        /// Get priority requests
        /// </summary>
        [HttpGet("priority")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> GetPriorityRequests(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _requestService.GetByPriorityAsync("high", pagination, cancellationToken);
            }, "Get priority requests");
        }

        /// <summary>
        /// Get overdue requests
        /// </summary>
        [HttpGet("overdue")]
        [RequirePermission(UserPermissions.ViewRequests)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestListDto>>>> GetOverdueRequests(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                // Filter for overdue requests using search
                var searchDto = new RequestSearchDto
                {
                    Status = "in_progress",
                    // Add logic to filter by overdue criteria in the service
                };
                return await _requestService.SearchAsync(searchDto, pagination, cancellationToken);
            }, "Get overdue requests");
        }

        /// <summary>
        /// Bulk update request status
        /// </summary>
        [HttpPut("bulk-status")]
        [RequirePermission(UserPermissions.UpdateRequests)]
        [AuditLog("BulkUpdateRequestStatus")]
        public async Task<ActionResult<ApiResponse<bool>>> BulkUpdateStatus(
            [FromBody] BulkRequestStatusUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            if (dto.RequestIds == null || !dto.RequestIds.Any())
            {
                return ValidationError<bool>("At least one request ID is required");
            }

            // Validate all request IDs
            foreach (var requestId in dto.RequestIds)
            {
                var objectIdResult = ParseObjectId(requestId);
                if (objectIdResult.Result != null) return objectIdResult.Result!;
            }

            return await ExecuteAsync(async () =>
            {
                int successCount = 0;
                foreach (var requestId in dto.RequestIds)
                {
                    var updateDto = new RequestStatusUpdateDto
                    {
                        Status = dto.Status,
                        Reason = dto.Reason
                    };

                    var success = await _requestService.UpdateStatusAsync(requestId, updateDto, CurrentUserId, cancellationToken);
                    if (success) successCount++;
                }

                return successCount == dto.RequestIds.Count;
            }, $"Bulk update status for {dto.RequestIds.Count} requests");
        }

        #endregion
    }
}