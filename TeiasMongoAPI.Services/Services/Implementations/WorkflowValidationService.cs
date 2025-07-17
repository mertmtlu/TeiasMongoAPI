using AutoMapper;
using Microsoft.Extensions.Logging;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class WorkflowValidationService : BaseService, IWorkflowValidationService
    {
        private readonly IProgramService _programService;

        public WorkflowValidationService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<WorkflowValidationService> logger,
            IProgramService programService)
            : base(unitOfWork, mapper, logger)
        {
            _programService = programService;
        }

        public async Task<WorkflowValidationResult> ValidateWorkflowAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            var result = new WorkflowValidationResult();

            try
            {
                // Validate workflow structure
                var structureResult = await ValidateWorkflowStructureAsync(workflow, cancellationToken);
                MergeValidationResults(result, structureResult);

                // Validate dependencies
                var dependencyResult = await ValidateWorkflowDependenciesAsync(workflow, cancellationToken);
                MergeValidationResults(result, dependencyResult);

                // Validate nodes
                var nodeResult = await ValidateWorkflowNodesAsync(workflow, cancellationToken);
                MergeValidationResults(result, nodeResult);

                // Validate edges
                var edgeResult = await ValidateWorkflowEdgesAsync(workflow, cancellationToken);
                MergeValidationResults(result, edgeResult);

                // Calculate complexity metrics
                result.ComplexityMetrics = await CalculateComplexityAsync(workflow, cancellationToken);

                result.IsValid = !result.Errors.Any();

                _logger.LogInformation($"Workflow {workflow.Name} validation completed. Valid: {result.IsValid}, Errors: {result.Errors.Count}, Warnings: {result.Warnings.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during workflow validation for {workflow.Name}");
                result.Errors.Add(new WorkflowValidationError
                {
                    ErrorCode = "VALIDATION_ERROR",
                    ErrorType = "SystemError",
                    Message = "An error occurred during workflow validation",
                    Details = ex.Message,
                    Severity = ValidationSeverity.Critical
                });
                result.IsValid = false;
            }

            return result;
        }

        public async Task<WorkflowValidationResult> ValidateWorkflowStructureAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            var result = new WorkflowValidationResult();

            // Check for cycles
            if (await HasCyclesAsync(workflow, cancellationToken))
            {
                result.Errors.Add(new WorkflowValidationError
                {
                    ErrorCode = "CYCLE_DETECTED",
                    ErrorType = "StructuralError",
                    Message = "Workflow contains cycles",
                    Details = "Workflows must be directed acyclic graphs (DAGs)",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Remove cyclic dependencies between nodes"
                });
            }

            // Check for orphaned nodes
            var orphanedNodes = await GetOrphanedNodesAsync(workflow, cancellationToken);
            if (orphanedNodes.Any())
            {
                result.Warnings.Add(new WorkflowValidationWarning
                {
                    WarningCode = "ORPHANED_NODES",
                    WarningType = "StructuralWarning",
                    Message = $"Found {orphanedNodes.Count} orphaned nodes",
                    Details = $"Orphaned nodes: {string.Join(", ", orphanedNodes)}",
                    Severity = ValidationSeverity.Warning,
                    Recommendation = "Connect orphaned nodes to the workflow or remove them"
                });
            }

            // Check for unreachable nodes
            var unreachableNodes = await GetUnreachableNodesAsync(workflow, cancellationToken);
            if (unreachableNodes.Any())
            {
                result.Warnings.Add(new WorkflowValidationWarning
                {
                    WarningCode = "UNREACHABLE_NODES",
                    WarningType = "StructuralWarning",
                    Message = $"Found {unreachableNodes.Count} unreachable nodes",
                    Details = $"Unreachable nodes: {string.Join(", ", unreachableNodes)}",
                    Severity = ValidationSeverity.Warning,
                    Recommendation = "Add paths to unreachable nodes or remove them"
                });
            }

            // Check for start nodes
            var startNodes = workflow.Nodes.Where(n => n.NodeType == WorkflowNodeType.StartNode || 
                                                    !workflow.Edges.Any(e => e.TargetNodeId == n.Id)).ToList();
            if (!startNodes.Any())
            {
                result.Errors.Add(new WorkflowValidationError
                {
                    ErrorCode = "NO_START_NODE",
                    ErrorType = "StructuralError",
                    Message = "Workflow has no start nodes",
                    Details = "Every workflow must have at least one starting point",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Add a start node or ensure at least one node has no incoming edges"
                });
            }

            // Check for end nodes
            var endNodes = workflow.Nodes.Where(n => n.NodeType == WorkflowNodeType.EndNode || 
                                                  !workflow.Edges.Any(e => e.SourceNodeId == n.Id)).ToList();
            if (!endNodes.Any())
            {
                result.Warnings.Add(new WorkflowValidationWarning
                {
                    WarningCode = "NO_END_NODE",
                    WarningType = "StructuralWarning",
                    Message = "Workflow has no end nodes",
                    Details = "Workflows typically should have explicit end points",
                    Severity = ValidationSeverity.Warning,
                    Recommendation = "Add an end node or ensure some nodes have no outgoing edges"
                });
            }

            result.IsValid = !result.Errors.Any();
            return result;
        }

        public async Task<WorkflowValidationResult> ValidateWorkflowDependenciesAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            var result = new WorkflowValidationResult();

            foreach (var node in workflow.Nodes)
            {
                // Check if the program exists
                var program = await _unitOfWork.Programs.GetByIdAsync(node.ProgramId, cancellationToken);
                if (program == null)
                {
                    result.Errors.Add(new WorkflowValidationError
                    {
                        ErrorCode = "PROGRAM_NOT_FOUND",
                        ErrorType = "DependencyError",
                        Message = $"Program not found for node '{node.Name}'",
                        Details = $"Program ID: {node.ProgramId}",
                        NodeId = node.Id,
                        Severity = ValidationSeverity.Error,
                        SuggestedFix = "Ensure the program exists or update the node configuration"
                    });
                    continue;
                }

                // Check if the version exists (if specified)
                if (node.VersionId.HasValue)
                {
                    var version = await _unitOfWork.Versions.GetByIdAsync(node.VersionId.Value, cancellationToken);
                    if (version == null)
                    {
                        result.Errors.Add(new WorkflowValidationError
                        {
                            ErrorCode = "VERSION_NOT_FOUND",
                            ErrorType = "DependencyError",
                            Message = $"Version not found for node '{node.Name}'",
                            Details = $"Version ID: {node.VersionId}",
                            NodeId = node.Id,
                            Severity = ValidationSeverity.Error,
                            SuggestedFix = "Ensure the version exists or use latest version"
                        });
                    }
                    else if (version.ProgramId != node.ProgramId)
                    {
                        result.Errors.Add(new WorkflowValidationError
                        {
                            ErrorCode = "VERSION_PROGRAM_MISMATCH",
                            ErrorType = "DependencyError",
                            Message = $"Version does not belong to the specified program for node '{node.Name}'",
                            Details = $"Version Program ID: {version.ProgramId}, Node Program ID: {node.ProgramId}",
                            NodeId = node.Id,
                            Severity = ValidationSeverity.Error,
                            SuggestedFix = "Use a version that belongs to the specified program"
                        });
                    }
                }

                // Check program status
                if (program.Status != "live")
                {
                    result.Warnings.Add(new WorkflowValidationWarning
                    {
                        WarningCode = "PROGRAM_NOT_LIVE",
                        WarningType = "DependencyWarning",
                        Message = $"Program '{program.Name}' is not in live status",
                        Details = $"Current status: {program.Status}",
                        NodeId = node.Id,
                        Severity = ValidationSeverity.Warning,
                        Recommendation = "Ensure the program is ready for production use"
                    });
                }
            }

            result.IsValid = !result.Errors.Any();
            return result;
        }

        public async Task<WorkflowValidationResult> ValidateWorkflowNodesAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            var result = new WorkflowValidationResult();

            foreach (var node in workflow.Nodes)
            {
                var nodeResult = await ValidateNodeAsync(node, cancellationToken);
                
                // Convert node validation results to workflow validation results
                foreach (var error in nodeResult.Errors)
                {
                    result.Errors.Add(new WorkflowValidationError
                    {
                        ErrorCode = error.ErrorCode,
                        ErrorType = error.ErrorType,
                        Message = error.Message,
                        Details = error.Details,
                        NodeId = node.Id,
                        Severity = error.Severity,
                        SuggestedFix = error.SuggestedFix,
                        Context = error.Context
                    });
                }

                foreach (var warning in nodeResult.Warnings)
                {
                    result.Warnings.Add(new WorkflowValidationWarning
                    {
                        WarningCode = warning.WarningCode,
                        WarningType = warning.WarningType,
                        Message = warning.Message,
                        Details = warning.Details,
                        NodeId = node.Id,
                        Severity = warning.Severity,
                        Recommendation = warning.Recommendation,
                        Context = warning.Context
                    });
                }
            }

            result.IsValid = !result.Errors.Any();
            return result;
        }

        public async Task<WorkflowValidationResult> ValidateWorkflowEdgesAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            var result = new WorkflowValidationResult();

            foreach (var edge in workflow.Edges)
            {
                var edgeResult = await ValidateEdgeAsync(edge, workflow, cancellationToken);
                
                // Convert edge validation results to workflow validation results
                foreach (var error in edgeResult.Errors)
                {
                    result.Errors.Add(new WorkflowValidationError
                    {
                        ErrorCode = error.ErrorCode,
                        ErrorType = error.ErrorType,
                        Message = error.Message,
                        Details = error.Details,
                        EdgeId = edge.Id,
                        Severity = error.Severity,
                        SuggestedFix = error.SuggestedFix,
                        Context = error.Context
                    });
                }

                foreach (var warning in edgeResult.Warnings)
                {
                    result.Warnings.Add(new WorkflowValidationWarning
                    {
                        WarningCode = warning.WarningCode,
                        WarningType = warning.WarningType,
                        Message = warning.Message,
                        Details = warning.Details,
                        EdgeId = edge.Id,
                        Severity = warning.Severity,
                        Recommendation = warning.Recommendation,
                        Context = warning.Context
                    });
                }
            }

            result.IsValid = !result.Errors.Any();
            return result;
        }

        public async Task<WorkflowValidationResult> ValidateWorkflowPermissionsAsync(Workflow workflow, string userId, CancellationToken cancellationToken = default)
        {
            var result = new WorkflowValidationResult();

            // Check if user has permission to execute the workflow
            var hasPermission = await _unitOfWork.Workflows.HasPermissionAsync(workflow._ID, userId, WorkflowPermissionType.Execute, cancellationToken);
            if (!hasPermission)
            {
                result.Errors.Add(new WorkflowValidationError
                {
                    ErrorCode = "INSUFFICIENT_PERMISSIONS",
                    ErrorType = "SecurityError",
                    Message = "User does not have permission to execute this workflow",
                    Details = $"User ID: {userId}, Workflow: {workflow.Name}",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Request execute permission from the workflow owner"
                });
            }

            result.IsValid = !result.Errors.Any();
            return result;
        }

        public async Task<WorkflowValidationResult> ValidateWorkflowExecutionAsync(Workflow workflow, WorkflowExecutionContext context, CancellationToken cancellationToken = default)
        {
            var result = new WorkflowValidationResult();

            // Validate required user inputs
            foreach (var node in workflow.Nodes)
            {
                foreach (var userInput in node.InputConfiguration.UserInputs.Where(ui => ui.IsRequired))
                {
                    var inputKey = $"{node.Id}.{userInput.Name}";
                    if (!context.UserInputs.ToString().Contains(inputKey) || context.UserInputs[inputKey] == null)
                    {
                        result.Errors.Add(new WorkflowValidationError
                        {
                            ErrorCode = "MISSING_USER_INPUT",
                            ErrorType = "ValidationError",
                            Message = $"Required user input missing for node '{node.Name}'",
                            Details = $"Input: {userInput.Name}",
                            NodeId = node.Id,
                            Severity = ValidationSeverity.Error,
                            SuggestedFix = "Provide the required user input"
                        });
                    }
                }
            }

            // Validate execution settings
            if (context.MaxConcurrentNodes <= 0)
            {
                result.Errors.Add(new WorkflowValidationError
                {
                    ErrorCode = "INVALID_CONCURRENT_NODES",
                    ErrorType = "ConfigurationError",
                    Message = "Maximum concurrent nodes must be greater than 0",
                    Details = $"Current value: {context.MaxConcurrentNodes}",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Set maximum concurrent nodes to a positive value"
                });
            }

            if (context.TimeoutMinutes <= 0)
            {
                result.Errors.Add(new WorkflowValidationError
                {
                    ErrorCode = "INVALID_TIMEOUT",
                    ErrorType = "ConfigurationError",
                    Message = "Timeout must be greater than 0",
                    Details = $"Current value: {context.TimeoutMinutes}",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Set timeout to a positive value"
                });
            }

            result.IsValid = !result.Errors.Any();
            return result;
        }

        public async Task<NodeValidationResult> ValidateNodeAsync(WorkflowNode node, CancellationToken cancellationToken = default)
        {
            var result = new NodeValidationResult();

            // Validate node ID
            if (string.IsNullOrEmpty(node.Id))
            {
                result.Errors.Add(new NodeValidationError
                {
                    ErrorCode = "MISSING_NODE_ID",
                    ErrorType = "ValidationError",
                    Message = "Node ID is required",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Provide a unique node ID"
                });
            }

            // Validate node name
            if (string.IsNullOrEmpty(node.Name))
            {
                result.Errors.Add(new NodeValidationError
                {
                    ErrorCode = "MISSING_NODE_NAME",
                    ErrorType = "ValidationError",
                    Message = "Node name is required",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Provide a descriptive node name"
                });
            }

            // Validate program ID
            if (node.ProgramId == default(MongoDB.Bson.ObjectId))
            {
                result.Errors.Add(new NodeValidationError
                {
                    ErrorCode = "MISSING_PROGRAM_ID",
                    ErrorType = "ValidationError",
                    Message = "Program ID is required",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Specify a valid program ID"
                });
            }

            // Validate execution settings
            if (node.ExecutionSettings.TimeoutMinutes <= 0)
            {
                result.Warnings.Add(new NodeValidationWarning
                {
                    WarningCode = "INVALID_TIMEOUT",
                    WarningType = "ConfigurationWarning",
                    Message = "Node timeout should be greater than 0",
                    Details = $"Current value: {node.ExecutionSettings.TimeoutMinutes}",
                    Severity = ValidationSeverity.Warning,
                    Recommendation = "Set a reasonable timeout value"
                });
            }

            // Validate resource limits
            if (node.ExecutionSettings.ResourceLimits.MaxMemoryMb <= 0)
            {
                result.Warnings.Add(new NodeValidationWarning
                {
                    WarningCode = "INVALID_MEMORY_LIMIT",
                    WarningType = "ConfigurationWarning",
                    Message = "Memory limit should be greater than 0",
                    Details = $"Current value: {node.ExecutionSettings.ResourceLimits.MaxMemoryMb}",
                    Severity = ValidationSeverity.Warning,
                    Recommendation = "Set appropriate memory limits"
                });
            }

            result.IsValid = !result.Errors.Any();
            return await Task.FromResult(result);
        }

        public async Task<EdgeValidationResult> ValidateEdgeAsync(WorkflowEdge edge, Workflow workflow, CancellationToken cancellationToken = default)
        {
            var result = new EdgeValidationResult();

            // Validate edge ID
            if (string.IsNullOrEmpty(edge.Id))
            {
                result.Errors.Add(new EdgeValidationError
                {
                    ErrorCode = "MISSING_EDGE_ID",
                    ErrorType = "ValidationError",
                    Message = "Edge ID is required",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Provide a unique edge ID"
                });
            }

            // Validate source node
            if (string.IsNullOrEmpty(edge.SourceNodeId))
            {
                result.Errors.Add(new EdgeValidationError
                {
                    ErrorCode = "MISSING_SOURCE_NODE",
                    ErrorType = "ValidationError",
                    Message = "Source node ID is required",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Specify a valid source node ID"
                });
            }
            else if (!workflow.Nodes.Any(n => n.Id == edge.SourceNodeId))
            {
                result.Errors.Add(new EdgeValidationError
                {
                    ErrorCode = "INVALID_SOURCE_NODE",
                    ErrorType = "ValidationError",
                    Message = $"Source node '{edge.SourceNodeId}' not found in workflow",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Use a valid source node ID"
                });
            }

            // Validate target node
            if (string.IsNullOrEmpty(edge.TargetNodeId))
            {
                result.Errors.Add(new EdgeValidationError
                {
                    ErrorCode = "MISSING_TARGET_NODE",
                    ErrorType = "ValidationError",
                    Message = "Target node ID is required",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Specify a valid target node ID"
                });
            }
            else if (!workflow.Nodes.Any(n => n.Id == edge.TargetNodeId))
            {
                result.Errors.Add(new EdgeValidationError
                {
                    ErrorCode = "INVALID_TARGET_NODE",
                    ErrorType = "ValidationError",
                    Message = $"Target node '{edge.TargetNodeId}' not found in workflow",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Use a valid target node ID"
                });
            }

            // Check for self-loops
            if (edge.SourceNodeId == edge.TargetNodeId)
            {
                result.Errors.Add(new EdgeValidationError
                {
                    ErrorCode = "SELF_LOOP",
                    ErrorType = "StructuralError",
                    Message = "Edge cannot connect a node to itself",
                    Details = $"Node: {edge.SourceNodeId}",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Remove the self-loop or use a different target node"
                });
            }

            result.IsValid = !result.Errors.Any();
            return await Task.FromResult(result);
        }

        public async Task<bool> HasCyclesAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var node in workflow.Nodes)
            {
                if (!visited.Contains(node.Id))
                {
                    if (await HasCyclesDFSAsync(node.Id, workflow, visited, recursionStack, cancellationToken))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private async Task<bool> HasCyclesDFSAsync(string nodeId, Workflow workflow, HashSet<string> visited, HashSet<string> recursionStack, CancellationToken cancellationToken)
        {
            visited.Add(nodeId);
            recursionStack.Add(nodeId);

            var outgoingEdges = workflow.Edges.Where(e => e.SourceNodeId == nodeId && !e.IsDisabled);
            foreach (var edge in outgoingEdges)
            {
                if (!visited.Contains(edge.TargetNodeId))
                {
                    if (await HasCyclesDFSAsync(edge.TargetNodeId, workflow, visited, recursionStack, cancellationToken))
                    {
                        return true;
                    }
                }
                else if (recursionStack.Contains(edge.TargetNodeId))
                {
                    return true;
                }
            }

            recursionStack.Remove(nodeId);
            return false;
        }

        public async Task<List<string>> GetTopologicalOrderAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            var result = new List<string>();
            var visited = new HashSet<string>();

            foreach (var node in workflow.Nodes)
            {
                if (!visited.Contains(node.Id))
                {
                    await TopologicalSortDFSAsync(node.Id, workflow, visited, result, cancellationToken);
                }
            }

            result.Reverse();
            return result;
        }

        private async Task TopologicalSortDFSAsync(string nodeId, Workflow workflow, HashSet<string> visited, List<string> result, CancellationToken cancellationToken)
        {
            visited.Add(nodeId);

            var outgoingEdges = workflow.Edges.Where(e => e.SourceNodeId == nodeId && !e.IsDisabled);
            foreach (var edge in outgoingEdges)
            {
                if (!visited.Contains(edge.TargetNodeId))
                {
                    await TopologicalSortDFSAsync(edge.TargetNodeId, workflow, visited, result, cancellationToken);
                }
            }

            result.Add(nodeId);
        }

        public async Task<Dictionary<string, List<string>>> GetDependencyGraphAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            var graph = new Dictionary<string, List<string>>();

            foreach (var node in workflow.Nodes)
            {
                graph[node.Id] = new List<string>();
            }

            foreach (var edge in workflow.Edges.Where(e => !e.IsDisabled))
            {
                graph[edge.SourceNodeId].Add(edge.TargetNodeId);
            }

            return await Task.FromResult(graph);
        }

        public async Task<List<string>> GetOrphanedNodesAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            var orphanedNodes = new List<string>();

            foreach (var node in workflow.Nodes)
            {
                var hasIncomingEdges = workflow.Edges.Any(e => e.TargetNodeId == node.Id && !e.IsDisabled);
                var hasOutgoingEdges = workflow.Edges.Any(e => e.SourceNodeId == node.Id && !e.IsDisabled);

                if (!hasIncomingEdges && !hasOutgoingEdges)
                {
                    orphanedNodes.Add(node.Id);
                }
            }

            return await Task.FromResult(orphanedNodes);
        }

        public async Task<List<string>> GetUnreachableNodesAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            var reachableNodes = new HashSet<string>();
            var startNodes = workflow.Nodes.Where(n => !workflow.Edges.Any(e => e.TargetNodeId == n.Id && !e.IsDisabled)).ToList();

            foreach (var startNode in startNodes)
            {
                await MarkReachableNodesAsync(startNode.Id, workflow, reachableNodes, cancellationToken);
            }

            var unreachableNodes = workflow.Nodes.Where(n => !reachableNodes.Contains(n.Id)).Select(n => n.Id).ToList();
            return await Task.FromResult(unreachableNodes);
        }

        private async Task MarkReachableNodesAsync(string nodeId, Workflow workflow, HashSet<string> reachableNodes, CancellationToken cancellationToken)
        {
            if (reachableNodes.Contains(nodeId))
                return;

            reachableNodes.Add(nodeId);

            var outgoingEdges = workflow.Edges.Where(e => e.SourceNodeId == nodeId && !e.IsDisabled);
            foreach (var edge in outgoingEdges)
            {
                await MarkReachableNodesAsync(edge.TargetNodeId, workflow, reachableNodes, cancellationToken);
            }
        }

        public async Task<WorkflowComplexityMetrics> CalculateComplexityAsync(Workflow workflow, CancellationToken cancellationToken = default)
        {
            var metrics = new WorkflowComplexityMetrics
            {
                TotalNodes = workflow.Nodes.Count,
                TotalEdges = workflow.Edges.Count(e => !e.IsDisabled),
                ConditionalNodes = workflow.Nodes.Count(n => n.ConditionalExecution != null),
                LoopNodes = workflow.Edges.Count(e => e.EdgeType == WorkflowEdgeType.Loop)
            };

            // Calculate max depth and width
            var levels = await CalculateNodeLevelsAsync(workflow, cancellationToken);
            metrics.MaxDepth = levels.Values.Any() ? levels.Values.Max() : 0;
            metrics.MaxWidth = levels.Values.Any() ? levels.GroupBy(kvp => kvp.Value).Max(g => g.Count()) : 0;

            // Calculate connectivity ratio
            metrics.ConnectivityRatio = metrics.TotalNodes > 0 ? (double)metrics.TotalEdges / metrics.TotalNodes : 0;

            // Calculate parallel branches
            metrics.ParallelBranches = await CalculateParallelBranchesAsync(workflow, cancellationToken);

            // Calculate cyclomatic complexity
            metrics.CyclomaticComplexity = metrics.TotalEdges - metrics.TotalNodes + 2 + metrics.ConditionalNodes;

            // Determine complexity level
            metrics.ComplexityLevel = DetermineComplexityLevel(metrics);

            return metrics;
        }

        private async Task<Dictionary<string, int>> CalculateNodeLevelsAsync(Workflow workflow, CancellationToken cancellationToken)
        {
            var levels = new Dictionary<string, int>();
            var visited = new HashSet<string>();

            var startNodes = workflow.Nodes.Where(n => !workflow.Edges.Any(e => e.TargetNodeId == n.Id && !e.IsDisabled)).ToList();

            foreach (var startNode in startNodes)
            {
                await CalculateNodeLevelsDFSAsync(startNode.Id, 0, workflow, levels, visited, cancellationToken);
            }

            return levels;
        }

        private async Task CalculateNodeLevelsDFSAsync(string nodeId, int level, Workflow workflow, Dictionary<string, int> levels, HashSet<string> visited, CancellationToken cancellationToken)
        {
            if (visited.Contains(nodeId))
                return;

            visited.Add(nodeId);
            levels[nodeId] = Math.Max(levels.GetValueOrDefault(nodeId), level);

            var outgoingEdges = workflow.Edges.Where(e => e.SourceNodeId == nodeId && !e.IsDisabled);
            foreach (var edge in outgoingEdges)
            {
                await CalculateNodeLevelsDFSAsync(edge.TargetNodeId, level + 1, workflow, levels, visited, cancellationToken);
            }
        }

        private async Task<int> CalculateParallelBranchesAsync(Workflow workflow, CancellationToken cancellationToken)
        {
            var maxParallelBranches = 0;

            foreach (var node in workflow.Nodes)
            {
                var outgoingEdges = workflow.Edges.Where(e => e.SourceNodeId == node.Id && !e.IsDisabled).ToList();
                if (outgoingEdges.Count > maxParallelBranches)
                {
                    maxParallelBranches = outgoingEdges.Count;
                }
            }

            return await Task.FromResult(maxParallelBranches);
        }

        private ComplexityLevel DetermineComplexityLevel(WorkflowComplexityMetrics metrics)
        {
            var score = 0;

            if (metrics.TotalNodes > 20) score += 2;
            else if (metrics.TotalNodes > 10) score += 1;

            if (metrics.MaxDepth > 10) score += 2;
            else if (metrics.MaxDepth > 5) score += 1;

            if (metrics.ConditionalNodes > 5) score += 2;
            else if (metrics.ConditionalNodes > 2) score += 1;

            if (metrics.ParallelBranches > 5) score += 2;
            else if (metrics.ParallelBranches > 2) score += 1;

            if (metrics.CyclomaticComplexity > 15) score += 2;
            else if (metrics.CyclomaticComplexity > 10) score += 1;

            return score switch
            {
                0 => ComplexityLevel.Simple,
                1 or 2 => ComplexityLevel.Moderate,
                3 or 4 => ComplexityLevel.Complex,
                _ => ComplexityLevel.VeryComplex
            };
        }

        private void MergeValidationResults(WorkflowValidationResult target, WorkflowValidationResult source)
        {
            target.Errors.AddRange(source.Errors);
            target.Warnings.AddRange(source.Warnings);
            target.Info.AddRange(source.Info);
            
            foreach (var metadata in source.Metadata)
            {
                target.Metadata[metadata.Key] = metadata.Value;
            }
        }
    }
}