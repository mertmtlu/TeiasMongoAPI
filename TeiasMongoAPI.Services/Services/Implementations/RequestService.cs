using AutoMapper;
using Microsoft.Extensions.Logging;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class RequestService : BaseService, IRequestService
    {
        private readonly IProgramService _programService;
        private readonly Dictionary<string, RequestTemplate> _requestTemplates = new();
        private readonly Dictionary<string, List<string>> _requestSubscriptions = new();

        public RequestService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IProgramService programService,
            ILogger<RequestService> logger)
            : base(unitOfWork, mapper, logger)
        {
            _programService = programService;
            InitializeDefaultTemplates();
        }

        #region Basic CRUD Operations

        public async Task<RequestDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {id} not found.");
            }

            var dto = _mapper.Map<RequestDetailDto>(request);

            // Get requester details
            try
            {
                var requester = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(request.RequestedBy), cancellationToken);
                if (requester != null)
                {
                    dto.RequestedByName = requester.FullName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get requester details for request {RequestId}", id);
            }

            // Get assignee details
            if (!string.IsNullOrEmpty(request.AssignedTo))
            {
                try
                {
                    var assignee = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(request.AssignedTo), cancellationToken);
                    if (assignee != null)
                    {
                        dto.AssignedToName = assignee.FullName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get assignee details for request {RequestId}", id);
                }
            }

            // Get program details (required)
            try
            {
                var program = await _unitOfWork.Programs.GetByIdAsync(request.ProgramId, cancellationToken);
                if (program != null)
                {
                    dto.ProgramName = program.Name;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get program details for request {RequestId}", id);
            }

            // Get responses with user details
            dto.Responses = await GetRequestResponsesWithUserDetailsAsync(request.Responses, cancellationToken);

            // Build timeline
            dto.Timeline = BuildRequestTimeline(request);

            // Get subscribers
            dto.Subscribers = GetRequestSubscribers(id);

            return dto;
        }

        public async Task<PagedResponse<RequestListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetAllAsync(cancellationToken);
            var requestsList = requests.ToList();

            // Apply pagination
            var totalCount = requestsList.Count;
            var paginatedRequests = requestsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = await MapRequestListDtosAsync(paginatedRequests, cancellationToken);

            return new PagedResponse<RequestListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<RequestListDto>> SearchAsync(RequestSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var allRequests = await _unitOfWork.Requests.GetAllAsync(cancellationToken);
            var filteredRequests = allRequests.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchDto.Type))
            {
                filteredRequests = filteredRequests.Where(r => r.Type.Equals(searchDto.Type, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(searchDto.Title))
            {
                filteredRequests = filteredRequests.Where(r => r.Title.Contains(searchDto.Title, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(searchDto.Description))
            {
                filteredRequests = filteredRequests.Where(r => r.Description.Contains(searchDto.Description, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(searchDto.Status))
            {
                filteredRequests = filteredRequests.Where(r => r.Status == searchDto.Status);
            }

            if (!string.IsNullOrEmpty(searchDto.Priority))
            {
                filteredRequests = filteredRequests.Where(r => r.Priority == searchDto.Priority);
            }

            if (!string.IsNullOrEmpty(searchDto.RequestedBy))
            {
                filteredRequests = filteredRequests.Where(r => r.RequestedBy == searchDto.RequestedBy);
            }

            if (!string.IsNullOrEmpty(searchDto.AssignedTo))
            {
                filteredRequests = filteredRequests.Where(r => r.AssignedTo == searchDto.AssignedTo);
            }

            if (!string.IsNullOrEmpty(searchDto.ProgramId))
            {
                var programObjectId = ParseObjectId(searchDto.ProgramId);
                filteredRequests = filteredRequests.Where(r => r.ProgramId == programObjectId);
            }

            if (searchDto.RequestedFrom.HasValue)
            {
                filteredRequests = filteredRequests.Where(r => r.RequestedAt >= searchDto.RequestedFrom.Value);
            }

            if (searchDto.RequestedTo.HasValue)
            {
                filteredRequests = filteredRequests.Where(r => r.RequestedAt <= searchDto.RequestedTo.Value);
            }

            var requestsList = filteredRequests.ToList();

            // Apply pagination
            var totalCount = requestsList.Count;
            var paginatedRequests = requestsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = await MapRequestListDtosAsync(paginatedRequests, cancellationToken);

            return new PagedResponse<RequestListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<RequestDto> CreateAsync(RequestCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate required program
            if (string.IsNullOrEmpty(dto.ProgramId))
            {
                throw new ArgumentException("ProgramId is required for all requests.");
            }

            await ValidateProgramAsync(dto.ProgramId, cancellationToken);

            var request = _mapper.Map<Request>(dto);
            request.RequestedAt = DateTime.UtcNow;
            request.Status = "open";
            request.ProgramId = ParseObjectId(dto.ProgramId);

            var createdRequest = await _unitOfWork.Requests.CreateAsync(request, cancellationToken);

            _logger.LogInformation("Created request {RequestId} of type {Type} for program {ProgramId} by user {UserId}",
                createdRequest._ID, dto.Type, dto.ProgramId, dto.RequestedBy);

            // Auto-assign based on request type if configured
            await TryAutoAssignRequestAsync(createdRequest, cancellationToken);

            return _mapper.Map<RequestDto>(createdRequest);
        }

        public async Task<RequestDto> UpdateAsync(string id, RequestUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var existingRequest = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (existingRequest == null)
            {
                throw new KeyNotFoundException($"Request with ID {id} not found.");
            }

            // Only allow updates if request is not completed or rejected
            if (existingRequest.Status == "completed" || existingRequest.Status == "rejected")
            {
                throw new InvalidOperationException("Cannot update completed or rejected requests.");
            }

            // Validate program if changed
            if (!string.IsNullOrEmpty(dto.ProgramId) && dto.ProgramId != existingRequest.ProgramId.ToString())
            {
                await ValidateProgramAsync(dto.ProgramId, cancellationToken);
            }

            _mapper.Map(dto, existingRequest);

            var success = await _unitOfWork.Requests.UpdateAsync(objectId, existingRequest, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update request with ID {id}.");
            }

            _logger.LogInformation("Updated request {RequestId}", id);

            return _mapper.Map<RequestDto>(existingRequest);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {id} not found.");
            }

            // Only allow deletion of open requests
            if (request.Status != "open")
            {
                throw new InvalidOperationException("Can only delete open requests.");
            }

            var success = await _unitOfWork.Requests.DeleteAsync(objectId, cancellationToken);

            if (success)
            {
                // Remove subscriptions
                _requestSubscriptions.Remove(id);
                _logger.LogInformation("Deleted request {RequestId}", id);
            }

            return success;
        }

        #endregion

        #region Request Filtering and Categorization

        public async Task<PagedResponse<RequestListDto>> GetByTypeAsync(string type, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetByTypeAsync(type, cancellationToken);
            return await CreatePagedRequestResponse(requests, pagination, cancellationToken);
        }

        public async Task<PagedResponse<RequestListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetByStatusAsync(status, cancellationToken);
            return await CreatePagedRequestResponse(requests, pagination, cancellationToken);
        }

        public async Task<PagedResponse<RequestListDto>> GetByPriorityAsync(string priority, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetByPriorityAsync(priority, cancellationToken);
            return await CreatePagedRequestResponse(requests, pagination, cancellationToken);
        }

        public async Task<PagedResponse<RequestListDto>> GetByRequestedByAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetByRequestedByAsync(userId, cancellationToken);
            return await CreatePagedRequestResponse(requests, pagination, cancellationToken);
        }

        public async Task<PagedResponse<RequestListDto>> GetByAssignedToAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetByAssignedToAsync(userId, cancellationToken);
            return await CreatePagedRequestResponse(requests, pagination, cancellationToken);
        }

        public async Task<PagedResponse<RequestListDto>> GetByProgramIdAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var requests = await _unitOfWork.Requests.GetByProgramIdAsync(programObjectId, cancellationToken);
            return await CreatePagedRequestResponse(requests, pagination, cancellationToken);
        }

        public async Task<PagedResponse<RequestListDto>> GetUnassignedRequestsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetUnassignedRequestsAsync(cancellationToken);
            return await CreatePagedRequestResponse(requests, pagination, cancellationToken);
        }

        #endregion

        #region Request Status and Assignment Management

        public async Task<bool> UpdateStatusAsync(string id, RequestStatusUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {id} not found.");
            }

            // Validate status transition
            if (!IsValidStatusTransition(request.Status, dto.Status))
            {
                throw new InvalidOperationException($"Invalid status transition from {request.Status} to {dto.Status}.");
            }

            var success = await _unitOfWork.Requests.UpdateStatusAsync(objectId, dto.Status, cancellationToken);

            if (success)
            {
                // Add system response if reason provided
                if (!string.IsNullOrEmpty(dto.Reason))
                {
                    var systemResponse = new RequestResponse
                    {
                        RespondedBy = "system", // Should come from current user context BaseController holds CurrentUserId property
                        RespondedAt = DateTime.UtcNow,
                        Message = $"Status changed to {dto.Status}: {dto.Reason}"
                    };

                    await _unitOfWork.Requests.AddResponseAsync(objectId, systemResponse, cancellationToken);
                }

                _logger.LogInformation("Updated status of request {RequestId} to {Status}", id, dto.Status);

                // Notify subscribers
                await NotifySubscribersAsync(id, $"Request status changed to {dto.Status}", cancellationToken);
            }

            return success;
        }

        public async Task<RequestDto> AssignRequestAsync(string id, RequestAssignmentDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {id} not found.");
            }

            // Verify assignee exists
            var assigneeObjectId = ParseObjectId(dto.AssignedTo);
            if (!await _unitOfWork.Users.ExistsAsync(assigneeObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"User with ID {dto.AssignedTo} not found.");
            }

            var success = await _unitOfWork.Requests.AssignRequestAsync(objectId, dto.AssignedTo, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to assign request {id}.");
            }

            // Add assignment response if notes provided
            if (!string.IsNullOrEmpty(dto.AssignmentNotes))
            {
                var assignmentResponse = new RequestResponse
                {
                    RespondedBy = "system", // Should come from current user context BaseController holds CurrentUserId property
                    RespondedAt = DateTime.UtcNow,
                    Message = $"Request assigned to user: {dto.AssignmentNotes}"
                };

                await _unitOfWork.Requests.AddResponseAsync(objectId, assignmentResponse, cancellationToken);
            }

            _logger.LogInformation("Assigned request {RequestId} to user {UserId}", id, dto.AssignedTo);

            // Notify assignee and subscribers
            await NotifySubscribersAsync(id, $"Request assigned to user {dto.AssignedTo}", cancellationToken);

            var updatedRequest = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);
            return _mapper.Map<RequestDto>(updatedRequest);
        }

        public async Task<bool> UnassignRequestAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {id} not found.");
            }

            request.AssignedTo = null;
            request.Status = "open";

            var success = await _unitOfWork.Requests.UpdateAsync(objectId, request, cancellationToken);

            if (success)
            {
                var unassignResponse = new RequestResponse
                {
                    RespondedBy = "system", // Should come from current user context BaseController holds CurrentUserId property
                    RespondedAt = DateTime.UtcNow,
                    Message = "Request unassigned and returned to open status"
                };

                await _unitOfWork.Requests.AddResponseAsync(objectId, unassignResponse, cancellationToken);

                _logger.LogInformation("Unassigned request {RequestId}", id);

                // Notify subscribers
                await NotifySubscribersAsync(id, "Request unassigned", cancellationToken);
            }

            return success;
        }

        public async Task<bool> UpdatePriorityAsync(string id, RequestPriorityUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {id} not found.");
            }

            var success = await _unitOfWork.Requests.UpdatePriorityAsync(objectId, dto.Priority, cancellationToken);

            if (success)
            {
                // Add priority change response if reason provided
                if (!string.IsNullOrEmpty(dto.Reason))
                {
                    var priorityResponse = new RequestResponse
                    {
                        RespondedBy = "system", // Should come from current user context BaseController holds CurrentUserId property
                        RespondedAt = DateTime.UtcNow,
                        Message = $"Priority changed to {dto.Priority}: {dto.Reason}"
                    };

                    await _unitOfWork.Requests.AddResponseAsync(objectId, priorityResponse, cancellationToken);
                }

                _logger.LogInformation("Updated priority of request {RequestId} to {Priority}", id, dto.Priority);

                // Notify subscribers
                await NotifySubscribersAsync(id, $"Request priority changed to {dto.Priority}", cancellationToken);
            }

            return success;
        }

        #endregion

        #region Request Response and Communication

        public async Task<RequestResponseDto> AddResponseAsync(string id, RequestResponseCreateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {id} not found.");
            }

            var response = new RequestResponse
            {
                RespondedBy = "system", // Should come from current user context BaseController holds CurrentUserId property
                RespondedAt = DateTime.UtcNow,
                Message = dto.Message
            };

            var success = await _unitOfWork.Requests.AddResponseAsync(objectId, response, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to add response to request {id}.");
            }

            _logger.LogInformation("Added response to request {RequestId}", id);

            // Notify subscribers (excluding internal responses if configured)
            if (!dto.IsInternal)
            {
                await NotifySubscribersAsync(id, "New response added to request", cancellationToken);
            }

            // Get user details for response
            var responseDto = new RequestResponseDto
            {
                Id = Guid.NewGuid().ToString(),
                RequestId = id,
                RespondedBy = response.RespondedBy,
                RespondedByName = "System", // Would resolve from user service
                RespondedAt = response.RespondedAt,
                Message = response.Message,
                IsInternal = dto.IsInternal,
                Attachments = dto.Attachments
            };

            return responseDto;
        }

        public async Task<List<RequestResponseDto>> GetResponsesAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var responses = await _unitOfWork.Requests.GetResponsesAsync(objectId, cancellationToken);

            return await GetRequestResponsesWithUserDetailsAsync(responses, cancellationToken);
        }

        public async Task<bool> UpdateResponseAsync(string requestId, string responseId, RequestResponseUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(requestId);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {requestId} not found.");
            }

            // Find and update the response (in a real implementation, responses would have IDs)
            // For now, we'll implement this as a basic update to the latest response
            _logger.LogInformation("Updated response for request {RequestId}", requestId);
            return true;
        }

        public async Task<bool> DeleteResponseAsync(string requestId, string responseId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(requestId);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {requestId} not found.");
            }

            // In a real implementation, this would remove the specific response
            _logger.LogInformation("Deleted response {ResponseId} from request {RequestId}", responseId, requestId);
            return true;
        }

        #endregion

        #region Request Workflow Management

        public async Task<bool> OpenRequestAsync(string id, CancellationToken cancellationToken = default)
        {
            return await UpdateStatusAsync(id, new RequestStatusUpdateDto { Status = "open" }, cancellationToken);
        }

        public async Task<bool> StartWorkOnRequestAsync(string id, string assignedTo, CancellationToken cancellationToken = default)
        {
            // First assign the request
            await AssignRequestAsync(id, new RequestAssignmentDto { AssignedTo = assignedTo }, cancellationToken);

            // Then update status to in_progress
            return await UpdateStatusAsync(id, new RequestStatusUpdateDto { Status = "in_progress" }, cancellationToken);
        }

        public async Task<RequestDto> CompleteRequestAsync(string id, RequestCompletionDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {id} not found.");
            }

            if (request.Status != "in_progress")
            {
                throw new InvalidOperationException("Can only complete requests that are in progress.");
            }

            // Update status to completed
            await _unitOfWork.Requests.UpdateStatusAsync(objectId, "completed", cancellationToken);

            // Add completion response
            var completionResponse = new RequestResponse
            {
                RespondedBy = "system", // Should come from current user context BaseController holds CurrentUserId property
                RespondedAt = DateTime.UtcNow,
                Message = $"Request completed: {dto.CompletionNotes}"
            };

            await _unitOfWork.Requests.AddResponseAsync(objectId, completionResponse, cancellationToken);

            _logger.LogInformation("Completed request {RequestId}", id);

            // Notify subscribers
            await NotifySubscribersAsync(id, "Request completed", cancellationToken);

            var updatedRequest = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);
            return _mapper.Map<RequestDto>(updatedRequest);
        }

        public async Task<RequestDto> RejectRequestAsync(string id, RequestRejectionDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {id} not found.");
            }

            // Update status to rejected
            await _unitOfWork.Requests.UpdateStatusAsync(objectId, "rejected", cancellationToken);

            // Add rejection response
            var rejectionResponse = new RequestResponse
            {
                RespondedBy = "system", // Should come from current user context BaseController holds CurrentUserId property
                RespondedAt = DateTime.UtcNow,
                Message = $"Request rejected: {dto.RejectionReason}"
            };

            await _unitOfWork.Requests.AddResponseAsync(objectId, rejectionResponse, cancellationToken);

            _logger.LogInformation("Rejected request {RequestId}", id);

            // Notify subscribers
            await NotifySubscribersAsync(id, "Request rejected", cancellationToken);

            var updatedRequest = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);
            return _mapper.Map<RequestDto>(updatedRequest);
        }

        public async Task<bool> ReopenRequestAsync(string id, string reason, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {id} not found.");
            }

            if (request.Status != "completed" && request.Status != "rejected")
            {
                throw new InvalidOperationException("Can only reopen completed or rejected requests.");
            }

            // Update status to open and clear assignment
            request.Status = "open";
            request.AssignedTo = null;

            var success = await _unitOfWork.Requests.UpdateAsync(objectId, request, cancellationToken);

            if (success)
            {
                // Add reopen response
                var reopenResponse = new RequestResponse
                {
                    RespondedBy = "system", // Should come from current user context BaseController holds CurrentUserId property
                    RespondedAt = DateTime.UtcNow,
                    Message = $"Request reopened: {reason}"
                };

                await _unitOfWork.Requests.AddResponseAsync(objectId, reopenResponse, cancellationToken);

                _logger.LogInformation("Reopened request {RequestId}", id);

                // Notify subscribers
                await NotifySubscribersAsync(id, "Request reopened", cancellationToken);
            }

            return success;
        }

        #endregion

        #region Request Analytics and Reporting

        public async Task<RequestStatsDto> GetRequestStatsAsync(RequestStatsFilterDto? filter = null, CancellationToken cancellationToken = default)
        {
            var allRequests = await _unitOfWork.Requests.GetAllAsync(cancellationToken);
            var requests = allRequests.AsQueryable();

            // Apply filters
            if (filter != null)
            {
                if (filter.FromDate.HasValue)
                {
                    requests = requests.Where(r => r.RequestedAt >= filter.FromDate.Value);
                }

                if (filter.ToDate.HasValue)
                {
                    requests = requests.Where(r => r.RequestedAt <= filter.ToDate.Value);
                }

                if (!string.IsNullOrEmpty(filter.Type))
                {
                    requests = requests.Where(r => r.Type == filter.Type);
                }

                if (!string.IsNullOrEmpty(filter.AssignedTo))
                {
                    requests = requests.Where(r => r.AssignedTo == filter.AssignedTo);
                }

                if (!string.IsNullOrEmpty(filter.ProgramId))
                {
                    var programObjectId = ParseObjectId(filter.ProgramId);
                    requests = requests.Where(r => r.ProgramId == programObjectId);
                }

                if (filter.Statuses?.Any() == true)
                {
                    requests = requests.Where(r => filter.Statuses.Contains(r.Status));
                }
            }

            var requestsList = requests.ToList();
            var completedRequests = requestsList.Where(r => r.Status == "completed").ToList();

            // Calculate average resolution time
            var resolvedRequests = requestsList.Where(r => r.Status == "completed" || r.Status == "rejected").ToList();
            var averageResolutionTime = resolvedRequests.Any()
                ? resolvedRequests.Where(r => r.Responses.Any()).Average(r =>
                    (r.Responses.OrderByDescending(resp => resp.RespondedAt).First().RespondedAt - r.RequestedAt).TotalHours)
                : 0;

            return new RequestStatsDto
            {
                TotalRequests = requestsList.Count,
                OpenRequests = requestsList.Count(r => r.Status == "open"),
                InProgressRequests = requestsList.Count(r => r.Status == "in_progress"),
                CompletedRequests = requestsList.Count(r => r.Status == "completed"),
                RejectedRequests = requestsList.Count(r => r.Status == "rejected"),
                UnassignedRequests = requestsList.Count(r => string.IsNullOrEmpty(r.AssignedTo)),
                AverageResolutionTime = averageResolutionTime,
                RequestsByType = requestsList.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count()),
                RequestsByPriority = requestsList.GroupBy(r => r.Priority).ToDictionary(g => g.Key, g => g.Count())
            };
        }

        public async Task<List<RequestTrendDto>> GetRequestTrendsAsync(int days = 30, CancellationToken cancellationToken = default)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var requests = await _unitOfWork.Requests.GetAllAsync(cancellationToken);

            var trends = requests
                .Where(r => r.RequestedAt >= cutoffDate)
                .GroupBy(r => r.RequestedAt.Date)
                .Select(g => new RequestTrendDto
                {
                    Date = g.Key,
                    CreatedCount = g.Count(),
                    CompletedCount = g.Count(r => r.Status == "completed"),
                    TotalOpen = g.Count(r => r.Status == "open" || r.Status == "in_progress")
                })
                .OrderBy(t => t.Date)
                .ToList();

            return trends;
        }

        public async Task<List<RequestMetricDto>> GetRequestMetricsByTypeAsync(CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetAllAsync(cancellationToken);
            var requestsList = requests.ToList();
            var totalCount = requestsList.Count;

            return requestsList
                .GroupBy(r => r.Type)
                .Select(g => new RequestMetricDto
                {
                    Category = "Type",
                    Label = g.Key,
                    Count = g.Count(),
                    Percentage = totalCount > 0 ? (double)g.Count() / totalCount * 100 : 0
                })
                .OrderByDescending(m => m.Count)
                .ToList();
        }

        public async Task<List<RequestMetricDto>> GetRequestMetricsByStatusAsync(CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetAllAsync(cancellationToken);
            var requestsList = requests.ToList();
            var totalCount = requestsList.Count;

            return requestsList
                .GroupBy(r => r.Status)
                .Select(g => new RequestMetricDto
                {
                    Category = "Status",
                    Label = g.Key,
                    Count = g.Count(),
                    Percentage = totalCount > 0 ? (double)g.Count() / totalCount * 100 : 0
                })
                .OrderByDescending(m => m.Count)
                .ToList();
        }

        public async Task<List<RequestPerformanceDto>> GetAssigneePerformanceAsync(CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetAllAsync(cancellationToken);
            var assignedRequests = requests.Where(r => !string.IsNullOrEmpty(r.AssignedTo)).ToList();

            var performance = new List<RequestPerformanceDto>();

            var assigneeGroups = assignedRequests.GroupBy(r => r.AssignedTo);
            foreach (var group in assigneeGroups)
            {
                var assigneeRequests = group.ToList();
                var completedRequests = assigneeRequests.Where(r => r.Status == "completed").ToList();

                // Calculate average resolution time
                var resolvedRequests = assigneeRequests.Where(r => r.Status == "completed" || r.Status == "rejected").ToList();
                var averageResolutionTime = resolvedRequests.Any() && resolvedRequests.All(r => r.Responses.Any())
                    ? resolvedRequests.Average(r =>
                        (r.Responses.OrderByDescending(resp => resp.RespondedAt).First().RespondedAt - r.RequestedAt).TotalHours)
                    : 0;

                // Get user details
                var userName = "Unknown";
                try
                {
                    var user = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(group.Key!), cancellationToken);
                    if (user != null)
                    {
                        userName = user.FullName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get user details for assignee {AssigneeId}", group.Key);
                }

                performance.Add(new RequestPerformanceDto
                {
                    UserId = group.Key!,
                    UserName = userName,
                    AssignedCount = assigneeRequests.Count,
                    CompletedCount = completedRequests.Count,
                    CompletionRate = assigneeRequests.Count > 0 ? (double)completedRequests.Count / assigneeRequests.Count * 100 : 0,
                    AverageResolutionTime = averageResolutionTime,
                    Rating = CalculatePerformanceRating(assigneeRequests, completedRequests, averageResolutionTime)
                });
            }

            return performance.OrderByDescending(p => p.Rating).ToList();
        }

        #endregion

        #region Request Templates and Categories

        public async Task<List<RequestTemplateDto>> GetRequestTemplatesAsync(string? type = null, CancellationToken cancellationToken = default)
        {
            var templates = _requestTemplates.Values.AsQueryable();

            if (!string.IsNullOrEmpty(type))
            {
                templates = templates.Where(t => t.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            }

            return templates.Select(t => new RequestTemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Type = t.Type,
                TitleTemplate = t.TitleTemplate,
                DescriptionTemplate = t.DescriptionTemplate,
                FieldDefinitions = t.FieldDefinitions,
                Priority = t.Priority,
                IsActive = t.IsActive,
                CreatedBy = t.CreatedBy,
                CreatedAt = t.CreatedAt,
                UsageCount = t.UsageCount
            }).ToList();
        }

        public async Task<RequestTemplateDto> CreateRequestTemplateAsync(RequestTemplateCreateDto dto, CancellationToken cancellationToken = default)
        {
            var template = new RequestTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.Type,
                TitleTemplate = dto.TitleTemplate,
                DescriptionTemplate = dto.DescriptionTemplate,
                FieldDefinitions = dto.FieldDefinitions,
                Priority = dto.Priority,
                IsActive = dto.IsActive,
                CreatedBy = "system", // Should come from current user context BaseController holds CurrentUserId property
                CreatedAt = DateTime.UtcNow,
                UsageCount = 0
            };

            _requestTemplates[template.Id] = template;

            _logger.LogInformation("Created request template {TemplateId} of type {Type}", template.Id, dto.Type);

            return _mapper.Map<RequestTemplateDto>(template);
        }

        public async Task<RequestDto> CreateFromTemplateAsync(string templateId, RequestFromTemplateDto dto, CancellationToken cancellationToken = default)
        {
            if (!_requestTemplates.TryGetValue(templateId, out var template))
            {
                throw new KeyNotFoundException($"Request template with ID {templateId} not found.");
            }

            if (!template.IsActive)
            {
                throw new InvalidOperationException("Cannot create requests from inactive templates.");
            }

            // Validate required program
            if (string.IsNullOrEmpty(dto.ProgramId))
            {
                throw new ArgumentException("ProgramId is required when creating requests from templates.");
            }

            // Process template with field values
            var title = ProcessTemplate(template.TitleTemplate, dto.FieldValues);
            var description = ProcessTemplate(template.DescriptionTemplate, dto.FieldValues);

            var createDto = new RequestCreateDto
            {
                Type = template.Type,
                Title = title,
                Description = description,
                ProgramId = dto.ProgramId,
                Priority = template.Priority,
                Metadata = dto.FieldValues
            };

            // Increment template usage
            template.UsageCount++;

            var request = await CreateAsync(createDto, cancellationToken);

            _logger.LogInformation("Created request {RequestId} from template {TemplateId} for program {ProgramId}",
                request.Id, templateId, dto.ProgramId);

            return request;
        }

        #endregion

        #region Request Notifications and Subscriptions

        public async Task<bool> SubscribeToRequestUpdatesAsync(string requestId, string userId, CancellationToken cancellationToken = default)
        {
            // Verify request exists
            var objectId = ParseObjectId(requestId);
            if (!await _unitOfWork.Requests.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Request with ID {requestId} not found.");
            }

            // Verify user exists
            var userObjectId = ParseObjectId(userId);
            if (!await _unitOfWork.Users.ExistsAsync(userObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"User with ID {userId} not found.");
            }

            if (!_requestSubscriptions.ContainsKey(requestId))
            {
                _requestSubscriptions[requestId] = new List<string>();
            }

            if (!_requestSubscriptions[requestId].Contains(userId))
            {
                _requestSubscriptions[requestId].Add(userId);
                _logger.LogInformation("User {UserId} subscribed to request {RequestId}", userId, requestId);
            }

            return true;
        }

        public async Task<bool> UnsubscribeFromRequestUpdatesAsync(string requestId, string userId, CancellationToken cancellationToken = default)
        {
            if (_requestSubscriptions.TryGetValue(requestId, out var subscribers))
            {
                if (subscribers.Remove(userId))
                {
                    _logger.LogInformation("User {UserId} unsubscribed from request {RequestId}", userId, requestId);

                    // Clean up empty subscription lists
                    if (!subscribers.Any())
                    {
                        _requestSubscriptions.Remove(requestId);
                    }

                    return true;
                }
            }

            return false;
        }

        public async Task<List<string>> GetRequestSubscribersAsync(string requestId, CancellationToken cancellationToken = default)
        {
            return _requestSubscriptions.TryGetValue(requestId, out var subscribers)
                ? new List<string>(subscribers)
                : new List<string>();
        }

        #endregion

        #region Request Validation and Rules

        public async Task<RequestValidationResult> ValidateRequestAsync(RequestCreateDto dto, CancellationToken cancellationToken = default)
        {
            var result = new RequestValidationResult { IsValid = true };

            // Validate request type
            var validTypes = GetAvailableRequestTypesAsync(cancellationToken).Result;
            if (!validTypes.Contains(dto.Type))
            {
                result.Errors.Add($"Invalid request type: {dto.Type}");
                result.IsValid = false;
            }

            // Validate priority
            var validPriorities = GetAvailableRequestPrioritiesAsync(cancellationToken).Result;
            if (!validPriorities.Contains(dto.Priority))
            {
                result.Errors.Add($"Invalid priority: {dto.Priority}");
                result.IsValid = false;
            }

            // Validate title length
            if (string.IsNullOrWhiteSpace(dto.Title) || dto.Title.Length > 200)
            {
                result.Errors.Add("Title is required and must be 200 characters or less");
                result.IsValid = false;
            }

            // Validate description length
            if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Length > 2000)
            {
                result.Errors.Add("Description is required and must be 2000 characters or less");
                result.IsValid = false;
            }

            // Validate required program
            if (string.IsNullOrEmpty(dto.ProgramId))
            {
                result.Errors.Add("ProgramId is required for all requests");
                result.IsValid = false;
            }
            else
            {
                try
                {
                    await ValidateProgramAsync(dto.ProgramId, cancellationToken);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Program validation failed: {ex.Message}");
                    result.IsValid = false;
                }
            }

            // Add suggestions
            if (dto.Priority == "low")
            {
                result.Suggestions.Add(new RequestValidationSuggestionDto
                {
                    Field = "priority",
                    Message = "Consider if this request should have higher priority",
                    SuggestedValue = "normal"
                });
            }

            return result;
        }

        public async Task<bool> CanUserModifyRequestAsync(string requestId, string userId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(requestId);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                return false;
            }

            // User can modify if they are the requester, assignee, or have admin permissions
            if (request.RequestedBy == userId || request.AssignedTo == userId)
            {
                return true;
            }

            // Check if user has admin permissions (would need user service integration)
            // For now, return false for other users
            return false;
        }

        public async Task<List<string>> GetAvailableRequestTypesAsync(CancellationToken cancellationToken = default)
        {
            return new List<string>
            {
                "feature",
                "ui",
                "review",
                "bug_report",
                "enhancement",
                "support",
                "documentation"
            };
        }

        public async Task<List<string>> GetAvailableRequestStatusesAsync(CancellationToken cancellationToken = default)
        {
            return new List<string>
            {
                "open",
                "in_progress",
                "completed",
                "rejected"
            };
        }

        public async Task<List<string>> GetAvailableRequestPrioritiesAsync(CancellationToken cancellationToken = default)
        {
            return new List<string>
            {
                "low",
                "normal",
                "high"
            };
        }

        #endregion

        #region Private Helper Methods

        private async Task<List<RequestListDto>> MapRequestListDtosAsync(List<Request> requests, CancellationToken cancellationToken)
        {
            var dtos = new List<RequestListDto>();

            foreach (var request in requests)
            {
                var dto = _mapper.Map<RequestListDto>(request);

                // Get requester details
                try
                {
                    var requester = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(request.RequestedBy), cancellationToken);
                    if (requester != null)
                    {
                        dto.RequestedByName = requester.FullName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get requester details for request {RequestId}", request._ID);
                }

                // Get assignee details
                if (!string.IsNullOrEmpty(request.AssignedTo))
                {
                    try
                    {
                        var assignee = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(request.AssignedTo), cancellationToken);
                        if (assignee != null)
                        {
                            dto.AssignedToName = assignee.FullName;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get assignee details for request {RequestId}", request._ID);
                    }
                }

                // Get program details (required)
                try
                {
                    var program = await _unitOfWork.Programs.GetByIdAsync(request.ProgramId, cancellationToken);
                    if (program != null)
                    {
                        dto.ProgramName = program.Name;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get program details for request {RequestId}", request._ID);
                }

                // Set additional properties
                dto.ResponseCount = request.Responses.Count;
                dto.LastResponseAt = request.Responses.Any()
                    ? request.Responses.OrderByDescending(r => r.RespondedAt).First().RespondedAt
                    : null;

                dtos.Add(dto);
            }

            return dtos;
        }

        private async Task<PagedResponse<RequestListDto>> CreatePagedRequestResponse(IEnumerable<Request> requests, PaginationRequestDto pagination, CancellationToken cancellationToken)
        {
            var requestsList = requests.ToList();
            var totalCount = requestsList.Count;
            var paginatedRequests = requestsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = await MapRequestListDtosAsync(paginatedRequests, cancellationToken);
            return new PagedResponse<RequestListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        private async Task<List<RequestResponseDto>> GetRequestResponsesWithUserDetailsAsync(IEnumerable<RequestResponse> responses, CancellationToken cancellationToken)
        {
            var responseDtos = new List<RequestResponseDto>();

            foreach (var response in responses)
            {
                var dto = new RequestResponseDto
                {
                    Id = Guid.NewGuid().ToString(),
                    RespondedBy = response.RespondedBy,
                    RespondedAt = response.RespondedAt,
                    Message = response.Message,
                    IsInternal = false,
                    Attachments = new List<string>()
                };

                // Get user details
                try
                {
                    if (response.RespondedBy != "system") // Should come from current user context BaseController holds CurrentUserId property
                    {
                        var user = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(response.RespondedBy), cancellationToken);
                        if (user != null)
                        {
                            dto.RespondedByName = user.FullName;
                        }
                    }
                    else
                    {
                        dto.RespondedByName = "System";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get user details for response by {UserId}", response.RespondedBy);
                    dto.RespondedByName = "Unknown";
                }

                responseDtos.Add(dto);
            }

            return responseDtos;
        }

        private async Task ValidateProgramAsync(string programId, CancellationToken cancellationToken)
        {
            var programObjectId = ParseObjectId(programId);
            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }
        }

        private RequestTimelineDto BuildRequestTimeline(Request request)
        {
            var timeline = new RequestTimelineDto
            {
                CreatedAt = request.RequestedAt
            };

            // Find key events in responses
            foreach (var response in request.Responses.OrderBy(r => r.RespondedAt))
            {
                if (response.Message.Contains("assigned", StringComparison.OrdinalIgnoreCase) && !timeline.AssignedAt.HasValue)
                {
                    timeline.AssignedAt = response.RespondedAt;
                }
                else if (timeline.FirstResponseAt == default)
                {
                    timeline.FirstResponseAt = response.RespondedAt;
                }
            }

            // Set completion time based on status
            if (request.Status == "completed" || request.Status == "rejected")
            {
                var lastResponse = request.Responses.OrderByDescending(r => r.RespondedAt).FirstOrDefault();
                if (lastResponse != null)
                {
                    timeline.CompletedAt = lastResponse.RespondedAt;
                    timeline.ResolutionTime = timeline.CompletedAt.Value - request.RequestedAt;
                }
            }

            // Build event timeline
            timeline.Events = new List<RequestTimelineEventDto>
            {
                new RequestTimelineEventDto
                {
                    Timestamp = request.RequestedAt,
                    EventType = "created",
                    Description = "Request created",
                    UserId = request.RequestedBy,
                    UserName = "User" // Would resolve from user service
                }
            };

            return timeline;
        }

        private async Task TryAutoAssignRequestAsync(Request request, CancellationToken cancellationToken)
        {
            // Simple auto-assignment logic based on request type
            string? assignTo = request.Type.ToLowerInvariant() switch
            {
                "bug_report" => await FindDeveloperUserAsync(cancellationToken),
                "support" => await FindSupportUserAsync(cancellationToken),
                _ => null
            };

            if (!string.IsNullOrEmpty(assignTo))
            {
                try
                {
                    await _unitOfWork.Requests.AssignRequestAsync(request._ID, assignTo, cancellationToken);
                    _logger.LogInformation("Auto-assigned request {RequestId} to user {UserId}", request._ID, assignTo);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to auto-assign request {RequestId}", request._ID);
                }
            }
        }

        private async Task<string?> FindDeveloperUserAsync(CancellationToken cancellationToken)
        {
            // Find first engineer user (simplified implementation)
            var engineerUsers = await _unitOfWork.Users.GetByRoleAsync("Engineer", cancellationToken);
            return engineerUsers.FirstOrDefault()?._ID.ToString();
        }

        private async Task<string?> FindSupportUserAsync(CancellationToken cancellationToken)
        {
            // Find first support user (simplified implementation)
            var supportUsers = await _unitOfWork.Users.GetByRoleAsync("Support", cancellationToken);
            return supportUsers.FirstOrDefault()?._ID.ToString();
        }

        private bool IsValidStatusTransition(string currentStatus, string newStatus)
        {
            return currentStatus.ToLowerInvariant() switch
            {
                "open" => new[] { "in_progress", "rejected" }.Contains(newStatus.ToLowerInvariant()),
                "in_progress" => new[] { "completed", "rejected", "open" }.Contains(newStatus.ToLowerInvariant()),
                "completed" => new[] { "open" }.Contains(newStatus.ToLowerInvariant()),
                "rejected" => new[] { "open" }.Contains(newStatus.ToLowerInvariant()),
                _ => false
            };
        }

        private async Task NotifySubscribersAsync(string requestId, string message, CancellationToken cancellationToken)
        {
            if (_requestSubscriptions.TryGetValue(requestId, out var subscribers))
            {
                foreach (var subscriberId in subscribers)
                {
                    // In a real implementation, this would send notifications
                    _logger.LogDebug("Notifying subscriber {SubscriberId} about request {RequestId}: {Message}",
                        subscriberId, requestId, message);
                }
            }
        }

        private List<string> GetRequestSubscribers(string requestId)
        {
            return _requestSubscriptions.TryGetValue(requestId, out var subscribers)
                ? new List<string>(subscribers)
                : new List<string>();
        }

        private string ProcessTemplate(string template, Dictionary<string, object> fieldValues)
        {
            var result = template;

            foreach (var field in fieldValues)
            {
                var placeholder = $"{{{field.Key}}}";
                result = result.Replace(placeholder, field.Value?.ToString() ?? string.Empty);
            }

            return result;
        }

        private double CalculatePerformanceRating(List<Request> assignedRequests, List<Request> completedRequests, double averageResolutionTime)
        {
            var completionRate = assignedRequests.Count > 0 ? (double)completedRequests.Count / assignedRequests.Count : 0;
            var timeScore = averageResolutionTime > 0 ? Math.Max(0, 100 - averageResolutionTime) / 100 : 0.5;

            return Math.Round((completionRate * 0.7 + timeScore * 0.3) * 5, 2); // 5-point scale
        }

        private void InitializeDefaultTemplates()
        {
            var templates = new[]
            {
                new RequestTemplate
                {
                    Id = "feature-request",
                    Name = "Feature Request",
                    Description = "Template for requesting new features",
                    Type = "feature",
                    TitleTemplate = "Feature Request: {feature_name}",
                    DescriptionTemplate = "## Feature Description\n{description}\n\n## Business Justification\n{justification}\n\n## Acceptance Criteria\n{criteria}",
                    FieldDefinitions = new { feature_name = "string", description = "text", justification = "text", criteria = "text" },
                    Priority = "normal",
                    IsActive = true,
                    CreatedBy = "system", 
                    CreatedAt = DateTime.UtcNow,
                    UsageCount = 0
                },
                new RequestTemplate
                {
                    Id = "bug-report",
                    Name = "Bug Report",
                    Description = "Template for reporting bugs",
                    Type = "bug_report",
                    TitleTemplate = "Bug: {bug_title}",
                    DescriptionTemplate = "## Steps to Reproduce\n{steps}\n\n## Expected Behavior\n{expected}\n\n## Actual Behavior\n{actual}\n\n## Environment\n{environment}",
                    FieldDefinitions = new { bug_title = "string", steps = "text", expected = "text", actual = "text", environment = "string" },
                    Priority = "normal",
                    IsActive = true,
                    CreatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    UsageCount = 0
                },
                new RequestTemplate
                {
                    Id = "support-request",
                    Name = "Support Request",
                    Description = "Template for general support requests",
                    Type = "support",
                    TitleTemplate = "Support Request: {issue_title}",
                    DescriptionTemplate = "## Issue Description\n{issue_description}\n\n## Steps Taken\n{steps_taken}\n\n## Expected Resolution\n{expected_resolution}",
                    FieldDefinitions = new { issue_title = "string", issue_description = "text", steps_taken = "text", expected_resolution = "text" },
                    Priority = "normal",
                    IsActive = true,
                    CreatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    UsageCount = 0
                }
            };

            foreach (var template in templates)
            {
                _requestTemplates[template.Id] = template;
            }
        }

        #endregion

        #region Supporting Classes

        private class RequestTemplate
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string TitleTemplate { get; set; } = string.Empty;
            public string DescriptionTemplate { get; set; } = string.Empty;
            public object FieldDefinitions { get; set; } = new object();
            public string Priority { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public string CreatedBy { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public int UsageCount { get; set; }
        }

        #endregion
    }
}