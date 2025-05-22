using AutoMapper;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
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
        public RequestService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<RequestService> logger)
            : base(unitOfWork, mapper, logger)
        {
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

            // Enrich with additional data
            await EnrichRequestDetailAsync(dto, request, cancellationToken);

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

            var dtos = _mapper.Map<List<RequestListDto>>(paginatedRequests);

            // Enrich list items
            await EnrichRequestListAsync(dtos, cancellationToken);

            return new PagedResponse<RequestListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<RequestListDto>> SearchAsync(RequestSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var allRequests = await _unitOfWork.Requests.GetAllAsync(cancellationToken);
            var filteredRequests = allRequests.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchDto.Type))
            {
                filteredRequests = filteredRequests.Where(r => r.Type == searchDto.Type);
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

            if (!string.IsNullOrEmpty(searchDto.RelatedEntityId))
            {
                var entityObjectId = ParseObjectId(searchDto.RelatedEntityId);
                filteredRequests = filteredRequests.Where(r => r.RelatedEntityId == entityObjectId);
            }

            if (!string.IsNullOrEmpty(searchDto.RelatedEntityType))
            {
                filteredRequests = filteredRequests.Where(r => r.RelatedEntityType == searchDto.RelatedEntityType);
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

            var dtos = _mapper.Map<List<RequestListDto>>(paginatedRequests);

            // Enrich list items
            await EnrichRequestListAsync(dtos, cancellationToken);

            return new PagedResponse<RequestListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<RequestDto> CreateAsync(RequestCreateDto dto, CancellationToken cancellationToken = default)
        {
            var request = _mapper.Map<Request>(dto);
            request.RequestedAt = DateTime.UtcNow;
            request.Status = "open";

            // Set requester from current context (would come from authentication context)
            // request.RequestedBy = currentUserId; // This would be injected via service context

            // Validate related entities if specified
            if (!string.IsNullOrEmpty(dto.ProgramId))
            {
                var programObjectId = ParseObjectId(dto.ProgramId);
                var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);
                if (program == null)
                {
                    throw new KeyNotFoundException($"Program with ID {dto.ProgramId} not found.");
                }
                request.ProgramId = programObjectId;
            }

            if (!string.IsNullOrEmpty(dto.RelatedEntityId) && !string.IsNullOrEmpty(dto.RelatedEntityType))
            {
                // Validate that the related entity exists
                await ValidateRelatedEntityAsync(dto.RelatedEntityId, dto.RelatedEntityType, cancellationToken);
                request.RelatedEntityId = ParseObjectId(dto.RelatedEntityId);
            }

            var createdRequest = await _unitOfWork.Requests.CreateAsync(request, cancellationToken);

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

            _mapper.Map(dto, existingRequest);

            var success = await _unitOfWork.Requests.UpdateAsync(objectId, existingRequest, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update request with ID {id}.");
            }

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

            // Check if request can be deleted (e.g., not in progress)
            if (request.Status == "in_progress")
            {
                throw new InvalidOperationException("Cannot delete a request that is in progress.");
            }

            return await _unitOfWork.Requests.DeleteAsync(objectId, cancellationToken);
        }

        #endregion

        #region Request Filtering and Categorization

        public async Task<PagedResponse<RequestListDto>> GetByTypeAsync(string type, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetByTypeAsync(type, cancellationToken);
            var requestsList = requests.ToList();

            // Apply pagination
            var totalCount = requestsList.Count;
            var paginatedRequests = requestsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<RequestListDto>>(paginatedRequests);
            await EnrichRequestListAsync(dtos, cancellationToken);

            return new PagedResponse<RequestListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<RequestListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetByStatusAsync(status, cancellationToken);
            var requestsList = requests.ToList();

            // Apply pagination
            var totalCount = requestsList.Count;
            var paginatedRequests = requestsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<RequestListDto>>(paginatedRequests);
            await EnrichRequestListAsync(dtos, cancellationToken);

            return new PagedResponse<RequestListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<RequestListDto>> GetByPriorityAsync(string priority, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetByPriorityAsync(priority, cancellationToken);
            var requestsList = requests.ToList();

            // Apply pagination
            var totalCount = requestsList.Count;
            var paginatedRequests = requestsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<RequestListDto>>(paginatedRequests);
            await EnrichRequestListAsync(dtos, cancellationToken);

            return new PagedResponse<RequestListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<RequestListDto>> GetByRequestedByAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetByRequestedByAsync(userId, cancellationToken);
            var requestsList = requests.ToList();

            // Apply pagination
            var totalCount = requestsList.Count;
            var paginatedRequests = requestsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<RequestListDto>>(paginatedRequests);
            await EnrichRequestListAsync(dtos, cancellationToken);

            return new PagedResponse<RequestListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<RequestListDto>> GetByAssignedToAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetByAssignedToAsync(userId, cancellationToken);
            var requestsList = requests.ToList();

            // Apply pagination
            var totalCount = requestsList.Count;
            var paginatedRequests = requestsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<RequestListDto>>(paginatedRequests);
            await EnrichRequestListAsync(dtos, cancellationToken);

            return new PagedResponse<RequestListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<RequestListDto>> GetByProgramIdAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var requests = await _unitOfWork.Requests.GetByProgramIdAsync(programObjectId, cancellationToken);
            var requestsList = requests.ToList();

            // Apply pagination
            var totalCount = requestsList.Count;
            var paginatedRequests = requestsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<RequestListDto>>(paginatedRequests);
            await EnrichRequestListAsync(dtos, cancellationToken);

            return new PagedResponse<RequestListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<RequestListDto>> GetUnassignedRequestsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var requests = await _unitOfWork.Requests.GetUnassignedRequestsAsync(cancellationToken);
            var requestsList = requests.ToList();

            // Apply pagination
            var totalCount = requestsList.Count;
            var paginatedRequests = requestsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<RequestListDto>>(paginatedRequests);
            await EnrichRequestListAsync(dtos, cancellationToken);

            return new PagedResponse<RequestListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
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

            var success = await _unitOfWork.Requests.UpdateStatusAsync(objectId, dto.Status, cancellationToken);

            if (success && !string.IsNullOrEmpty(dto.Reason))
            {
                // Add a response explaining the status change
                var response = new RequestResponse
                {
                    RespondedBy = "current-user-id", // Would come from auth context
                    RespondedAt = DateTime.UtcNow,
                    Message = $"Status changed to {dto.Status}: {dto.Reason}"
                };

                await _unitOfWork.Requests.AddResponseAsync(objectId, response, cancellationToken);
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

            var success = await _unitOfWork.Requests.AssignRequestAsync(objectId, dto.AssignedTo, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException("Failed to assign request.");
            }

            // Add assignment notification response
            if (!string.IsNullOrEmpty(dto.AssignmentNotes))
            {
                var response = new RequestResponse
                {
                    RespondedBy = "current-user-id", // Would come from auth context
                    RespondedAt = DateTime.UtcNow,
                    Message = $"Request assigned to {dto.AssignedTo}: {dto.AssignmentNotes}"
                };

                await _unitOfWork.Requests.AddResponseAsync(objectId, response, cancellationToken);
            }

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

            return await _unitOfWork.Requests.UpdateAsync(objectId, request, cancellationToken);
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

            if (success && !string.IsNullOrEmpty(dto.Reason))
            {
                // Add a response explaining the priority change
                var response = new RequestResponse
                {
                    RespondedBy = "current-user-id", // Would come from auth context
                    RespondedAt = DateTime.UtcNow,
                    Message = $"Priority changed to {dto.Priority}: {dto.Reason}"
                };

                await _unitOfWork.Requests.AddResponseAsync(objectId, response, cancellationToken);
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
                RespondedBy = "current-user-id", // Would come from auth context
                RespondedAt = DateTime.UtcNow,
                Message = dto.Message
            };

            var success = await _unitOfWork.Requests.AddResponseAsync(objectId, response, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException("Failed to add response.");
            }

            return new RequestResponseDto
            {
                Id = Guid.NewGuid().ToString(),
                RequestId = id,
                RespondedBy = response.RespondedBy,
                RespondedByName = "Current User", // Would fetch from user service
                RespondedAt = response.RespondedAt,
                Message = response.Message,
                IsInternal = dto.IsInternal,
                Attachments = dto.Attachments
            };
        }

        public async Task<List<RequestResponseDto>> GetResponsesAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var responses = await _unitOfWork.Requests.GetResponsesAsync(objectId, cancellationToken);

            var dtos = responses.Select(r => new RequestResponseDto
            {
                Id = Guid.NewGuid().ToString(),
                RequestId = id,
                RespondedBy = r.RespondedBy,
                RespondedByName = "User Name", // Would fetch from user service
                RespondedAt = r.RespondedAt,
                Message = r.Message,
                IsInternal = false, // Would be determined from response data
                Attachments = new List<string>() // Would be populated from response data
            }).ToList();

            return dtos;
        }

        public async Task<bool> UpdateResponseAsync(string requestId, string responseId, RequestResponseUpdateDto dto, CancellationToken cancellationToken = default)
        {
            // TODO: Implement response update logic
            // This would require modifications to the repository to support response updates
            await Task.CompletedTask;
            return true;
        }

        public async Task<bool> DeleteResponseAsync(string requestId, string responseId, CancellationToken cancellationToken = default)
        {
            // TODO: Implement response deletion logic
            // This would require modifications to the repository to support response deletion
            await Task.CompletedTask;
            return true;
        }

        #endregion

        #region Request Workflow Management

        public async Task<bool> OpenRequestAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            return await _unitOfWork.Requests.UpdateStatusAsync(objectId, "open", cancellationToken);
        }

        public async Task<bool> StartWorkOnRequestAsync(string id, string assignedTo, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var success = await _unitOfWork.Requests.AssignRequestAsync(objectId, assignedTo, cancellationToken);

            if (success)
            {
                await _unitOfWork.Requests.UpdateStatusAsync(objectId, "in_progress", cancellationToken);
            }

            return success;
        }

        public async Task<RequestDto> CompleteRequestAsync(string id, RequestCompletionDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {id} not found.");
            }

            // Update status to completed
            await _unitOfWork.Requests.UpdateStatusAsync(objectId, "completed", cancellationToken);

            // Add completion response
            var response = new RequestResponse
            {
                RespondedBy = "current-user-id", // Would come from auth context
                RespondedAt = DateTime.UtcNow,
                Message = $"Request completed: {dto.CompletionNotes}"
            };

            await _unitOfWork.Requests.AddResponseAsync(objectId, response, cancellationToken);

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
            var response = new RequestResponse
            {
                RespondedBy = "current-user-id", // Would come from auth context
                RespondedAt = DateTime.UtcNow,
                Message = $"Request rejected: {dto.RejectionReason}"
            };

            await _unitOfWork.Requests.AddResponseAsync(objectId, response, cancellationToken);

            var updatedRequest = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);
            return _mapper.Map<RequestDto>(updatedRequest);
        }

        public async Task<bool> ReopenRequestAsync(string id, string reason, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var success = await _unitOfWork.Requests.UpdateStatusAsync(objectId, "open", cancellationToken);

            if (success)
            {
                var response = new RequestResponse
                {
                    RespondedBy = "current-user-id", // Would come from auth context
                    RespondedAt = DateTime.UtcNow,
                    Message = $"Request reopened: {reason}"
                };

                await _unitOfWork.Requests.AddResponseAsync(objectId, response, cancellationToken);
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

                if (filter.Statuses?.Any() == true)
                {
                    requests = requests.Where(r => filter.Statuses.Contains(r.Status));
                }
            }

            var requestsList = requests.ToList();

            return new RequestStatsDto
            {
                TotalRequests = requestsList.Count,
                OpenRequests = requestsList.Count(r => r.Status == "open"),
                InProgressRequests = requestsList.Count(r => r.Status == "in_progress"),
                CompletedRequests = requestsList.Count(r => r.Status == "completed"),
                RejectedRequests = requestsList.Count(r => r.Status == "rejected"),
                UnassignedRequests = requestsList.Count(r => string.IsNullOrEmpty(r.AssignedTo)),
                AverageResolutionTime = ComputeAverageResolutionTime(requestsList),
                RequestsByType = requestsList.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count()),
                RequestsByPriority = requestsList.GroupBy(r => r.Priority).ToDictionary(g => g.Key, g => g.Count())
            };
        }

        public async Task<List<RequestTrendDto>> GetRequestTrendsAsync(int days = 30, CancellationToken cancellationToken = default)
        {
            var allRequests = await _unitOfWork.Requests.GetAllAsync(cancellationToken);
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var recentRequests = allRequests.Where(r => r.RequestedAt >= cutoffDate);

            var trends = new List<RequestTrendDto>();

            for (var i = 0; i < days; i++)
            {
                var date = DateTime.UtcNow.AddDays(-i).Date;
                var dayRequests = recentRequests.Where(r => r.RequestedAt.Date == date);

                trends.Add(new RequestTrendDto
                {
                    Date = date,
                    CreatedCount = dayRequests.Count(),
                    CompletedCount = dayRequests.Count(r => r.Status == "completed"),
                    TotalOpen = allRequests.Count(r => r.Status == "open" && r.RequestedAt.Date <= date)
                });
            }

            return trends.OrderBy(t => t.Date).ToList();
        }

        public async Task<List<RequestMetricDto>> GetRequestMetricsByTypeAsync(CancellationToken cancellationToken = default)
        {
            var allRequests = await _unitOfWork.Requests.GetAllAsync(cancellationToken);
            var requestsList = allRequests.ToList();
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
            var allRequests = await _unitOfWork.Requests.GetAllAsync(cancellationToken);
            var requestsList = allRequests.ToList();
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
            var allRequests = await _unitOfWork.Requests.GetAllAsync(cancellationToken);
            var assignedRequests = allRequests.Where(r => !string.IsNullOrEmpty(r.AssignedTo));

            return assignedRequests
                .GroupBy(r => r.AssignedTo)
                .Select(g => new RequestPerformanceDto
                {
                    UserId = g.Key!,
                    UserName = "User Name", // Would fetch from user service
                    AssignedCount = g.Count(),
                    CompletedCount = g.Count(r => r.Status == "completed"),
                    CompletionRate = g.Any() ? (double)g.Count(r => r.Status == "completed") / g.Count() * 100 : 0,
                    AverageResolutionTime = ComputeAverageResolutionTime(g.ToList()),
                    Rating = 4.0 // Would be computed from feedback
                })
                .OrderByDescending(p => p.CompletionRate)
                .ToList();
        }

        #endregion

        #region Request Templates and Categories

        public async Task<List<RequestTemplateDto>> GetRequestTemplatesAsync(string? type = null, CancellationToken cancellationToken = default)
        {
            // TODO: Implement template repository and logic
            var templates = new List<RequestTemplateDto>();

            // For now, return some default templates
            if (string.IsNullOrEmpty(type) || type == "feature")
            {
                templates.Add(new RequestTemplateDto
                {
                    Id = "feature-template",
                    Name = "Feature Request",
                    Description = "Request a new feature for a program",
                    Type = "feature",
                    TitleTemplate = "Feature Request: {feature_name}",
                    DescriptionTemplate = "Feature: {feature_name}\nDescription: {description}\nPriority: {priority}",
                    Priority = "normal",
                    IsActive = true,
                    CreatedBy = "system",
                    CreatedAt = DateTime.UtcNow,
                    UsageCount = 0
                });
            }

            return templates;
        }

        public async Task<RequestTemplateDto> CreateRequestTemplateAsync(RequestTemplateCreateDto dto, CancellationToken cancellationToken = default)
        {
            // TODO: Implement template creation logic
            return new RequestTemplateDto
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                Description = dto.Description,
                Type = dto.Type,
                TitleTemplate = dto.TitleTemplate,
                DescriptionTemplate = dto.DescriptionTemplate,
                Priority = dto.Priority,
                IsActive = dto.IsActive,
                CreatedBy = "current-user-id", // Would come from auth context
                CreatedAt = DateTime.UtcNow,
                UsageCount = 0
            };
        }

        public async Task<RequestDto> CreateFromTemplateAsync(string templateId, RequestFromTemplateDto dto, CancellationToken cancellationToken = default)
        {
            // TODO: Implement template-based request creation
            var createDto = new RequestCreateDto
            {
                Type = "feature", // Would come from template
                Title = "Feature Request from Template", // Would be generated from template
                Description = "Generated from template", // Would be generated from template
                ProgramId = dto.ProgramId,
                RelatedEntityId = dto.RelatedEntityId,
                RelatedEntityType = dto.RelatedEntityType,
                Priority = "normal" // Would come from template
            };

            return await CreateAsync(createDto, cancellationToken);
        }

        #endregion

        #region Request Notifications and Subscriptions

        public async Task<bool> SubscribeToRequestUpdatesAsync(string requestId, string userId, CancellationToken cancellationToken = default)
        {
            // TODO: Implement subscription logic
            await Task.CompletedTask;
            return true;
        }

        public async Task<bool> UnsubscribeFromRequestUpdatesAsync(string requestId, string userId, CancellationToken cancellationToken = default)
        {
            // TODO: Implement unsubscription logic
            await Task.CompletedTask;
            return true;
        }

        public async Task<List<string>> GetRequestSubscribersAsync(string requestId, CancellationToken cancellationToken = default)
        {
            // TODO: Implement subscriber retrieval logic
            await Task.CompletedTask;
            return new List<string>();
        }

        #endregion

        #region Request Integration with Infrastructure

        public async Task<PagedResponse<RequestListDto>> GetInfrastructureRelatedRequestsAsync(string entityType, string entityId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var searchDto = new RequestSearchDto
            {
                RelatedEntityType = entityType,
                RelatedEntityId = entityId
            };

            return await SearchAsync(searchDto, pagination, cancellationToken);
        }

        public async Task<bool> LinkRequestToInfrastructureAsync(string requestId, RequestInfrastructureLinkDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(requestId);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {requestId} not found.");
            }

            // Validate that the infrastructure entity exists
            await ValidateRelatedEntityAsync(dto.EntityId, dto.EntityType, cancellationToken);

            request.RelatedEntityId = ParseObjectId(dto.EntityId);
            request.RelatedEntityType = dto.EntityType;

            return await _unitOfWork.Requests.UpdateAsync(objectId, request, cancellationToken);
        }

        public async Task<bool> UnlinkRequestFromInfrastructureAsync(string requestId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(requestId);
            var request = await _unitOfWork.Requests.GetByIdAsync(objectId, cancellationToken);

            if (request == null)
            {
                throw new KeyNotFoundException($"Request with ID {requestId} not found.");
            }

            request.RelatedEntityId = null;
            request.RelatedEntityType = null;

            return await _unitOfWork.Requests.UpdateAsync(objectId, request, cancellationToken);
        }

        #endregion

        #region Request Validation and Rules

        public async Task<RequestValidationResult> ValidateRequestAsync(RequestCreateDto dto, CancellationToken cancellationToken = default)
        {
            var result = new RequestValidationResult { IsValid = true };

            // Validate required fields
            if (string.IsNullOrEmpty(dto.Title))
            {
                result.Errors.Add("Title is required");
                result.IsValid = false;
            }

            if (string.IsNullOrEmpty(dto.Description))
            {
                result.Errors.Add("Description is required");
                result.IsValid = false;
            }

            // Validate type
            var validTypes = await GetAvailableRequestTypesAsync(cancellationToken);
            if (!validTypes.Contains(dto.Type))
            {
                result.Errors.Add($"Invalid request type: {dto.Type}");
                result.IsValid = false;
            }

            // Validate priority
            var validPriorities = await GetAvailableRequestPrioritiesAsync(cancellationToken);
            if (!validPriorities.Contains(dto.Priority))
            {
                result.Errors.Add($"Invalid priority: {dto.Priority}");
                result.IsValid = false;
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

            // User can modify if they are the requester, assignee, or admin
            return request.RequestedBy == userId ||
                   request.AssignedTo == userId ||
                   await IsUserAdminAsync(userId, cancellationToken);
        }

        public async Task<List<string>> GetAvailableRequestTypesAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new List<string> { "feature", "ui", "review", "infrastructure_access", "bug", "enhancement" };
        }

        public async Task<List<string>> GetAvailableRequestStatusesAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new List<string> { "open", "in_progress", "completed", "rejected", "on_hold" };
        }

        public async Task<List<string>> GetAvailableRequestPrioritiesAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new List<string> { "low", "normal", "high", "urgent", "critical" };
        }

        #endregion

        #region Private Helper Methods

        private async Task EnrichRequestDetailAsync(RequestDetailDto dto, Request request, CancellationToken cancellationToken)
        {
            // Enrich with user names
            dto.RequestedByName = "User Name"; // Would fetch from user service
            dto.AssignedToName = string.IsNullOrEmpty(request.AssignedTo) ? null : "Assignee Name";

            // Enrich with program name
            if (request.ProgramId.HasValue)
            {
                var program = await _unitOfWork.Programs.GetByIdAsync(request.ProgramId.Value, cancellationToken);
                dto.ProgramName = program?.Name;
            }

            // Enrich with responses
            dto.Responses = await GetResponsesAsync(dto.Id, cancellationToken);

            // Enrich with related entity
            if (request.RelatedEntityId.HasValue && !string.IsNullOrEmpty(request.RelatedEntityType))
            {
                dto.RelatedEntity = await GetRelatedEntityAsync(request.RelatedEntityId.Value, request.RelatedEntityType, cancellationToken);
            }

            // Create timeline
            dto.Timeline = CreateTimeline(request, dto.Responses);
        }

        private async Task EnrichRequestListAsync(List<RequestListDto> dtos, CancellationToken cancellationToken)
        {
            foreach (var dto in dtos)
            {
                // TODO: Add user name resolution, program name, etc.
                dto.RequestedByName = "User Name"; // Would fetch from user service
                dto.AssignedToName = string.IsNullOrEmpty(dto.AssignedTo) ? null : "Assignee Name";
                dto.ProgramName = !string.IsNullOrEmpty(dto.ProgramId) ? "Program Name" : null;
                dto.ResponseCount = 0; // Would be computed from actual responses
                await Task.CompletedTask; // Placeholder
            }
        }

        private async Task ValidateRelatedEntityAsync(string entityId, string entityType, CancellationToken cancellationToken)
        {
            var objectId = ParseObjectId(entityId);

            switch (entityType.ToLower())
            {
                case "client":
                    var client = await _unitOfWork.Clients.GetByIdAsync(objectId, cancellationToken);
                    if (client == null) throw new KeyNotFoundException($"Client with ID {entityId} not found.");
                    break;

                case "region":
                    var region = await _unitOfWork.Regions.GetByIdAsync(objectId, cancellationToken);
                    if (region == null) throw new KeyNotFoundException($"Region with ID {entityId} not found.");
                    break;

                case "tm":
                    var tm = await _unitOfWork.TMs.GetByIdAsync(objectId, cancellationToken);
                    if (tm == null) throw new KeyNotFoundException($"TM with ID {entityId} not found.");
                    break;

                case "building":
                    var building = await _unitOfWork.Buildings.GetByIdAsync(objectId, cancellationToken);
                    if (building == null) throw new KeyNotFoundException($"Building with ID {entityId} not found.");
                    break;

                default:
                    throw new ArgumentException($"Unknown entity type: {entityType}");
            }
        }

        private async Task<RequestRelatedEntityDto?> GetRelatedEntityAsync(ObjectId entityId, string entityType, CancellationToken cancellationToken)
        {
            try
            {
                switch (entityType.ToLower())
                {
                    case "client":
                        var client = await _unitOfWork.Clients.GetByIdAsync(entityId, cancellationToken);
                        return client != null ? new RequestRelatedEntityDto
                        {
                            EntityType = entityType,
                            EntityId = entityId.ToString(),
                            EntityName = client.Name
                        } : null;

                    // Add other entity types as needed
                    default:
                        return new RequestRelatedEntityDto
                        {
                            EntityType = entityType,
                            EntityId = entityId.ToString(),
                            EntityName = "Unknown Entity"
                        };
                }
            }
            catch
            {
                return null;
            }
        }

        private async Task<bool> IsUserAdminAsync(string userId, CancellationToken cancellationToken)
        {
            // TODO: Check if user has admin role
            await Task.CompletedTask;
            return false;
        }

        private static RequestTimelineDto CreateTimeline(Request request, List<RequestResponseDto> responses)
        {
            var timeline = new RequestTimelineDto
            {
                CreatedAt = request.RequestedAt,
                Events = new List<RequestTimelineEventDto>()
            };

            // Add creation event
            timeline.Events.Add(new RequestTimelineEventDto
            {
                Timestamp = request.RequestedAt,
                EventType = "Created",
                Description = "Request created",
                UserId = request.RequestedBy,
                UserName = "User Name"
            });

            // Add response events
            foreach (var response in responses.OrderBy(r => r.RespondedAt))
            {
                timeline.Events.Add(new RequestTimelineEventDto
                {
                    Timestamp = response.RespondedAt,
                    EventType = "Response",
                    Description = "Response added",
                    UserId = response.RespondedBy,
                    UserName = response.RespondedByName
                });
            }

            // Set timeline milestones
            if (!string.IsNullOrEmpty(request.AssignedTo))
            {
                timeline.AssignedAt = request.RequestedAt; // Would be actual assignment date
            }

            timeline.FirstResponseAt = responses.OrderBy(r => r.RespondedAt).FirstOrDefault()?.RespondedAt;

            if (request.Status == "completed")
            {
                timeline.CompletedAt = responses.OrderByDescending(r => r.RespondedAt).FirstOrDefault()?.RespondedAt;

                if (timeline.CompletedAt.HasValue)
                {
                    timeline.ResolutionTime = timeline.CompletedAt.Value - request.RequestedAt;
                }
            }

            return timeline;
        }

        private static double ComputeAverageResolutionTime(List<Request> requests)
        {
            var completedRequests = requests.Where(r => r.Status == "completed").ToList();

            if (!completedRequests.Any())
                return 0;

            // TODO: Compute actual resolution time from request data
            // For now, return a placeholder value
            return 24.0; // Hours
        }

        #endregion
    }
}