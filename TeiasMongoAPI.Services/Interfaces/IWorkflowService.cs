using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IWorkflowService
    {
        Task<PagedResponse<WorkflowListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<WorkflowDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<WorkflowDetailDto> CreateAsync(WorkflowCreateDto createDto, CancellationToken cancellationToken = default);
        Task<WorkflowDetailDto> UpdateAsync(string id, WorkflowUpdateDto updateDto, CancellationToken cancellationToken = default);
        Task<WorkflowDetailDto> UpdateNameDescriptionAsync(string id, WorkflowNameDescriptionUpdateDto updateDto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<WorkflowListDto>> GetUserAccessibleWorkflowsAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<WorkflowListDto>> GetWorkflowsByStatusAsync(WorkflowStatus status, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<WorkflowListDto>> SearchWorkflowsAsync(string searchTerm, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<WorkflowListDto>> GetWorkflowTemplatesAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<WorkflowDetailDto> CloneWorkflowAsync(string workflowId, WorkflowCloneDto cloneDto, ObjectId? currentUserId = null, CancellationToken cancellationToken = default);
        Task<WorkflowValidationResult> ValidateWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);
        Task<bool> UpdateWorkflowStatusAsync(string workflowId, WorkflowStatus status, CancellationToken cancellationToken = default);
        Task<bool> ArchiveWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);
        Task<bool> RestoreWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);
        Task<WorkflowPermissionDto> GetWorkflowPermissionsAsync(string workflowId, CancellationToken cancellationToken = default);
        Task<bool> UpdateWorkflowPermissionsAsync(string workflowId, WorkflowPermissionUpdateDto permissionDto, CancellationToken cancellationToken = default);
        Task<bool> HasPermissionAsync(string workflowId, string userId, WorkflowPermissionType permission, CancellationToken cancellationToken = default);
        Task<WorkflowStatisticsDto> GetWorkflowStatisticsAsync(string workflowId, CancellationToken cancellationToken = default);
        Task<List<WorkflowExecutionSummaryDto>> GetWorkflowExecutionHistoryAsync(string workflowId, int limit = 10, CancellationToken cancellationToken = default);
        Task<WorkflowComplexityMetrics> GetWorkflowComplexityAsync(string workflowId, CancellationToken cancellationToken = default);
        Task<List<string>> GetWorkflowTagsAsync(CancellationToken cancellationToken = default);
        Task<PagedResponse<WorkflowListDto>> GetWorkflowsByTagAsync(string tag, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<bool> AddNodeToWorkflowAsync(string workflowId, WorkflowNodeCreateDto nodeDto, CancellationToken cancellationToken = default);
        Task<bool> UpdateNodeInWorkflowAsync(string workflowId, string nodeId, WorkflowNodeUpdateDto nodeDto, CancellationToken cancellationToken = default);
        Task<bool> RemoveNodeFromWorkflowAsync(string workflowId, string nodeId, CancellationToken cancellationToken = default);
        Task<bool> AddEdgeToWorkflowAsync(string workflowId, WorkflowEdgeCreateDto edgeDto, CancellationToken cancellationToken = default);
        Task<bool> UpdateEdgeInWorkflowAsync(string workflowId, string edgeId, WorkflowEdgeUpdateDto edgeDto, CancellationToken cancellationToken = default);
        Task<bool> RemoveEdgeFromWorkflowAsync(string workflowId, string edgeId, CancellationToken cancellationToken = default);
        Task<WorkflowExecutionPlanDto> GetExecutionPlanAsync(string workflowId, CancellationToken cancellationToken = default);
        Task<bool> ExportWorkflowAsync(string workflowId, string format, CancellationToken cancellationToken = default);
        Task<WorkflowDetailDto> ImportWorkflowAsync(WorkflowImportDto importDto, CancellationToken cancellationToken = default);
    }
}