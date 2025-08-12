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
    public class WorkflowService : BaseService, IWorkflowService
    {
        private readonly IWorkflowValidationService _validationService;
        private readonly IProgramService _programService;
        private readonly IUserService _userService;

        public WorkflowService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<WorkflowService> logger,
            IWorkflowValidationService validationService,
            IProgramService programService,
            IUserService userService)
            : base(unitOfWork, mapper, logger)
        {
            _validationService = validationService;
            _programService = programService;
            _userService = userService;
        }

        public async Task<PagedResponse<WorkflowListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var workflows = await _unitOfWork.Workflows.GetWorkflowsWithPaginationAsync(
                (pagination.PageNumber - 1) * pagination.PageSize,
                pagination.PageSize,
                cancellationToken: cancellationToken);

            var totalCount = await _unitOfWork.Workflows.CountAsync(cancellationToken: cancellationToken);

            var workflowDtos = workflows.Select(w => MapToListDto(w)).ToList();

            return new PagedResponse<WorkflowListDto>(
                workflowDtos,
                pagination.PageNumber,
                pagination.PageSize,
                (int)totalCount);
        }

        public async Task<WorkflowDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(id), cancellationToken);
            if (workflow == null)
            {
                throw new InvalidOperationException($"Workflow {id} not found");
            }

            return await MapToDetailDtoAsync(workflow, cancellationToken);
        }

        public async Task<WorkflowDetailDto> CreateAsync(WorkflowCreateDto createDto, CancellationToken cancellationToken = default)
        {
            var workflow = _mapper.Map<Workflow>(createDto);
            workflow.CreatedAt = DateTime.UtcNow;
            workflow.Version = 1;

            // Validate workflow before creating
            var validationResult = await _validationService.ValidateWorkflowAsync(workflow, cancellationToken);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Workflow validation failed: {string.Join(", ", validationResult.Errors.Select(e => e.Message))}");
            }

            var createdWorkflow = await _unitOfWork.Workflows.CreateAsync(workflow, cancellationToken);
            _logger.LogInformation($"Created workflow {createdWorkflow.Name} with ID {createdWorkflow._ID}");

            return await MapToDetailDtoAsync(createdWorkflow, cancellationToken);
        }

        public async Task<WorkflowDetailDto> UpdateAsync(string id, WorkflowUpdateDto updateDto, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(id), cancellationToken);
            if (workflow == null)
            {
                throw new InvalidOperationException($"Workflow {id} not found");
            }

            // Map updates to workflow
            _mapper.Map(updateDto, workflow);
            
            var currentTime = DateTime.UtcNow;
            workflow.UpdatedAt = currentTime;

            // Set UpdatedAt timestamps for nodes and edges if they were updated
            if (updateDto.Nodes != null && workflow.Nodes != null)
            {
                foreach (var node in workflow.Nodes)
                {
                    node.UpdatedAt = currentTime;
                }
            }

            if (updateDto.Edges != null && workflow.Edges != null)
            {
                foreach (var edge in workflow.Edges)
                {
                    edge.UpdatedAt = currentTime;
                }
            }

            // Validate updated workflow
            var validationResult = await _validationService.ValidateWorkflowAsync(workflow, cancellationToken);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Workflow validation failed: {string.Join(", ", validationResult.Errors.Select(e => e.Message))}");
            }

            await _unitOfWork.Workflows.UpdateAsync(ObjectId.Parse(id), workflow, cancellationToken);
            _logger.LogInformation($"Updated workflow {workflow.Name} with ID {id}");

            return await MapToDetailDtoAsync(workflow, cancellationToken);
        }

        public async Task<WorkflowDetailDto> UpdateNameDescriptionAsync(string id, WorkflowNameDescriptionUpdateDto updateDto, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(id), cancellationToken);
            if (workflow == null)
            {
                throw new InvalidOperationException($"Workflow {id} not found");
            }

            // Map updates to workflow using AutoMapper
            _mapper.Map(updateDto, workflow);

            workflow.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.Workflows.UpdateAsync(ObjectId.Parse(id), workflow, cancellationToken);
            _logger.LogInformation($"Updated workflow name/description for workflow {workflow.Name} with ID {id}");

            return await MapToDetailDtoAsync(workflow, cancellationToken);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(id), cancellationToken);
            if (workflow == null)
            {
                return false;
            }

            // Check if workflow has active executions
            var activeExecutions = await _unitOfWork.WorkflowExecutions.GetExecutionsByWorkflowIdAsync(ObjectId.Parse(id), cancellationToken);
            if (activeExecutions.Any(e => e.Status == WorkflowExecutionStatus.Running || e.Status == WorkflowExecutionStatus.Pending))
            {
                throw new InvalidOperationException("Cannot delete workflow with active executions");
            }

            var success = await _unitOfWork.Workflows.DeleteAsync(ObjectId.Parse(id), cancellationToken);
            if (success)
            {
                _logger.LogInformation($"Deleted workflow {workflow.Name} with ID {id}");
            }

            return success;
        }

        public async Task<PagedResponse<WorkflowListDto>> GetWorkflowsByUserAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var workflows = await _unitOfWork.Workflows.GetWorkflowsByUserAsync(userId, cancellationToken);
            var pagedWorkflows = workflows.Skip(pagination.PageNumber * pagination.PageSize).Take(pagination.PageSize);

            var workflowDtos = pagedWorkflows.Select(w => MapToListDto(w)).ToList();

            return new PagedResponse<WorkflowListDto>(
                workflowDtos,
                pagination.PageNumber,
                pagination.PageSize,
                workflows.Count());
        }

        public async Task<PagedResponse<WorkflowListDto>> GetWorkflowsByStatusAsync(WorkflowStatus status, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var workflows = await _unitOfWork.Workflows.GetWorkflowsByStatusAsync(status, cancellationToken);
            var pagedWorkflows = workflows.Skip(pagination.PageNumber * pagination.PageSize).Take(pagination.PageSize);

            var workflowDtos = pagedWorkflows.Select(w => MapToListDto(w)).ToList();

            return new PagedResponse<WorkflowListDto>(
                workflowDtos,
                pagination.PageNumber,
                pagination.PageSize,
                workflows.Count());
        }

        public async Task<PagedResponse<WorkflowListDto>> SearchWorkflowsAsync(string searchTerm, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var workflows = await _unitOfWork.Workflows.SearchWorkflowsAsync(searchTerm, cancellationToken);
            var pagedWorkflows = workflows.Skip(pagination.PageNumber * pagination.PageSize).Take(pagination.PageSize);

            var workflowDtos = pagedWorkflows.Select(w => MapToListDto(w)).ToList();

            return new PagedResponse<WorkflowListDto>(
                workflowDtos,
                pagination.PageNumber,
                pagination.PageSize,
                workflows.Count());
        }

        public async Task<PagedResponse<WorkflowListDto>> GetWorkflowTemplatesAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var templates = await _unitOfWork.Workflows.GetWorkflowTemplatesAsync(cancellationToken);
            var pagedTemplates = templates.Skip(pagination.PageNumber * pagination.PageSize).Take(pagination.PageSize);

            var templateDtos = pagedTemplates.Select(w => MapToListDto(w)).ToList();

            return new PagedResponse<WorkflowListDto>(
                templateDtos,
                pagination.PageNumber,
                pagination.PageSize,
                templates.Count());
        }

        public async Task<WorkflowDetailDto> CloneWorkflowAsync(string workflowId, WorkflowCloneDto cloneDto, CancellationToken cancellationToken = default)
        {
            var clonedWorkflow = await _unitOfWork.Workflows.CloneWorkflowAsync(
                ObjectId.Parse(workflowId),
                cloneDto.Name,
                "current_user", // This should come from the current user context
                cancellationToken);

            if (!string.IsNullOrEmpty(cloneDto.Description))
            {
                clonedWorkflow.Description = cloneDto.Description;
            }

            if (cloneDto.Tags.Any())
            {
                clonedWorkflow.Tags = cloneDto.Tags;
            }

            await _unitOfWork.Workflows.UpdateAsync(clonedWorkflow._ID, clonedWorkflow, cancellationToken);

            _logger.LogInformation($"Cloned workflow {workflowId} to {clonedWorkflow.Name} with ID {clonedWorkflow._ID}");

            return await MapToDetailDtoAsync(clonedWorkflow, cancellationToken);
        }

        public async Task<WorkflowValidationResult> ValidateWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            if (workflow == null)
            {
                throw new InvalidOperationException($"Workflow {workflowId} not found");
            }

            return await _validationService.ValidateWorkflowAsync(workflow, cancellationToken);
        }

        public async Task<bool> UpdateWorkflowStatusAsync(string workflowId, WorkflowStatus status, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.Workflows.UpdateWorkflowStatusAsync(ObjectId.Parse(workflowId), status, cancellationToken);
        }

        public async Task<bool> ArchiveWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.Workflows.ArchiveWorkflowAsync(ObjectId.Parse(workflowId), cancellationToken);
        }

        public async Task<bool> RestoreWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.Workflows.RestoreWorkflowAsync(ObjectId.Parse(workflowId), cancellationToken);
        }

        public async Task<WorkflowPermissionDto> GetWorkflowPermissionsAsync(string workflowId, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            if (workflow == null)
            {
                throw new InvalidOperationException($"Workflow {workflowId} not found");
            }

            return _mapper.Map<WorkflowPermissionDto>(workflow.Permissions);
        }

        public async Task<bool> UpdateWorkflowPermissionsAsync(string workflowId, WorkflowPermissionUpdateDto permissionDto, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            if (workflow == null)
            {
                return false;
            }

            // Update permissions
            if (permissionDto.IsPublic.HasValue)
                workflow.Permissions.IsPublic = permissionDto.IsPublic.Value;

            if (permissionDto.AllowedUsers != null)
                workflow.Permissions.AllowedUsers = permissionDto.AllowedUsers;

            if (permissionDto.AllowedRoles != null)
                workflow.Permissions.AllowedRoles = permissionDto.AllowedRoles;

            if (permissionDto.Permissions != null)
                workflow.Permissions.Permissions = _mapper.Map<List<WorkflowUserPermission>>(permissionDto.Permissions);

            workflow.UpdatedAt = DateTime.UtcNow;

            return await _unitOfWork.Workflows.UpdateAsync(ObjectId.Parse(workflowId), workflow, cancellationToken);
        }

        public async Task<bool> HasPermissionAsync(string workflowId, string userId, WorkflowPermissionType permission, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.Workflows.HasPermissionAsync(ObjectId.Parse(workflowId), userId, permission, cancellationToken);
        }

        public async Task<WorkflowStatisticsDto> GetWorkflowStatisticsAsync(string workflowId, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.WorkflowExecutions.GetExecutionsByWorkflowIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            var executionsList = executions.ToList();

            var stats = new WorkflowStatisticsDto
            {
                TotalExecutions = executionsList.Count,
                SuccessfulExecutions = executionsList.Count(e => e.Status == WorkflowExecutionStatus.Completed),
                FailedExecutions = executionsList.Count(e => e.Status == WorkflowExecutionStatus.Failed),
                CancelledExecutions = executionsList.Count(e => e.Status == WorkflowExecutionStatus.Cancelled),
                LastExecutionDate = executionsList.OrderByDescending(e => e.StartedAt).FirstOrDefault()?.StartedAt
            };

            if (stats.TotalExecutions > 0)
            {
                stats.SuccessRate = (double)stats.SuccessfulExecutions / stats.TotalExecutions * 100;

                var completedExecutions = executionsList.Where(e => e.CompletedAt.HasValue).ToList();
                if (completedExecutions.Any())
                {
                    var durations = completedExecutions.Select(e => e.CompletedAt!.Value - e.StartedAt).ToList();
                    stats.AverageExecutionTime = TimeSpan.FromTicks((long)durations.Average(d => d.Ticks));
                    stats.FastestExecutionTime = durations.Min();
                    stats.SlowestExecutionTime = durations.Max();
                }
            }

            // Group executions by status
            stats.ExecutionsByStatus = executionsList
                .GroupBy(e => e.Status.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            // Group executions by month
            stats.ExecutionsByMonth = executionsList
                .GroupBy(e => e.StartedAt.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Count());

            // Recent executions
            stats.RecentExecutions = executionsList
                .OrderByDescending(e => e.StartedAt)
                .Take(5)
                .Select(e => _mapper.Map<WorkflowExecutionSummaryDto>(e))
                .ToList();

            return stats;
        }

        public async Task<List<WorkflowExecutionSummaryDto>> GetWorkflowExecutionHistoryAsync(string workflowId, int limit = 10, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.WorkflowExecutions.GetExecutionHistoryAsync(ObjectId.Parse(workflowId), limit, cancellationToken);

            var tasks = executions.Select(async e =>
            {
                var dto = _mapper.Map<WorkflowExecutionSummaryDto>(e);
                dto.ExecutedByUserName = (await _userService.GetByIdAsync(dto.ExecutedBy)).FullName;
                return dto;
            });

            return (await Task.WhenAll(tasks)).ToList();
        }

        public async Task<WorkflowComplexityMetrics> GetWorkflowComplexityAsync(string workflowId, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            if (workflow == null)
            {
                throw new InvalidOperationException($"Workflow {workflowId} not found");
            }

            return await _validationService.CalculateComplexityAsync(workflow, cancellationToken);
        }

        public async Task<List<string>> GetWorkflowTagsAsync(CancellationToken cancellationToken = default)
        {
            var workflows = await _unitOfWork.Workflows.GetAllAsync(cancellationToken);
            return workflows.SelectMany(w => w.Tags).Distinct().OrderBy(t => t).ToList();
        }

        public async Task<PagedResponse<WorkflowListDto>> GetWorkflowsByTagAsync(string tag, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var workflows = await _unitOfWork.Workflows.GetWorkflowsByTagAsync(tag, cancellationToken);
            var pagedWorkflows = workflows.Skip(pagination.PageNumber * pagination.PageSize).Take(pagination.PageSize);

            var workflowDtos = pagedWorkflows.Select(w => MapToListDto(w)).ToList();

            return new PagedResponse<WorkflowListDto>(
                workflowDtos,
                pagination.PageNumber,
                pagination.PageSize,
                workflows.Count());
        }

        public async Task<bool> AddNodeToWorkflowAsync(string workflowId, WorkflowNodeCreateDto nodeDto, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            if (workflow == null)
            {
                return false;
            }

            var node = _mapper.Map<WorkflowNode>(nodeDto);
            node.CreatedAt = DateTime.UtcNow;

            workflow.Nodes.Add(node);
            workflow.UpdatedAt = DateTime.UtcNow;

            return await _unitOfWork.Workflows.UpdateAsync(ObjectId.Parse(workflowId), workflow, cancellationToken);
        }

        public async Task<bool> UpdateNodeInWorkflowAsync(string workflowId, string nodeId, WorkflowNodeUpdateDto nodeDto, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            if (workflow == null)
            {
                return false;
            }

            var node = workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
            {
                return false;
            }

            _mapper.Map(nodeDto, node);
            node.UpdatedAt = DateTime.UtcNow;
            workflow.UpdatedAt = DateTime.UtcNow;

            return await _unitOfWork.Workflows.UpdateAsync(ObjectId.Parse(workflowId), workflow, cancellationToken);
        }

        public async Task<bool> RemoveNodeFromWorkflowAsync(string workflowId, string nodeId, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            if (workflow == null)
            {
                return false;
            }

            var node = workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
            {
                return false;
            }

            workflow.Nodes.Remove(node);
            
            // Remove all edges connected to this node
            workflow.Edges.RemoveAll(e => e.SourceNodeId == nodeId || e.TargetNodeId == nodeId);
            
            workflow.UpdatedAt = DateTime.UtcNow;

            return await _unitOfWork.Workflows.UpdateAsync(ObjectId.Parse(workflowId), workflow, cancellationToken);
        }

        public async Task<bool> AddEdgeToWorkflowAsync(string workflowId, WorkflowEdgeCreateDto edgeDto, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            if (workflow == null)
            {
                return false;
            }

            var edge = _mapper.Map<WorkflowEdge>(edgeDto);
            edge.CreatedAt = DateTime.UtcNow;

            workflow.Edges.Add(edge);
            workflow.UpdatedAt = DateTime.UtcNow;

            return await _unitOfWork.Workflows.UpdateAsync(ObjectId.Parse(workflowId), workflow, cancellationToken);
        }

        public async Task<bool> UpdateEdgeInWorkflowAsync(string workflowId, string edgeId, WorkflowEdgeUpdateDto edgeDto, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            if (workflow == null)
            {
                return false;
            }

            var edge = workflow.Edges.FirstOrDefault(e => e.Id == edgeId);
            if (edge == null)
            {
                return false;
            }

            _mapper.Map(edgeDto, edge);
            edge.UpdatedAt = DateTime.UtcNow;
            workflow.UpdatedAt = DateTime.UtcNow;

            return await _unitOfWork.Workflows.UpdateAsync(ObjectId.Parse(workflowId), workflow, cancellationToken);
        }

        public async Task<bool> RemoveEdgeFromWorkflowAsync(string workflowId, string edgeId, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            if (workflow == null)
            {
                return false;
            }

            var edge = workflow.Edges.FirstOrDefault(e => e.Id == edgeId);
            if (edge == null)
            {
                return false;
            }

            workflow.Edges.Remove(edge);
            workflow.UpdatedAt = DateTime.UtcNow;

            return await _unitOfWork.Workflows.UpdateAsync(ObjectId.Parse(workflowId), workflow, cancellationToken);
        }

        public async Task<WorkflowExecutionPlanDto> GetExecutionPlanAsync(string workflowId, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            if (workflow == null)
            {
                throw new InvalidOperationException($"Workflow {workflowId} not found");
            }

            var executionOrder = await _validationService.GetTopologicalOrderAsync(workflow, cancellationToken);
            var dependencyGraph = await _validationService.GetDependencyGraphAsync(workflow, cancellationToken);
            var complexityMetrics = await _validationService.CalculateComplexityAsync(workflow, cancellationToken);
            var validationResult = await _validationService.ValidateWorkflowAsync(workflow, cancellationToken);

            var executionPlan = new WorkflowExecutionPlanDto
            {
                WorkflowId = workflowId,
                WorkflowName = workflow.Name,
                ExecutionOrder = executionOrder,
                DependencyGraph = dependencyGraph,
                MaxConcurrentNodes = workflow.Settings.MaxConcurrentNodes,
                ValidationResult = validationResult
            };

            // Calculate execution phases
            var phases = CalculateExecutionPhases(workflow, executionOrder, dependencyGraph);
            executionPlan.ExecutionPhases = phases;

            // Estimate execution time
            executionPlan.EstimatedExecutionTime = workflow.AverageExecutionTime ?? TimeSpan.FromMinutes(30);

            return executionPlan;
        }

        public async Task<bool> ExportWorkflowAsync(string workflowId, string format, CancellationToken cancellationToken = default)
        {
            var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(workflowId), cancellationToken);
            if (workflow == null)
            {
                return false;
            }

            // Export logic would go here
            // For now, just return true to indicate success
            return true;
        }

        public async Task<WorkflowDetailDto> ImportWorkflowAsync(WorkflowImportDto importDto, CancellationToken cancellationToken = default)
        {
            // Import logic would go here
            // For now, throw not implemented
            throw new NotImplementedException("Workflow import functionality not yet implemented");
        }

        private WorkflowListDto MapToListDto(Workflow workflow)
        {
            return new WorkflowListDto
            {
                Id = workflow._ID.ToString(),
                Name = workflow.Name,
                Description = workflow.Description,
                Creator = workflow.Creator,
                CreatedAt = workflow.CreatedAt,
                UpdatedAt = workflow.UpdatedAt,
                Status = workflow.Status,
                Version = workflow.Version,
                Tags = workflow.Tags,
                IsTemplate = workflow.IsTemplate,
                ExecutionCount = workflow.ExecutionCount,
                AverageExecutionTime = workflow.AverageExecutionTime,
                NodeCount = workflow.Nodes.Count,
                EdgeCount = workflow.Edges.Count,
                IsPublic = workflow.Permissions.IsPublic
            };
        }

        private async Task<WorkflowDetailDto> MapToDetailDtoAsync(Workflow workflow, CancellationToken cancellationToken)
        {
            var detailDto = _mapper.Map<WorkflowDetailDto>(workflow);
            
            // Add validation results
            detailDto.ValidationResult = await _validationService.ValidateWorkflowAsync(workflow, cancellationToken);
            
            // Add complexity metrics
            detailDto.ComplexityMetrics = await _validationService.CalculateComplexityAsync(workflow, cancellationToken);

            return detailDto;
        }

        private List<WorkflowExecutionPhaseDto> CalculateExecutionPhases(Workflow workflow, List<string> executionOrder, Dictionary<string, List<string>> dependencyGraph)
        {
            var phases = new List<WorkflowExecutionPhaseDto>();
            var processed = new HashSet<string>();
            var phaseNumber = 1;

            while (processed.Count < workflow.Nodes.Count)
            {
                var currentPhaseNodes = new List<string>();

                foreach (var nodeId in executionOrder)
                {
                    if (processed.Contains(nodeId))
                        continue;

                    // Check if all dependencies are processed
                    var dependencies = dependencyGraph.ContainsKey(nodeId) ? dependencyGraph[nodeId] : new List<string>();
                    if (dependencies.All(dep => processed.Contains(dep)))
                    {
                        currentPhaseNodes.Add(nodeId);
                    }
                }

                if (currentPhaseNodes.Any())
                {
                    var phase = new WorkflowExecutionPhaseDto
                    {
                        PhaseNumber = phaseNumber,
                        PhaseName = $"Phase {phaseNumber}",
                        NodeIds = currentPhaseNodes,
                        NodeNames = currentPhaseNodes.Select(id => workflow.Nodes.First(n => n.Id == id).Name).ToList(),
                        CanRunInParallel = currentPhaseNodes.Count > 1,
                        EstimatedDuration = TimeSpan.FromMinutes(5) // Default estimation
                    };

                    phases.Add(phase);
                    processed.UnionWith(currentPhaseNodes);
                    phaseNumber++;
                }
                else
                {
                    break; // Avoid infinite loop
                }
            }

            return phases;
        }
    }
}