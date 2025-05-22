using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IRequestService
    {
        // Basic CRUD Operations
        Task<RequestDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<RequestListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<RequestListDto>> SearchAsync(RequestSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<RequestDto> CreateAsync(RequestCreateDto dto, CancellationToken cancellationToken = default);
        Task<RequestDto> UpdateAsync(string id, RequestUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

        // Request Filtering and Categorization
        Task<PagedResponse<RequestListDto>> GetByTypeAsync(string type, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<RequestListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<RequestListDto>> GetByPriorityAsync(string priority, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<RequestListDto>> GetByRequestedByAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<RequestListDto>> GetByAssignedToAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<RequestListDto>> GetByProgramIdAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<RequestListDto>> GetUnassignedRequestsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);

        // Request Status and Assignment Management
        Task<bool> UpdateStatusAsync(string id, RequestStatusUpdateDto dto, CancellationToken cancellationToken = default);
        Task<RequestDto> AssignRequestAsync(string id, RequestAssignmentDto dto, CancellationToken cancellationToken = default);
        Task<bool> UnassignRequestAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> UpdatePriorityAsync(string id, RequestPriorityUpdateDto dto, CancellationToken cancellationToken = default);

        // Request Response and Communication
        Task<RequestResponseDto> AddResponseAsync(string id, RequestResponseCreateDto dto, CancellationToken cancellationToken = default);
        Task<List<RequestResponseDto>> GetResponsesAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> UpdateResponseAsync(string requestId, string responseId, RequestResponseUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteResponseAsync(string requestId, string responseId, CancellationToken cancellationToken = default);

        // Request Workflow Management
        Task<bool> OpenRequestAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> StartWorkOnRequestAsync(string id, string assignedTo, CancellationToken cancellationToken = default);
        Task<RequestDto> CompleteRequestAsync(string id, RequestCompletionDto dto, CancellationToken cancellationToken = default);
        Task<RequestDto> RejectRequestAsync(string id, RequestRejectionDto dto, CancellationToken cancellationToken = default);
        Task<bool> ReopenRequestAsync(string id, string reason, CancellationToken cancellationToken = default);

        // Request Analytics and Reporting
        Task<RequestStatsDto> GetRequestStatsAsync(RequestStatsFilterDto? filter = null, CancellationToken cancellationToken = default);
        Task<List<RequestTrendDto>> GetRequestTrendsAsync(int days = 30, CancellationToken cancellationToken = default);
        Task<List<RequestMetricDto>> GetRequestMetricsByTypeAsync(CancellationToken cancellationToken = default);
        Task<List<RequestMetricDto>> GetRequestMetricsByStatusAsync(CancellationToken cancellationToken = default);
        Task<List<RequestPerformanceDto>> GetAssigneePerformanceAsync(CancellationToken cancellationToken = default);

        // Request Templates and Categories
        Task<List<RequestTemplateDto>> GetRequestTemplatesAsync(string? type = null, CancellationToken cancellationToken = default);
        Task<RequestTemplateDto> CreateRequestTemplateAsync(RequestTemplateCreateDto dto, CancellationToken cancellationToken = default);
        Task<RequestDto> CreateFromTemplateAsync(string templateId, RequestFromTemplateDto dto, CancellationToken cancellationToken = default);

        // Request Notifications and Subscriptions
        Task<bool> SubscribeToRequestUpdatesAsync(string requestId, string userId, CancellationToken cancellationToken = default);
        Task<bool> UnsubscribeFromRequestUpdatesAsync(string requestId, string userId, CancellationToken cancellationToken = default);
        Task<List<string>> GetRequestSubscribersAsync(string requestId, CancellationToken cancellationToken = default);

        // Request Integration with Infrastructure
        Task<PagedResponse<RequestListDto>> GetInfrastructureRelatedRequestsAsync(string entityType, string entityId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<bool> LinkRequestToInfrastructureAsync(string requestId, RequestInfrastructureLinkDto dto, CancellationToken cancellationToken = default);
        Task<bool> UnlinkRequestFromInfrastructureAsync(string requestId, CancellationToken cancellationToken = default);

        // Request Validation and Rules
        Task<RequestValidationResult> ValidateRequestAsync(RequestCreateDto dto, CancellationToken cancellationToken = default);
        Task<bool> CanUserModifyRequestAsync(string requestId, string userId, CancellationToken cancellationToken = default);
        Task<List<string>> GetAvailableRequestTypesAsync(CancellationToken cancellationToken = default);
        Task<List<string>> GetAvailableRequestStatusesAsync(CancellationToken cancellationToken = default);
        Task<List<string>> GetAvailableRequestPrioritiesAsync(CancellationToken cancellationToken = default);
    }
}