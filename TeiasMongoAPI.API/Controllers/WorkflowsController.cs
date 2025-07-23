using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    /// <summary>
    /// Workflow management controller for creating, managing, and executing workflows
    /// </summary>
    [Attributes.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WorkflowsController : BaseController
    {
        private readonly IWorkflowService _workflowService;
        private readonly IWorkflowExecutionEngine _workflowExecutionEngine;
        private readonly IWorkflowValidationService _workflowValidationService;

        public WorkflowsController(
            IWorkflowService workflowService,
            IWorkflowExecutionEngine workflowExecutionEngine,
            IWorkflowValidationService workflowValidationService,
            ILogger<WorkflowsController> logger)
            : base(logger)
        {
            _workflowService = workflowService;
            _workflowExecutionEngine = workflowExecutionEngine;
            _workflowValidationService = workflowValidationService;
        }

        #region Basic CRUD Operations

        /// <summary>
        /// Get all workflows with pagination
        /// </summary>
        [HttpGet]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<PagedResponse<WorkflowListDto>>>> GetAll(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.GetAllAsync(pagination, cancellationToken);
            }, "Get all workflows");
        }

        /// <summary>
        /// Get workflow by ID
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowDetailDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.GetByIdAsync(id, cancellationToken);
            }, "Get workflow by ID");
        }

        /// <summary>
        /// Create a new workflow
        /// </summary>
        [HttpPost]
        [RequirePermission(UserPermissions.CreateWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowDetailDto>>> Create(
            [FromBody] WorkflowCreateDto createDto,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.CreateAsync(createDto, cancellationToken);
            }, "Create workflow");
        }

        /// <summary>
        /// Update an existing workflow
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.EditWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowDetailDto>>> Update(
            string id,
            [FromBody] WorkflowUpdateDto updateDto,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.UpdateAsync(id, updateDto, cancellationToken);
            }, "Update workflow");
        }

        /// <summary>
        /// Delete a workflow
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.DeleteWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.DeleteAsync(id, cancellationToken);
            }, "Delete workflow");
        }

        #endregion

        #region Workflow Management

        /// <summary>
        /// Get workflows by user
        /// </summary>
        [HttpGet("user/{userId}")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<PagedResponse<WorkflowListDto>>>> GetByUser(
            string userId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.GetWorkflowsByUserAsync(userId, pagination, cancellationToken);
            }, "Get workflows by user");
        }

        /// <summary>
        /// Get workflows by status
        /// </summary>
        [HttpGet("status/{status}")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<PagedResponse<WorkflowListDto>>>> GetByStatus(
            WorkflowStatus status,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.GetWorkflowsByStatusAsync(status, pagination, cancellationToken);
            }, "Get workflows by status");
        }

        /// <summary>
        /// Search workflows
        /// </summary>
        [HttpGet("search")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<PagedResponse<WorkflowListDto>>>> Search(
            [FromQuery] string searchTerm,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.SearchWorkflowsAsync(searchTerm, pagination, cancellationToken);
            }, "Search workflows");
        }

        /// <summary>
        /// Get workflow templates
        /// </summary>
        [HttpGet("templates")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<PagedResponse<WorkflowListDto>>>> GetTemplates(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.GetWorkflowTemplatesAsync(pagination, cancellationToken);
            }, "Get workflow templates");
        }

        /// <summary>
        /// Clone a workflow
        /// </summary>
        [HttpPost("{id}/clone")]
        [RequirePermission(UserPermissions.CreateWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowDetailDto>>> Clone(
            string id,
            [FromBody] WorkflowCloneDto cloneDto,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.CloneWorkflowAsync(id, cloneDto, cancellationToken);
            }, "Clone workflow");
        }

        /// <summary>
        /// Update workflow status
        /// </summary>
        [HttpPatch("{id}/status")]
        [RequirePermission(UserPermissions.EditWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateStatus(
            string id,
            [FromBody] WorkflowStatus status,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.UpdateWorkflowStatusAsync(id, status, cancellationToken);
            }, "Update workflow status");
        }

        /// <summary>
        /// Archive a workflow
        /// </summary>
        [HttpPost("{id}/archive")]
        [RequirePermission(UserPermissions.EditWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> Archive(
            string id,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.ArchiveWorkflowAsync(id, cancellationToken);
            }, "Archive workflow");
        }

        /// <summary>
        /// Restore an archived workflow
        /// </summary>
        [HttpPost("{id}/restore")]
        [RequirePermission(UserPermissions.EditWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> Restore(
            string id,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.RestoreWorkflowAsync(id, cancellationToken);
            }, "Restore workflow");
        }

        #endregion

        #region Workflow Validation

        /// <summary>
        /// Validate a workflow
        /// </summary>
        [HttpPost("{id}/validate")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowValidationResult>>> Validate(
            string id,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.ValidateWorkflowAsync(id, cancellationToken);
            }, "Validate workflow");
        }

        /// <summary>
        /// Get workflow execution plan
        /// </summary>
        [HttpGet("{id}/execution-plan")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowExecutionPlanDto>>> GetExecutionPlan(
            string id,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.GetExecutionPlanAsync(id, cancellationToken);
            }, "Get workflow execution plan");
        }

        /// <summary>
        /// Get workflow complexity metrics
        /// </summary>
        [HttpGet("{id}/complexity")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowComplexityMetrics>>> GetComplexity(
            string id,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.GetWorkflowComplexityAsync(id, cancellationToken);
            }, "Get workflow complexity");
        }

        #endregion

        #region Workflow Execution

        /// <summary>
        /// Execute a workflow
        /// </summary>
        [HttpPost("{id}/execute")]
        [RequirePermission(UserPermissions.ExecuteWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowExecutionResponseDto>>> Execute(
            string id,
            [FromBody] WorkflowExecutionRequest executionRequest,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                executionRequest.WorkflowId = id;
                return await _workflowExecutionEngine.ExecuteWorkflowAsync(executionRequest, CurrentUserId!.Value, cancellationToken);
            }, "Execute workflow");
        }

        /// <summary>
        /// Get workflow execution status
        /// </summary>
        [HttpGet("executions/{executionId}")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowExecutionResponseDto>>> GetExecutionStatus(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowExecutionEngine.GetExecutionStatusAsync(executionId, cancellationToken);
            }, "Get execution status");
        }

        /// <summary>
        /// Pause a workflow execution
        /// </summary>
        [HttpPost("executions/{executionId}/pause")]
        [RequirePermission(UserPermissions.ExecuteWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> PauseExecution(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowExecutionEngine.PauseWorkflowAsync(executionId, cancellationToken);
            }, "Pause execution");
        }

        /// <summary>
        /// Resume a workflow execution
        /// </summary>
        [HttpPost("executions/{executionId}/resume")]
        [RequirePermission(UserPermissions.ExecuteWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowExecutionResponseDto>>> ResumeExecution(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowExecutionEngine.ResumeWorkflowAsync(executionId, cancellationToken);
            }, "Resume execution");
        }

        /// <summary>
        /// Cancel a workflow execution
        /// </summary>
        [HttpPost("executions/{executionId}/cancel")]
        [RequirePermission(UserPermissions.ExecuteWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> CancelExecution(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowExecutionEngine.CancelWorkflowAsync(executionId, cancellationToken);
            }, "Cancel execution");
        }

        /// <summary>
        /// Get all node outputs from a workflow execution
        /// </summary>
        [HttpGet("executions/{executionId}/outputs")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<Dictionary<string, WorkflowDataContractDto>>>> GetExecutionOutputs(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowExecutionEngine.GetAllNodeOutputsAsync(executionId, cancellationToken);
            }, "Get execution outputs");
        }

        /// <summary>
        /// Get specific node output from a workflow execution
        /// </summary>
        [HttpGet("executions/{executionId}/outputs/{nodeId}")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowDataContractDto>>> GetNodeOutput(
            string executionId,
            string nodeId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowExecutionEngine.GetNodeOutputAsync(executionId, nodeId, cancellationToken);
            }, "Get node output");
        }

        /// <summary>
        /// Get execution statistics
        /// </summary>
        [HttpGet("executions/{executionId}/statistics")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowExecutionStatisticsResponseDto>>> GetExecutionStatistics(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowExecutionEngine.GetExecutionStatisticsAsync(executionId, cancellationToken);
            }, "Get execution statistics");
        }

        /// <summary>
        /// Get execution logs
        /// </summary>
        [HttpGet("executions/{executionId}/logs")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<List<WorkflowExecutionLogResponseDto>>>> GetExecutionLogs(
            string executionId,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 100,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowExecutionEngine.GetExecutionLogsAsync(executionId, skip, take, cancellationToken);
            }, "Get execution logs");
        }

        #endregion

        #region Node Management

        /// <summary>
        /// Add a node to a workflow
        /// </summary>
        [HttpPost("{id}/nodes")]
        [RequirePermission(UserPermissions.EditWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> AddNode(
            string id,
            [FromBody] WorkflowNodeCreateDto nodeDto,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.AddNodeToWorkflowAsync(id, nodeDto, cancellationToken);
            }, "Add node to workflow");
        }

        /// <summary>
        /// Update a node in a workflow
        /// </summary>
        [HttpPut("{id}/nodes/{nodeId}")]
        [RequirePermission(UserPermissions.EditWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateNode(
            string id,
            string nodeId,
            [FromBody] WorkflowNodeUpdateDto nodeDto,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.UpdateNodeInWorkflowAsync(id, nodeId, nodeDto, cancellationToken);
            }, "Update node in workflow");
        }

        /// <summary>
        /// Remove a node from a workflow
        /// </summary>
        [HttpDelete("{id}/nodes/{nodeId}")]
        [RequirePermission(UserPermissions.EditWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveNode(
            string id,
            string nodeId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.RemoveNodeFromWorkflowAsync(id, nodeId, cancellationToken);
            }, "Remove node from workflow");
        }

        /// <summary>
        /// Execute a specific node in a workflow execution
        /// </summary>
        [HttpPost("executions/{executionId}/nodes/{nodeId}/execute")]
        [RequirePermission(UserPermissions.ExecuteWorkflows)]
        public async Task<ActionResult<ApiResponse<NodeExecutionResponseDto>>> ExecuteNode(
            string executionId,
            string nodeId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowExecutionEngine.ExecuteNodeAsync(executionId, nodeId, cancellationToken);
            }, "Execute node");
        }

        /// <summary>
        /// Retry a failed node in a workflow execution
        /// </summary>
        [HttpPost("executions/{executionId}/nodes/{nodeId}/retry")]
        [RequirePermission(UserPermissions.ExecuteWorkflows)]
        public async Task<ActionResult<ApiResponse<NodeExecutionResponseDto>>> RetryNode(
            string executionId,
            string nodeId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowExecutionEngine.RetryNodeAsync(executionId, nodeId, cancellationToken);
            }, "Retry node");
        }

        /// <summary>
        /// Skip a node in a workflow execution
        /// </summary>
        [HttpPost("executions/{executionId}/nodes/{nodeId}/skip")]
        [RequirePermission(UserPermissions.ExecuteWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> SkipNode(
            string executionId,
            string nodeId,
            [FromBody] string reason,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowExecutionEngine.SkipNodeAsync(executionId, nodeId, reason, cancellationToken);
            }, "Skip node");
        }

        #endregion

        #region Edge Management

        /// <summary>
        /// Add an edge to a workflow
        /// </summary>
        [HttpPost("{id}/edges")]
        [RequirePermission(UserPermissions.EditWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> AddEdge(
            string id,
            [FromBody] WorkflowEdgeCreateDto edgeDto,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.AddEdgeToWorkflowAsync(id, edgeDto, cancellationToken);
            }, "Add edge to workflow");
        }

        /// <summary>
        /// Update an edge in a workflow
        /// </summary>
        [HttpPut("{id}/edges/{edgeId}")]
        [RequirePermission(UserPermissions.EditWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateEdge(
            string id,
            string edgeId,
            [FromBody] WorkflowEdgeUpdateDto edgeDto,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.UpdateEdgeInWorkflowAsync(id, edgeId, edgeDto, cancellationToken);
            }, "Update edge in workflow");
        }

        /// <summary>
        /// Remove an edge from a workflow
        /// </summary>
        [HttpDelete("{id}/edges/{edgeId}")]
        [RequirePermission(UserPermissions.EditWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveEdge(
            string id,
            string edgeId,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.RemoveEdgeFromWorkflowAsync(id, edgeId, cancellationToken);
            }, "Remove edge from workflow");
        }

        #endregion

        #region Statistics and Analytics

        /// <summary>
        /// Get workflow statistics
        /// </summary>
        [HttpGet("{id}/statistics")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowStatisticsDto>>> GetStatistics(
            string id,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.GetWorkflowStatisticsAsync(id, cancellationToken);
            }, "Get workflow statistics");
        }

        /// <summary>
        /// Get workflow execution history
        /// </summary>
        [HttpGet("{id}/executions")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<List<WorkflowExecutionSummaryDto>>>> GetExecutionHistory(
            string id,
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.GetWorkflowExecutionHistoryAsync(id, limit, cancellationToken);
            }, "Get execution history");
        }

        /// <summary>
        /// Get all active executions
        /// </summary>
        [HttpGet("executions/active")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<List<WorkflowExecutionResponseDto>>>> GetActiveExecutions(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowExecutionEngine.GetActiveExecutionsAsync(cancellationToken);
            }, "Get active executions");
        }

        #endregion

        #region Permissions

        /// <summary>
        /// Get workflow permissions
        /// </summary>
        [HttpGet("{id}/permissions")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowPermissionDto>>> GetPermissions(
            string id,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.GetWorkflowPermissionsAsync(id, cancellationToken);
            }, "Get workflow permissions");
        }

        /// <summary>
        /// Update workflow permissions
        /// </summary>
        [HttpPut("{id}/permissions")]
        [RequirePermission(UserPermissions.ManageWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> UpdatePermissions(
            string id,
            [FromBody] WorkflowPermissionUpdateDto permissionDto,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.UpdateWorkflowPermissionsAsync(id, permissionDto, cancellationToken);
            }, "Update workflow permissions");
        }

        #endregion

        #region Utility Endpoints

        /// <summary>
        /// Get all workflow tags
        /// </summary>
        [HttpGet("tags")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetTags(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.GetWorkflowTagsAsync(cancellationToken);
            }, "Get workflow tags");
        }

        /// <summary>
        /// Get workflows by tag
        /// </summary>
        [HttpGet("tags/{tag}")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<PagedResponse<WorkflowListDto>>>> GetByTag(
            string tag,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.GetWorkflowsByTagAsync(tag, pagination, cancellationToken);
            }, "Get workflows by tag");
        }

        /// <summary>
        /// Export a workflow
        /// </summary>
        [HttpGet("{id}/export")]
        [RequirePermission(UserPermissions.ViewWorkflows)]
        public async Task<ActionResult<ApiResponse<bool>>> Export(
            string id,
            [FromQuery] string format = "json",
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.ExportWorkflowAsync(id, format, cancellationToken);
            }, "Export workflow");
        }

        /// <summary>
        /// Import a workflow
        /// </summary>
        [HttpPost("import")]
        [RequirePermission(UserPermissions.CreateWorkflows)]
        public async Task<ActionResult<ApiResponse<WorkflowDetailDto>>> Import(
            [FromBody] WorkflowImportDto importDto,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _workflowService.ImportWorkflowAsync(importDto, cancellationToken);
            }, "Import workflow");
        }

        #endregion
    }
}