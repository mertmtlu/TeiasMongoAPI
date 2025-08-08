using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.UIWorkflow;
using TeiasMongoAPI.Services.DTOs.Response.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Interfaces.Execution;
using TeiasMongoAPI.Services.Services.Base;
using TeiasMongoAPI.Services.Helpers;
using MSLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class WorkflowExecutionEngine : BaseService, IWorkflowExecutionEngine
    {
        private readonly IWorkflowValidationService _validationService;
        private readonly IProjectExecutionEngine _projectExecutionEngine;
        private readonly IFileStorageService _fileStorageService;
        private readonly IWorkflowSessionManager _sessionManager;
        private readonly IBackgroundTaskQueue _backgroundTaskQueue;
        private readonly IUserService _userService;
        private readonly IWorkflowNotificationService _notificationService;
        private readonly IUIInteractionService _uiInteractionService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly SemaphoreSlim _executionSemaphore = new(10, 10); // Limit concurrent workflows
        private static readonly object _workflowExecutionLock = new object();

        private async Task UpdateNodeExecutionAndSyncSessionAsync(string executionId, string nodeId, NodeExecution nodeExecution, CancellationToken cancellationToken)
        {
            // Update database
            await _unitOfWork.WorkflowExecutions.UpdateNodeExecutionAsync(ObjectId.Parse(executionId), nodeId, nodeExecution, cancellationToken);

            // Update session state to keep it in sync with database
            if (_sessionManager.TryGetSession(executionId, out var session))
            {
                var sessionNodeExecution = session.Execution.NodeExecutions.FirstOrDefault(ne => ne.NodeId == nodeId);
                if (sessionNodeExecution != null)
                {
                    // Sync the session state with the updated database record
                    sessionNodeExecution.Status = nodeExecution.Status;
                    sessionNodeExecution.StartedAt = nodeExecution.StartedAt;
                    sessionNodeExecution.CompletedAt = nodeExecution.CompletedAt;
                    sessionNodeExecution.RetryCount = nodeExecution.RetryCount;
                    sessionNodeExecution.OutputData = nodeExecution.OutputData;
                    sessionNodeExecution.WasSkipped = nodeExecution.WasSkipped;
                    sessionNodeExecution.SkipReason = nodeExecution.SkipReason;

                    _logger.LogDebug($"Synchronized session state for node {nodeId} in execution {executionId} - Status: {nodeExecution.Status}");
                }
            }
        }

        public WorkflowExecutionEngine(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<WorkflowExecutionEngine> logger,
            IWorkflowValidationService validationService,
            IProjectExecutionEngine projectExecutionEngine,
            IFileStorageService fileStorageService,
            IWorkflowSessionManager sessionManager,
            IBackgroundTaskQueue backgroundTaskQueue,
            IUserService userService,
            IWorkflowNotificationService notificationService,
            IUIInteractionService uiInteractionService,
            IServiceScopeFactory serviceScopeFactory)
            : base(unitOfWork, mapper, logger)
        {
            _validationService = validationService;
            _projectExecutionEngine = projectExecutionEngine;
            _fileStorageService = fileStorageService;
            _sessionManager = sessionManager;
            _backgroundTaskQueue = backgroundTaskQueue;
            _logger.LogInformation($"WorkflowExecutionEngine instance created: {GetHashCode()}");
            _userService = userService;
            _notificationService = notificationService;
            _uiInteractionService = uiInteractionService;
            _serviceScopeFactory = serviceScopeFactory;
        }

        private async Task EmitUIInteractionCreatedAsync(string workflowId, string executionId, UIInteraction interaction, CancellationToken cancellationToken = default)
        {
            try
            {
                var eventArgs = new UIInteractionCreatedEventArgs
                {
                    InteractionId = interaction._ID.ToString(),
                    ExecutionId = executionId, // ADD THIS
                    NodeId = interaction.NodeId,
                    InteractionType = interaction.InteractionType.ToString(),
                    Status = interaction.Status.ToString(),
                    Title = interaction.Title,
                    Description = interaction.Description,
                    InputSchema = interaction.InputSchema,
                    ContextData = interaction.ContextData, // ADD THIS
                    CreatedAt = interaction.CreatedAt,
                    Timeout = interaction.Timeout
                };

                await _notificationService.NotifyUIInteractionCreatedAsync(workflowId, eventArgs, cancellationToken);
                _logger.LogDebug($"Emitted UIInteractionCreated event for workflow {workflowId}, interaction {interaction._ID}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to emit UIInteractionCreated event for workflow {workflowId}");
            }
        }

        private async Task EmitUIInteractionStatusChangedAsync(string workflowId, string executionId, string interactionId, UIInteractionStatus status, Dictionary<string, object> outputData = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var eventArgs = new UIInteractionStatusChangedEventArgs
                {
                    InteractionId = interactionId,
                    ExecutionId = executionId, // ADD THIS
                    Status = status.ToString(),
                    OutputData = outputData,
                    CompletedAt = status == UIInteractionStatus.Completed || status == UIInteractionStatus.Cancelled || status == UIInteractionStatus.Timeout ? DateTime.UtcNow : (DateTime?)null
                };

                await _notificationService.NotifyUIInteractionStatusChangedAsync(workflowId, eventArgs, cancellationToken);
                _logger.LogDebug($"Emitted UIInteractionStatusChanged event for workflow {workflowId}, interaction {interactionId}, status {status}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to emit UIInteractionStatusChanged event for workflow {workflowId}");
            }
        }

        private async Task EmitUIInteractionAvailableAsync(string workflowId, string nodeId, string interactionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var eventArgs = new UIInteractionAvailableEventArgs
                {
                    NodeId = nodeId,
                    InteractionId = interactionId,
                    Timestamp = DateTime.UtcNow
                };

                await _notificationService.NotifyUIInteractionAvailableAsync(workflowId, eventArgs, cancellationToken);
                _logger.LogDebug($"Emitted UIInteractionAvailable event for workflow {workflowId}, node {nodeId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to emit UIInteractionAvailable event for workflow {workflowId}");
            }
        }

        public async Task<WorkflowExecutionResponseDto> ExecuteWorkflowAsync(WorkflowExecutionRequest request, ObjectId currentUserId, CancellationToken cancellationToken = default)
        {
            var executionId = ObjectId.GenerateNewId();
            _logger.LogInformation($"[Instance {GetHashCode()}] Starting workflow execution {executionId} for workflow {request.WorkflowId} by user {currentUserId}");

            try
            {
                // Add initial execution log
                await AddExecutionLogAsync(executionId.ToString(), TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info, "Workflow execution started", metadata: new Dictionary<string, object>
                {
                    ["workflowId"] = request.WorkflowId,
                    ["userId"] = currentUserId.ToString(),
                    ["requestOptions"] = request.Options.ToBsonDocument()
                }, cancellationToken: cancellationToken);
                // Get workflow
                var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(request.WorkflowId), cancellationToken);
                if (workflow == null)
                {
                    await AddExecutionLogAsync(executionId.ToString(), TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Error, $"Workflow {request.WorkflowId} not found", cancellationToken: cancellationToken);
                    throw new InvalidOperationException($"Workflow {request.WorkflowId} not found");
                }

                await AddExecutionLogAsync(executionId.ToString(), TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info, $"Workflow '{workflow.Name}' loaded successfully", metadata: new Dictionary<string, object>
                {
                    ["nodeCount"] = workflow.Nodes.Count,
                    ["workflowVersion"] = workflow.Version
                }, cancellationToken: cancellationToken);

                // Validate workflow
                await AddExecutionLogAsync(executionId.ToString(), TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info, "Validating workflow configuration", cancellationToken: cancellationToken);
                var validationResult = await _validationService.ValidateWorkflowAsync(workflow, cancellationToken);
                if (!validationResult.IsValid)
                {
                    var errors = string.Join(", ", validationResult.Errors.Select(e => e.Message));
                    await AddExecutionLogAsync(executionId.ToString(), TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Error, $"Workflow validation failed: {errors}", cancellationToken: cancellationToken);
                    throw new InvalidOperationException($"Workflow validation failed: {errors}");
                }
                await AddExecutionLogAsync(executionId.ToString(), TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info, "Workflow validation completed successfully", cancellationToken: cancellationToken);


                // Validate permissions
                var permissionResult = await _validationService.ValidateWorkflowPermissionsAsync(workflow, currentUserId.ToString(), cancellationToken);
                if (!permissionResult.IsValid && false)
                {
                    throw new UnauthorizedAccessException($"User {currentUserId} does not have permission to execute workflow {workflow.Name}");
                }

                // Validate execution context
                var executionContext = _mapper.Map<WorkflowExecutionContext>(request.ExecutionContext);
                var executionValidationResult = await _validationService.ValidateWorkflowExecutionAsync(workflow, executionContext, cancellationToken);
                if (!executionValidationResult.IsValid)
                {
                    throw new InvalidOperationException($"Execution validation failed: {string.Join(", ", executionValidationResult.Errors.Select(e => e.Message))}");
                }

                // Create workflow execution record
                var execution = new WorkflowExecution
                {
                    _ID = executionId,
                    WorkflowId = workflow._ID,
                    WorkflowVersion = workflow.Version,
                    ExecutionName = request.ExecutionName,
                    ExecutedBy = currentUserId.ToString(),
                    StartedAt = DateTime.UtcNow,
                    Status = WorkflowExecutionStatus.Running,
                    ExecutionContext = executionContext,
                    Progress = new WorkflowExecutionProgress
                    {
                        TotalNodes = workflow.Nodes.Count,
                        CurrentPhase = "Initializing"
                    },
                    TriggerType = WorkflowTriggerType.Manual,
                    Metadata = request.Metadata.ToBsonDocument()
                };

                // Initialize node executions
                foreach (var node in workflow.Nodes)
                {
                    execution.NodeExecutions.Add(new NodeExecution
                    {
                        NodeId = node.Id,
                        NodeName = node.Name,
                        ProgramId = node.ProgramId,
                        Status = NodeExecutionStatus.Pending,
                        MaxRetries = node.ExecutionSettings.RetryCount
                    });
                }

                // Save execution record
                await _unitOfWork.WorkflowExecutions.CreateAsync(execution, cancellationToken);

                // Create execution session
                var session = new WorkflowExecutionSession
                {
                    ExecutionId = executionId.ToString(),
                    Workflow = workflow,
                    Execution = execution,
                    Options = request.Options,
                    NodeOutputs = new ConcurrentDictionary<string, WorkflowDataContract>(),
                    RunningNodes = new ConcurrentDictionary<string, Task<NodeExecution>>(),
                    CompletedNodes = new HashSet<string>(),
                    FailedNodes = new HashSet<string>(),
                    CancellationTokenSource = new CancellationTokenSource()
                };

                // Try to add session atomically - this will fail if workflow is already running
                if (!_sessionManager.TryAddSessionIfNotRunning(executionId.ToString(), session, request.WorkflowId))
                {
                    var runningExecutionId = _sessionManager.GetRunningExecutionId(request.WorkflowId);
                    _logger.LogWarning($"Workflow {request.WorkflowId} is already running with execution ID {runningExecutionId}");
                    throw new InvalidOperationException($"Workflow {request.WorkflowId} is already running. Execution ID: {runningExecutionId}");
                }

                // Queue background task execution
                await _backgroundTaskQueue.QueueBackgroundWorkItemAsync(async (serviceProvider, ct) =>
                {
                    // Resolve a new scoped instance of IWorkflowExecutionEngine
                    var scopedWorkflowEngine = serviceProvider.GetRequiredService<IWorkflowExecutionEngine>();

                    // Since we need access to the internal method, we'll cast to the concrete type
                    if (scopedWorkflowEngine is WorkflowExecutionEngine engine)
                    {
                        await engine.ExecuteWorkflowInternalAsync(session, ct);
                    }
                });

                _logger.LogInformation($"Workflow execution {executionId} started successfully");
                return _mapper.Map<WorkflowExecutionResponseDto>(execution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to start workflow execution {executionId}");
                throw;
            }
        }

        public async Task ExecuteWorkflowInternalAsync(WorkflowExecutionSession session, CancellationToken cancellationToken)
        {
            var combinedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, session.CancellationTokenSource.Token).Token;

            try
            {
                await _executionSemaphore.WaitAsync(combinedCancellationToken);

                // Update status and add log
                await UpdateExecutionStatusAsync(session.ExecutionId, WorkflowExecutionStatus.Running, "Analyzing dependencies", cancellationToken);
                await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info, "Analyzing workflow dependencies and execution order", cancellationToken: cancellationToken);

                // Get execution order for reference
                var executionOrder = await _validationService.GetTopologicalOrderAsync(session.Workflow, cancellationToken);
                _logger.LogInformation($"Execution order for workflow {session.ExecutionId}: {string.Join(" -> ", executionOrder)}");
                await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info, $"Execution order determined: {string.Join(" -> ", executionOrder)}", metadata: new Dictionary<string, object>
                {
                    ["totalNodes"] = executionOrder.Count,
                    ["executionOrder"] = executionOrder
                }, cancellationToken: cancellationToken);

                // Use dynamic execution approach to prevent race conditions
                await ExecuteWorkflowDynamicallyAsync(session, combinedCancellationToken);

                // Finalize execution
                await FinalizeExecutionAsync(session, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"Workflow execution {session.ExecutionId} was cancelled");
                await UpdateExecutionStatusAsync(session.ExecutionId, WorkflowExecutionStatus.Cancelled, "Execution cancelled", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during workflow execution {session.ExecutionId}");
                await HandleExecutionErrorAsync(session, ex, cancellationToken);
            }
            finally
            {
                // Wait for all running nodes to complete before cleanup
                await WaitForAllRunningNodesToComplete(session);
                _executionSemaphore.Release();

                // Only remove session if no nodes are waiting for UI input
                // This ensures sessions remain available for UI interaction completion
                var waitingNodes = session.Execution.NodeExecutions.Count(ne => ne.Status == NodeExecutionStatus.WaitingForInput);
                if (waitingNodes == 0)
                {
                    _sessionManager.RemoveSession(session.ExecutionId);
                    _logger.LogDebug("Session {ExecutionId} removed as no nodes are waiting for input", session.ExecutionId);
                }
                else
                {
                    _logger.LogInformation("Session {ExecutionId} kept alive for {WaitingNodes} nodes waiting for UI input",
                        session.ExecutionId, waitingNodes);
                }
            }
        }

        private async Task<bool> AreDependenciesSatisfiedAsync(WorkflowExecutionSession session, string nodeId, CancellationToken cancellationToken)
        {
            var incomingEdges = session.Workflow.Edges.Where(e => e.TargetNodeId == nodeId && !e.IsDisabled);

            foreach (var edge in incomingEdges)
            {
                var sourceNodeExecution = session.Execution.NodeExecutions.First(ne => ne.NodeId == edge.SourceNodeId);

                // Check if source node is completed or if this is an optional dependency
                if (sourceNodeExecution.Status != NodeExecutionStatus.Completed &&
                    sourceNodeExecution.Status != NodeExecutionStatus.Skipped)
                {
                    // Check if this is an optional dependency
                    var targetNode = session.Workflow.Nodes.First(n => n.Id == nodeId);
                    var inputMapping = targetNode.InputConfiguration.InputMappings.FirstOrDefault(im => im.SourceNodeId == edge.SourceNodeId);

                    if (inputMapping?.IsOptional == true)
                    {
                        continue; // Optional dependency, can proceed
                    }

                    return false; // Required dependency not satisfied
                }
            }

            return true;
        }

        private async Task ExecuteWorkflowDynamicallyAsync(WorkflowExecutionSession session, CancellationToken cancellationToken)
        {
            var nodeSemaphore = new SemaphoreSlim(session.Options.MaxConcurrentNodes, session.Options.MaxConcurrentNodes);
            var nodeLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
            var completionSource = new TaskCompletionSource<object>();

            // Update phase to indicate we're starting node execution
            await UpdateExecutionStatusAsync(session.ExecutionId, WorkflowExecutionStatus.Running, "Executing nodes", cancellationToken);

            try
            {
                // Start with nodes that have no dependencies
                var initialNodes = await GetNodesWithNoDependenciesAsync(session, cancellationToken);

                if (!initialNodes.Any())
                {
                    _logger.LogWarning($"No nodes without dependencies found in execution {session.ExecutionId}");
                    return;
                }

                // Track workflow completion
                var totalEnabledNodes = session.Workflow.Nodes.Count(n => !n.IsDisabled);
                var completedNodesCount = 0;

                // Start initial nodes
                foreach (var nodeId in initialNodes)
                {
                    await TryStartNodeExecutionAsync(session, nodeId, nodeSemaphore, nodeLocks,
                        () =>
                        {
                            var completed = Interlocked.Increment(ref completedNodesCount);
                            if (completed >= totalEnabledNodes)
                            {
                                completionSource.TrySetResult(null);
                            }
                        },
                        cancellationToken);
                }

                // Wait for all nodes to complete
                await completionSource.Task;
            }
            finally
            {
                nodeSemaphore.Dispose();
                foreach (var lockPair in nodeLocks)
                {
                    lockPair.Value.Dispose();
                }
            }
        }

        private async Task<List<string>> GetEligibleNodesAsync(WorkflowExecutionSession session, HashSet<string> processedNodes, CancellationToken cancellationToken)
        {
            var eligibleNodes = new List<string>();

            foreach (var node in session.Workflow.Nodes.Where(n => !n.IsDisabled))
            {
                // Skip if already processed, completed, failed, or currently running
                if (processedNodes.Contains(node.Id) ||
                    session.CompletedNodes.Contains(node.Id) ||
                    session.FailedNodes.Contains(node.Id) ||
                    session.RunningNodes.ContainsKey(node.Id))
                {
                    continue;
                }

                // Check if dependencies are satisfied
                if (await AreDependenciesSatisfiedAsync(session, node.Id, cancellationToken))
                {
                    eligibleNodes.Add(node.Id);
                }
            }

            return eligibleNodes;
        }

        private async Task<List<string>> GetNodesWithNoDependenciesAsync(WorkflowExecutionSession session, CancellationToken cancellationToken)
        {
            var nodesWithDependencies = new HashSet<string>(
                session.Workflow.Edges
                    .Where(e => !e.IsDisabled)
                    .Select(e => e.TargetNodeId)
            );

            return session.Workflow.Nodes
                .Where(n => !n.IsDisabled && !nodesWithDependencies.Contains(n.Id))
                .Select(n => n.Id)
                .ToList();
        }

        private async Task TryStartNodeExecutionAsync(WorkflowExecutionSession session, string nodeId,
            SemaphoreSlim globalSemaphore, ConcurrentDictionary<string, SemaphoreSlim> nodeLocks,
            Action onNodeCompleted, CancellationToken cancellationToken)
        {
            // Get or create a lock for this specific node to prevent duplicate execution
            var nodeLock = nodeLocks.GetOrAdd(nodeId, _ => new SemaphoreSlim(1, 1));

            // Try to acquire the node-specific lock
            if (!await nodeLock.WaitAsync(0, cancellationToken))
            {
                // Node is already being processed
                return;
            }

            try
            {
                // Double-check that the node isn't already processed or running
                if (session.CompletedNodes.Contains(nodeId) ||
                    session.FailedNodes.Contains(nodeId) ||
                    session.RunningNodes.ContainsKey(nodeId))
                {
                    return;
                }

                // Check if dependencies are satisfied
                if (!await AreDependenciesSatisfiedAsync(session, nodeId, cancellationToken))
                {
                    return;
                }

                // Start the node execution
                var nodeTask = ExecuteNodeWithTrackingAsync(session, nodeId, globalSemaphore, cancellationToken);
                session.RunningNodes[nodeId] = nodeTask;

                // Log node start
                await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info,
                    $"Starting execution of {1} nodes",
                    metadata: new Dictionary<string, object>
                    {
                        ["startingNodes"] = new[] { nodeId },
                        ["availableSlots"] = session.Options.MaxConcurrentNodes - session.RunningNodes.Count + 1,
                        ["maxConcurrentNodes"] = session.Options.MaxConcurrentNodes,
                        ["currentlyRunning"] = session.RunningNodes.Count
                    }, cancellationToken: cancellationToken);

                // Set up continuation to handle completion and trigger dependent nodes
                _ = nodeTask.ContinueWith(async (task) =>
                {
                    try
                    {
                        session.RunningNodes.TryRemove(nodeId, out _);

                        // Notify completion for workflow tracking
                        onNodeCompleted();

                        // Process dependent nodes
                        await ProcessDependentNodesAsync(session, nodeId, globalSemaphore, nodeLocks, onNodeCompleted, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error in node completion continuation for {nodeId} in execution {session.ExecutionId}");
                    }
                }, TaskScheduler.Current);
            }
            finally
            {
                nodeLock.Release();
            }
        }

        private async Task ProcessDependentNodesAsync(WorkflowExecutionSession session, string completedNodeId,
            SemaphoreSlim globalSemaphore, ConcurrentDictionary<string, SemaphoreSlim> nodeLocks,
            Action onNodeCompleted, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Processing dependent nodes for completed node {CompletedNodeId} in execution {ExecutionId}",
                    completedNodeId, session.ExecutionId);

                // Find nodes that depend on the completed node
                var dependentNodeIds = session.Workflow.Edges
                    .Where(e => e.SourceNodeId == completedNodeId && !e.IsDisabled)
                    .Select(e => e.TargetNodeId)
                    .Distinct()
                    .ToList();

                _logger.LogInformation("Found {DependentNodeCount} dependent nodes for {CompletedNodeId}: [{DependentNodes}]",
                    dependentNodeIds.Count, completedNodeId, string.Join(", ", dependentNodeIds));

                if (!dependentNodeIds.Any())
                {
                    _logger.LogInformation("No dependent nodes found for {CompletedNodeId}", completedNodeId);
                    return;
                }

                // Process each dependent node
                var dependentTasks = dependentNodeIds.Select(async dependentNodeId =>
                {
                    _logger.LogInformation("Attempting to start dependent node {DependentNodeId} after {CompletedNodeId}",
                        dependentNodeId, completedNodeId);
                    await TryStartNodeExecutionAsync(session, dependentNodeId, globalSemaphore, nodeLocks, onNodeCompleted, cancellationToken);
                    return dependentNodeId; // Return node ID for logging
                });

                var completedNodeIds = await Task.WhenAll(dependentTasks);

                _logger.LogInformation("Dependent node processing completed for {CompletedNodeId}. Processed nodes: [{ProcessedNodes}]",
                    completedNodeId, string.Join(", ", completedNodeIds));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing dependent nodes for {CompletedNodeId} in execution {ExecutionId}",
                    completedNodeId, session.ExecutionId);
            }
        }

        private bool IsWorkflowComplete(WorkflowExecutionSession session)
        {
            var enabledNodes = session.Workflow.Nodes.Where(n => !n.IsDisabled).ToList();
            var totalEnabledNodes = enabledNodes.Count;
            var finishedNodes = session.CompletedNodes.Count + session.FailedNodes.Count;

            // Count nodes waiting for input - they're not finished yet
            var waitingNodes = session.Execution.NodeExecutions.Count(ne => ne.Status == NodeExecutionStatus.WaitingForInput);

            // Check if all enabled nodes are finished (completed, failed, or skipped)
            if (finishedNodes >= totalEnabledNodes)
            {
                return true;
            }

            // If there are nodes waiting for input, workflow is not complete
            if (waitingNodes > 0)
            {
                return false;
            }

            // Check if workflow should stop due to failed nodes and ContinueOnError = false
            if (session.FailedNodes.Any() && !session.Options.ContinueOnError)
            {
                return true;
            }

            return false;
        }

        private async Task<NodeExecution> ExecuteNodeWithTrackingAsync(WorkflowExecutionSession session, string nodeId, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Add node execution start log
                var node = session.Workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
                _logger.LogInformation($"Starting execution of node '{node?.Name ?? nodeId}'");

                await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info, $"Starting execution of node '{node?.Name ?? nodeId}'", nodeId: nodeId, metadata: new Dictionary<string, object>
                {
                    ["nodeType"] = node?.NodeType.ToString() ?? "Unknown",
                    ["programId"] = node?.ProgramId.ToString() ?? "Unknown"
                }, cancellationToken: cancellationToken);

                var nodeExecution = await ExecuteNodeInternalAsync(session.ExecutionId, nodeId, isExternalCall: false, cancellationToken);

                // Update session state based on execution result
                switch (nodeExecution.Status)
                {
                    case NodeExecutionStatus.Completed:
                        session.CompletedNodes.Add(nodeId);
                        _logger.LogInformation($"Node {nodeId} completed successfully in execution {session.ExecutionId}");

                        // Add completion log with output information
                        var outputFileCount = nodeExecution.OutputData?.ContainsKey("outputFiles") == true &&
                                            nodeExecution.OutputData["outputFiles"] is BsonArray outputFiles ? outputFiles.Count : 0;

                        await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info, $"Node '{node?.Name ?? nodeId}' completed successfully",
                            nodeId: nodeId, metadata: new Dictionary<string, object>
                            {
                                ["executionTime"] = nodeExecution.CompletedAt.HasValue
                                    ? nodeExecution.CompletedAt.Value.Subtract(nodeExecution.StartedAt).TotalSeconds
                                    : 0,
                                ["outputFileCount"] = outputFileCount,
                                ["status"] = "Completed"
                            }, cancellationToken: cancellationToken);
                        break;

                    case NodeExecutionStatus.Failed:
                        session.FailedNodes.Add(nodeId);
                        _logger.LogError($"Node {nodeId} failed in execution {session.ExecutionId}");

                        // Add failure log with error details
                        await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Error, $"Node '{node?.Name ?? nodeId}' execution failed",
                            nodeId: nodeId, metadata: new Dictionary<string, object>
                            {
                                ["status"] = "Failed",
                                ["errorMessage"] = nodeExecution.ErrorMessage ?? "Unknown error"
                            }, exception: nodeExecution.ErrorMessage, cancellationToken: cancellationToken);

                        if (!session.Options.ContinueOnError)
                        {
                            _logger.LogWarning($"Cancelling workflow execution {session.ExecutionId} due to failed node {nodeId}");
                            await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Warning, $"Workflow execution cancelled due to failed node '{node?.Name ?? nodeId}'",
                                nodeId: nodeId, cancellationToken: cancellationToken);
                            session.CancellationTokenSource.Cancel();
                        }
                        break;

                    case NodeExecutionStatus.Skipped:
                        // Treat skipped nodes as completed for dependency purposes
                        session.CompletedNodes.Add(nodeId);
                        _logger.LogInformation($"Node {nodeId} was skipped in execution {session.ExecutionId}");

                        await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info, $"Node '{node?.Name ?? nodeId}' was skipped",
                            nodeId: nodeId, metadata: new Dictionary<string, object>
                            {
                                ["status"] = "Skipped"
                            }, cancellationToken: cancellationToken);
                        break;

                    case NodeExecutionStatus.WaitingForInput:
                        // Don't mark as completed, but don't count as failed either
                        // The node is waiting for user input and will continue later
                        _logger.LogInformation($"Node {nodeId} is waiting for UI input in execution {session.ExecutionId}");

                        await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info, $"Node '{node?.Name ?? nodeId}' is waiting for user input",
                            nodeId: nodeId, metadata: new Dictionary<string, object>
                            {
                                ["status"] = "WaitingForInput"
                            }, cancellationToken: cancellationToken);
                        break;
                }

                // Update execution progress
                await UpdateExecutionProgressAsync(session, cancellationToken);

                // Return the node execution for tracking
                return session.Execution.NodeExecutions.First(ne => ne.NodeId == nodeId);
            }
            finally
            {
                semaphore.Release();
            }
        }


        private async Task WaitForAllRunningNodesToComplete(WorkflowExecutionSession session)
        {
            try
            {
                var runningTasks = session.RunningNodes.Values.ToArray();
                if (runningTasks.Any())
                {
                    _logger.LogInformation($"Waiting for {runningTasks.Length} running nodes to complete in execution {session.ExecutionId}");
                    await Task.WhenAll(runningTasks);
                    _logger.LogInformation($"All running nodes completed in execution {session.ExecutionId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error waiting for running nodes to complete in execution {session.ExecutionId}");
            }
        }

        private async Task<Task> ExecuteNodeWithSemaphoreAsync(WorkflowExecutionSession session, string nodeId, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var nodeExecution = await ExecuteNodeInternalAsync(session.ExecutionId, nodeId, isExternalCall: false, cancellationToken);

                    if (nodeExecution.Status == NodeExecutionStatus.Completed)
                    {
                        session.CompletedNodes.Add(nodeId);
                        await UpdateExecutionProgressAsync(session, cancellationToken);
                    }
                    else if (nodeExecution.Status == NodeExecutionStatus.Failed)
                    {
                        session.FailedNodes.Add(nodeId);

                        if (!session.Options.ContinueOnError)
                        {
                            session.CancellationTokenSource.Cancel();
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);
        }

        public async Task<NodeExecutionResponseDto> ExecuteNodeAsync(string executionId, string nodeId, CancellationToken cancellationToken = default)
        {
            return await ExecuteNodeInternalAsync(executionId, nodeId, isExternalCall: true, cancellationToken);
        }

        private async Task<NodeExecutionResponseDto> ExecuteNodeInternalAsync(string executionId, string nodeId, bool isExternalCall, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"[Instance {GetHashCode()}] Executing node {nodeId} in workflow execution {executionId} (External: {isExternalCall})");
            _logger.LogInformation($"[Instance {GetHashCode()}] Active sessions count: {_sessionManager.SessionCount}, Session keys: [{string.Join(", ", _sessionManager.SessionKeys)}]");

            try
            {
                if (!_sessionManager.TryGetSession(executionId, out var session))
                {
                    // Enhanced validation: Check database for execution status
                    var executionStatus = await GetExecutionStatusFromDatabaseAsync(executionId, cancellationToken);

                    // If execution is completed and this is not an external call, just return gracefully
                    if (!isExternalCall && executionStatus.Status == WorkflowExecutionStatus.Completed)
                    {
                        _logger.LogWarning($"Attempted to execute node {nodeId} on completed workflow {executionId}. Ignoring request to prevent race condition.");
                        return new NodeExecutionResponseDto
                        {
                            Status = NodeExecutionStatus.Skipped,
                            ErrorMessage = "Workflow already completed - node execution skipped to prevent race condition",
                            CompletedAt = DateTime.UtcNow
                        };
                    }

                    throw new InvalidOperationException(executionStatus.ErrorMessage);
                }

                // Check if this is being called externally while workflow is running internally
                if (isExternalCall)
                {
                    var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
                    if (execution?.Status == WorkflowExecutionStatus.Running)
                    {
                        throw new InvalidOperationException($"Cannot manually execute node {nodeId} - workflow execution {executionId} is currently running automatically. Wait for workflow completion or cancel the workflow first.");
                    }
                    else if (execution?.Status == WorkflowExecutionStatus.Completed)
                    {
                        throw new InvalidOperationException($"Cannot execute node {nodeId} - workflow execution has already completed at {execution.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC");
                    }
                    else if (execution?.Status == WorkflowExecutionStatus.Failed)
                    {
                        throw new InvalidOperationException($"Cannot execute node {nodeId} - workflow execution failed at {execution.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC");
                    }
                    else if (execution?.Status == WorkflowExecutionStatus.Cancelled)
                    {
                        throw new InvalidOperationException($"Cannot execute node {nodeId} - workflow execution was cancelled at {execution.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC");
                    }
                }

                var node = session.Workflow.Nodes.First(n => n.Id == nodeId);
                var nodeExecution = session.Execution.NodeExecutions.First(ne => ne.NodeId == nodeId);

                // Update node status
                nodeExecution.Status = NodeExecutionStatus.Running;
                nodeExecution.StartedAt = DateTime.UtcNow;
                await UpdateNodeExecutionAndSyncSessionAsync(executionId, nodeId, nodeExecution, cancellationToken);

                // Prepare input data
                var inputData = await PrepareNodeInputDataAsync(session, nodeId, cancellationToken);

                // Generate WorkflowInputs.py content for easy access to dependency outputs
                var workflowInputsHelper = WorkflowInputsGenerator.GenerateWorkflowInputsFromBsonDocument(inputData.Data);

                // Pass WorkflowInputs.py content via environment variable for PythonProjectRunner to write to project directory
                var enhancedEnvironment = new Dictionary<string, string>(node.ExecutionSettings.Environment);
                enhancedEnvironment["WORKFLOW_INPUTS_CONTENT"] = workflowInputsHelper;

                _logger.LogInformation($"Generated WorkflowInputs.py content for node {nodeId} with {inputData.Data.ElementCount} dependencies");

                // Create program execution request
                var programRequest = new ProjectExecutionRequest
                {
                    ProgramId = node.ProgramId.ToString(),
                    VersionId = node.VersionId?.ToString(),
                    UserId = session.Execution.ExecutedBy,
                    Parameters = inputData.Data,
                    Environment = enhancedEnvironment,
                    TimeoutMinutes = node.ExecutionSettings.TimeoutMinutes,
                    ResourceLimits = new ProjectResourceLimits
                    {
                        MaxCpuPercentage = node.ExecutionSettings.ResourceLimits.MaxCpuPercentage,
                        MaxMemoryMb = node.ExecutionSettings.ResourceLimits.MaxMemoryMb,
                        MaxDiskMb = node.ExecutionSettings.ResourceLimits.MaxDiskMb
                    }
                };

                // Check if this program requires UI interaction
                var program = await _unitOfWork.Programs.GetByIdAsync(node.ProgramId, cancellationToken);
                if (program != null && await IsUIInteractionRequiredAsync(program, cancellationToken))
                {
                    _logger.LogInformation($"Program {program.Name} requires UI interaction. Creating UI interaction session.");

                    // Get UI component ID for this program
                    var uiComponentId = await GetUIComponentIdForProgramAsync(program, cancellationToken);

                    // Create UI interaction request
                    var uiInteractionRequest = new UIInteractionRequest
                    {
                        Title = $"Input Required: {program.Name}",
                        Description = $"Program '{program.Name}' requires user interaction to continue.",
                        InteractionType = "UserInput",
                        InputSchema = CreateUISchemaForProgram(program),
                        Timeout = TimeSpan.FromMinutes(30),
                        Metadata = new Dictionary<string, object>
                        {
                            ["programId"] = program._ID.ToString(),
                            ["programName"] = program.Name,
                            ["nodeId"] = nodeId,
                            ["inputData"] = inputData.Data.ToJson(),
                            ["executionId"] = executionId,
                            ["workflowId"] = session.Workflow._ID.ToString()
                        }
                    };

                    // Create UI interaction using the service
                    _logger.LogInformation("Creating UI interaction session for ExecutionID {ExecutionId}, NodeID {NodeId}, WorkflowID {WorkflowId}", 
                        executionId, nodeId, session.Workflow._ID.ToString());
                    
                    var uiInteractionSession = await _uiInteractionService.CreateUIInteractionAsync(
                        session.Workflow._ID.ToString(),
                        executionId,
                        nodeId,
                        uiInteractionRequest,
                        uiComponentId,
                        cancellationToken);
                    
                    _logger.LogInformation("UI interaction session created successfully for ExecutionID {ExecutionId}, InteractionID {InteractionId}", 
                        executionId, uiInteractionSession.SessionId);

                    // Set node status to waiting for UI input
                    nodeExecution.Status = NodeExecutionStatus.WaitingForInput;
                    nodeExecution.CompletedAt = null; // Clear completion time since we're waiting
                    await UpdateNodeExecutionAndSyncSessionAsync(executionId, nodeId, nodeExecution, cancellationToken);

                    _logger.LogInformation($"UI interaction {uiInteractionSession.SessionId} created for node {nodeId}. Workflow execution will pause until user provides input.");

                    // Return early - execution will continue when UI interaction is completed
                    return new NodeExecutionResponseDto
                    {
                        Status = NodeExecutionStatus.WaitingForInput,
                        ErrorMessage = "Waiting for user input via UI interaction",
                        StartedAt = nodeExecution.StartedAt.Value,
                        ExecutionId = executionId,
                        InputData = inputData.Data.ToDictionary(x => x.Name, x => (object)x.Value),
                        NodeId = nodeId
                    };
                }

                // Execute program (only if no UI interaction required)
                var programResult = await _projectExecutionEngine.ExecuteProjectAsync(programRequest, cancellationToken);

                // Process results
                var outputData = await ProcessNodeOutputAsync(session, nodeId, programResult, cancellationToken);

                // Update node execution
                nodeExecution.CompletedAt = DateTime.UtcNow;
                nodeExecution.Duration = nodeExecution.CompletedAt - nodeExecution.StartedAt.Value;
                nodeExecution.ProgramExecutionId = programResult.ExecutionId;
                nodeExecution.InputData = inputData.Data;
                nodeExecution.OutputData = outputData.Data;

                if (programResult.Success)
                {
                    nodeExecution.Status = NodeExecutionStatus.Completed;
                    session.NodeOutputs[nodeId] = outputData;

                    _logger.LogInformation($"Node {nodeId} completed successfully in execution {executionId}");
                }
                else
                {
                    nodeExecution.Status = NodeExecutionStatus.Failed;
                    nodeExecution.Error = new NodeExecutionError
                    {
                        ErrorType = NodeErrorType.ExecutionError,
                        Message = programResult.ErrorMessage ?? "Program execution failed",
                        ExitCode = programResult.ExitCode,
                        Timestamp = DateTime.UtcNow,
                        CanRetry = nodeExecution.RetryCount < nodeExecution.MaxRetries
                    };

                    _logger.LogError($"Node {nodeId} failed in execution {executionId}: {programResult.ErrorMessage}");
                }

                // Update node execution in database and session state
                await UpdateNodeExecutionAndSyncSessionAsync(executionId, nodeId, nodeExecution, cancellationToken);

                return _mapper.Map<NodeExecutionResponseDto>(nodeExecution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing node {nodeId} in execution {executionId}");
                throw;
            }
        }

        private async Task<WorkflowDataContract> PrepareNodeInputDataAsync(WorkflowExecutionSession session, string nodeId, CancellationToken cancellationToken)
        {
            var node = session.Workflow.Nodes.First(n => n.Id == nodeId);
            var inputData = new BsonDocument();

            // NEW APPROACH: Get all dependency outputs by program name
            var dependencyNodeIds = GetDependencyNodeIds(session.Workflow, nodeId);

            _logger.LogInformation($"Node {nodeId} has {dependencyNodeIds.Count} dependencies: [{string.Join(", ", dependencyNodeIds)}]");

            foreach (var depNodeId in dependencyNodeIds)
            {
                if (session.NodeOutputs.TryGetValue(depNodeId, out var depOutput))
                {
                    // Get program name for this dependency node
                    var programName = await GetProgramNameForNode(session.Workflow, depNodeId);

                    // Add complete output under program name
                    inputData[programName] = depOutput.Data;

                    _logger.LogInformation($"Added dependency '{programName}' output for node {nodeId}");
                }
                else
                {
                    _logger.LogWarning($"Dependency node {depNodeId} output not found for node {nodeId}");
                }
            }

            // LEGACY SUPPORT: Keep existing static inputs and user inputs for backward compatibility
            // Add static inputs
            foreach (var staticInput in node.InputConfiguration.StaticInputs)
            {
                inputData[staticInput.Name] = staticInput.Value;
            }

            // Add user inputs
            foreach (var userInput in node.InputConfiguration.UserInputs)
            {
                var inputKey = $"{nodeId}.{userInput.Name}";
                if (session.Execution.ExecutionContext.UserInputs.ToString().Contains(inputKey))
                {
                    inputData[userInput.Name] = session.Execution.ExecutionContext.UserInputs[inputKey];
                }
                else if (userInput.DefaultValue != null)
                {
                    inputData[userInput.Name] = userInput.DefaultValue;
                }
            }

            // LEGACY SUPPORT: Keep existing input mappings for backward compatibility
            // Add inputs from other nodes (old way - for nodes that still use input mappings)
            foreach (var inputMapping in node.InputConfiguration.InputMappings)
            {
                if (session.NodeOutputs.TryGetValue(inputMapping.SourceNodeId, out var sourceOutput))
                {
                    var sourceValue = ExtractValueFromOutput(sourceOutput, inputMapping.SourceOutputName);

                    // Apply transformation if specified
                    if (!string.IsNullOrEmpty(inputMapping.Transformation))
                    {
                        sourceValue = await ApplyTransformationAsync(sourceValue, inputMapping.Transformation, cancellationToken);
                    }

                    inputData[inputMapping.InputName] = sourceValue;
                }
                else if (inputMapping.DefaultValue != null)
                {
                    inputData[inputMapping.InputName] = inputMapping.DefaultValue;
                }
                else if (!inputMapping.IsOptional)
                {
                    _logger.LogWarning($"Required input mapping '{inputMapping.InputName}' not available for node '{nodeId}' - but dependencies are available via program names");
                }
            }

            return new WorkflowDataContract
            {
                SourceNodeId = "workflow_engine",
                TargetNodeId = nodeId,
                Data = inputData,
                DataType = WorkflowDataType.JSON,
                Timestamp = DateTime.UtcNow
            };
        }

        private async Task<WorkflowDataContract> ProcessNodeOutputAsync(WorkflowExecutionSession session, string nodeId, ProjectExecutionResult programResult, CancellationToken cancellationToken)
        {
            var node = session.Workflow.Nodes.First(n => n.Id == nodeId);
            var outputData = new BsonDocument();

            // Add standard outputs
            outputData["stdout"] = programResult.Output;
            outputData["stderr"] = programResult.ErrorOutput;
            outputData["exitCode"] = programResult.ExitCode;
            outputData["success"] = programResult.Success;
            outputData["duration"] = programResult.Duration?.ToString();

            // Add output files
            if (programResult.OutputFiles.Any())
            {
                var outputFiles = new BsonArray();
                foreach (var file in programResult.OutputFiles)
                {
                    outputFiles.Add(new BsonDocument
                    {
                        ["fileName"] = Path.GetFileName(file),
                        ["path"] = file // Now absolute path
                    });
                }
                outputData["outputFiles"] = outputFiles;
            }

            // Process custom output mappings
            foreach (var outputMapping in node.OutputConfiguration.OutputMappings)
            {
                var sourceValue = ExtractValueFromProgramOutput(programResult, outputMapping.SourceField);

                // Apply transformation if specified
                if (!string.IsNullOrEmpty(outputMapping.Transformation))
                {
                    sourceValue = await ApplyTransformationAsync(sourceValue, outputMapping.Transformation, cancellationToken);
                }

                outputData[outputMapping.OutputName] = sourceValue;
            }

            return new WorkflowDataContract
            {
                SourceNodeId = nodeId,
                TargetNodeId = "workflow_engine",
                Data = outputData,
                DataType = WorkflowDataType.JSON,
                Timestamp = DateTime.UtcNow,
                Metadata = new DataContractMetadata
                {
                    Size = outputData.ToJson().Length,
                    ContentType = "application/json"
                }
            };
        }

        private BsonValue ExtractValueFromOutput(WorkflowDataContract output, string fieldName)
        {
            if (output.Data.Contains(fieldName))
            {
                return output.Data[fieldName];
            }
            return BsonNull.Value;
        }

        private BsonValue ExtractValueFromProgramOutput(ProjectExecutionResult result, string fieldName)
        {
            return fieldName switch
            {
                "stdout" => result.Output,
                "stderr" => result.ErrorOutput,
                "exitCode" => result.ExitCode,
                "success" => result.Success,
                "duration" => result.Duration?.ToString() ?? "",
                "outputFiles" => new BsonArray(result.OutputFiles.Select(f => new BsonString(f))),
                _ => BsonNull.Value
            };
        }

        private async Task<BsonValue> ApplyTransformationAsync(BsonValue input, string transformation, CancellationToken cancellationToken)
        {
            // Simple transformation implementation
            // In a real implementation, this would support JSONPath, JMESPath, etc.
            try
            {
                // For now, just return the input value
                // TODO: Implement proper transformation logic
                return input;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Transformation failed: {transformation}");
                return input;
            }
        }

        private async Task UpdateExecutionStatusAsync(string executionId, WorkflowExecutionStatus status, string currentPhase, CancellationToken cancellationToken)
        {
            await _unitOfWork.WorkflowExecutions.UpdateExecutionStatusAsync(ObjectId.Parse(executionId), status, cancellationToken);

            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            if (execution != null)
            {
                execution.Progress.CurrentPhase = currentPhase;
                await _unitOfWork.WorkflowExecutions.UpdateExecutionProgressAsync(ObjectId.Parse(executionId), execution.Progress, cancellationToken);
            }
        }

        private async Task UpdateExecutionProgressAsync(WorkflowExecutionSession session, CancellationToken cancellationToken)
        {
            var progress = session.Execution.Progress;
            progress.CompletedNodes = session.CompletedNodes.Count;
            progress.FailedNodes = session.FailedNodes.Count;
            progress.RunningNodes = session.RunningNodes.Count;
            progress.PercentComplete = (double)progress.CompletedNodes / progress.TotalNodes * 100;

            // Update phase to show execution progress
            progress.CurrentPhase = $"Executing nodes ({progress.CompletedNodes}/{progress.TotalNodes} completed)";

            await _unitOfWork.WorkflowExecutions.UpdateExecutionProgressAsync(ObjectId.Parse(session.ExecutionId), progress, cancellationToken);
        }

        private async Task FinalizeExecutionAsync(WorkflowExecutionSession session, CancellationToken cancellationToken)
        {
            var execution = session.Execution;

            // Update phase to indicate finalization is starting
            execution.Progress.CurrentPhase = "Completing workflow";
            await _unitOfWork.WorkflowExecutions.UpdateExecutionProgressAsync(ObjectId.Parse(session.ExecutionId), execution.Progress, cancellationToken);

            if (session.FailedNodes.Any() && !session.Options.ContinueOnError)
            {
                execution.Status = WorkflowExecutionStatus.Failed;
                execution.Error = new WorkflowExecutionError
                {
                    ErrorType = WorkflowErrorType.ExecutionError,
                    Message = $"Workflow failed due to {session.FailedNodes.Count} failed nodes",
                    Timestamp = DateTime.UtcNow
                };

                // Add failure log
                await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Error, $"Workflow execution failed due to {session.FailedNodes.Count} failed nodes",
                    metadata: new Dictionary<string, object>
                    {
                        ["failedNodes"] = session.FailedNodes.ToArray(),
                        ["completedNodes"] = session.CompletedNodes.Count,
                        ["totalNodes"] = session.Workflow.Nodes.Count
                    }, cancellationToken: cancellationToken);
            }
            else
            {
                execution.Status = WorkflowExecutionStatus.Completed;

                // Add success log
                await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info, $"Workflow execution completed successfully",
                    metadata: new Dictionary<string, object>
                    {
                        ["completedNodes"] = session.CompletedNodes.Count,
                        ["failedNodes"] = session.FailedNodes.Count,
                        ["totalNodes"] = session.Workflow.Nodes.Count,
                        ["totalOutputFiles"] = 0 // Will be updated below after collecting output files
                    }, cancellationToken: cancellationToken);

                // Collect final outputs
                var finalOutputs = new BsonDocument();
                foreach (var nodeOutput in session.NodeOutputs)
                {
                    finalOutputs[nodeOutput.Key] = nodeOutput.Value.Data;
                }

                // Collect output files from all nodes
                var allOutputFiles = new List<WorkflowOutputFile>();
                foreach (var nodeOutput in session.NodeOutputs)
                {
                    if (nodeOutput.Value.Data.Contains("outputFiles") &&
                        nodeOutput.Value.Data["outputFiles"] is BsonArray)
                    {
                        var outputFilesArray = nodeOutput.Value.Data["outputFiles"] as BsonArray;
                        if (outputFilesArray != null)
                        {
                            foreach (var fileDoc in outputFilesArray.OfType<BsonDocument>())
                            {
                                var fileName = fileDoc.GetValue("fileName", "").AsString;
                                var filePath = fileDoc.GetValue("path", "").AsString;

                                if (!string.IsNullOrEmpty(fileName) && !string.IsNullOrEmpty(filePath))
                                {
                                    allOutputFiles.Add(new WorkflowOutputFile
                                    {
                                        FileName = fileName,
                                        FilePath = filePath,
                                        NodeId = nodeOutput.Key,
                                        CreatedAt = DateTime.UtcNow,
                                        Size = fileDoc.GetValue("size", 0).ToInt64(),
                                        ContentType = fileDoc.GetValue("contentType", "application/octet-stream").AsString
                                    });
                                }
                            }
                        }
                    }
                }

                execution.Results = new WorkflowExecutionResults
                {
                    FinalOutputs = finalOutputs,
                    IntermediateResults = session.NodeOutputs.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Data),
                    OutputFiles = allOutputFiles,
                    Summary = $"Workflow completed with {session.CompletedNodes.Count} successful nodes and {session.FailedNodes.Count} failed nodes"
                };

                // Update success log with final file count
                await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Info, $"Workflow results collected: {allOutputFiles.Count} output files generated",
                    metadata: new Dictionary<string, object>
                    {
                        ["totalOutputFiles"] = allOutputFiles.Count,
                        ["outputFilesByNode"] = allOutputFiles.GroupBy(f => f.NodeId).ToDictionary(g => g.Key, g => g.Count())
                    }, cancellationToken: cancellationToken);
            }

            execution.CompletedAt = DateTime.UtcNow;

            // Update final phase based on execution status
            execution.Progress.CurrentPhase = execution.Status == WorkflowExecutionStatus.Completed ? "Completed" : "Failed";

            // Update specific fields to preserve logs and other data
            var executionId = ObjectId.Parse(session.ExecutionId);

            // Update status (this also sets CompletedAt if status is final)
            await _unitOfWork.WorkflowExecutions.UpdateExecutionStatusAsync(executionId, execution.Status, cancellationToken);

            // Update progress
            await _unitOfWork.WorkflowExecutions.UpdateExecutionProgressAsync(executionId, execution.Progress, cancellationToken);

            // Set error if exists
            if (execution.Error != null)
            {
                await _unitOfWork.WorkflowExecutions.SetExecutionErrorAsync(executionId, execution.Error, cancellationToken);
            }

            // Set results if exists
            if (execution.Results != null)
            {
                await _unitOfWork.WorkflowExecutions.SetExecutionResultsAsync(executionId, execution.Results, cancellationToken);
            }

            _logger.LogInformation($"Workflow execution {session.ExecutionId} finalized with status {execution.Status}");
        }

        private async Task AddExecutionLogAsync(string executionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel level, string message, string? nodeId = null, string source = "WorkflowEngine", Dictionary<string, object>? metadata = null, string? exception = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var log = new WorkflowExecutionLog
                {
                    Timestamp = DateTime.UtcNow,
                    Level = level,
                    Message = message,
                    NodeId = nodeId,
                    Source = source,
                    Metadata = metadata != null ? new BsonDocument(metadata) : new BsonDocument()
                };

                await _unitOfWork.WorkflowExecutions.AddExecutionLogAsync(ObjectId.Parse(executionId), log, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add execution log for execution {ExecutionId}: {Message}", executionId, message);
            }
        }

        private async Task HandleExecutionErrorAsync(WorkflowExecutionSession session, Exception ex, CancellationToken cancellationToken)
        {
            var execution = session.Execution;
            execution.Status = WorkflowExecutionStatus.Failed;
            execution.CompletedAt = DateTime.UtcNow;
            execution.Error = new WorkflowExecutionError
            {
                ErrorType = WorkflowErrorType.SystemError,
                Message = ex.Message,
                Timestamp = DateTime.UtcNow,
                CanRetry = true
            };

            // Add system error log
            await AddExecutionLogAsync(session.ExecutionId, TeiasMongoAPI.Core.Models.Collaboration.LogLevel.Critical, $"System error occurred during workflow execution: {ex.Message}",
                metadata: new Dictionary<string, object>
                {
                    ["errorType"] = "SystemError",
                    ["stackTrace"] = ex.StackTrace ?? "",
                    ["canRetry"] = true,
                    ["completedNodes"] = session.CompletedNodes.Count,
                    ["failedNodes"] = session.FailedNodes.Count
                }, exception: ex.ToString(), cancellationToken: cancellationToken);

            await _unitOfWork.WorkflowExecutions.UpdateAsync(ObjectId.Parse(session.ExecutionId), execution, cancellationToken);
        }

        public async Task<WorkflowExecutionResponseDto> ResumeWorkflowAsync(string executionId, CancellationToken cancellationToken = default)
        {
            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            if (execution == null)
            {
                throw new InvalidOperationException($"Execution {executionId} not found");
            }

            if (execution.Status != WorkflowExecutionStatus.Paused)
            {
                throw new InvalidOperationException($"Execution {executionId} is not paused");
            }

            await _unitOfWork.WorkflowExecutions.ResumeExecutionAsync(ObjectId.Parse(executionId), cancellationToken);
            return _mapper.Map<WorkflowExecutionResponseDto>(execution);
        }

        public async Task<bool> PauseWorkflowAsync(string executionId, CancellationToken cancellationToken = default)
        {
            if (_sessionManager.TryGetSession(executionId, out var session))
            {
                session.CancellationTokenSource.Cancel();
            }

            return await _unitOfWork.WorkflowExecutions.PauseExecutionAsync(ObjectId.Parse(executionId), cancellationToken);
        }

        public async Task<bool> CancelWorkflowAsync(string executionId, CancellationToken cancellationToken = default)
        {
            if (_sessionManager.TryGetSession(executionId, out var session))
            {
                session.CancellationTokenSource.Cancel();
                _sessionManager.RemoveSession(executionId);
            }

            return await _unitOfWork.WorkflowExecutions.CancelExecutionAsync(ObjectId.Parse(executionId), cancellationToken);
        }

        public async Task<WorkflowExecutionResponseDto> GetExecutionStatusAsync(string executionId, CancellationToken cancellationToken = default)
        {
            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            var dto = _mapper.Map<WorkflowExecutionResponseDto>(execution);

            var user = await _userService.GetByIdAsync(dto.ExecutedBy, cancellationToken);

            dto.ExecutedByUsername = user.FullName;

            return dto;
        }

        public async Task<List<WorkflowExecutionResponseDto>> GetActiveExecutionsAsync(CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.WorkflowExecutions.GetRunningExecutionsAsync(cancellationToken);
            return _mapper.Map<List<WorkflowExecutionResponseDto>>(executions.ToList());
        }

        public async Task<NodeExecutionResponseDto> RetryNodeAsync(string executionId, string nodeId, CancellationToken cancellationToken = default)
        {
            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            if (execution == null)
            {
                throw new InvalidOperationException($"Workflow execution {executionId} not found");
            }

            // Check if execution is in a state that allows node retry
            if (execution.Status == WorkflowExecutionStatus.Completed)
            {
                throw new InvalidOperationException($"Cannot retry node - workflow execution has already completed at {execution.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC");
            }
            if (execution.Status == WorkflowExecutionStatus.Cancelled)
            {
                throw new InvalidOperationException($"Cannot retry node - workflow execution was cancelled at {execution.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC");
            }

            var nodeExecution = execution.NodeExecutions.FirstOrDefault(ne => ne.NodeId == nodeId);
            if (nodeExecution == null)
            {
                throw new InvalidOperationException($"Node execution {nodeId} not found in workflow {executionId}");
            }

            if (nodeExecution.RetryCount >= nodeExecution.MaxRetries)
            {
                throw new InvalidOperationException($"Maximum retry attempts ({nodeExecution.MaxRetries}) exceeded for node {nodeId}");
            }

            // Only retry failed nodes
            if (nodeExecution.Status != NodeExecutionStatus.Failed)
            {
                throw new InvalidOperationException($"Cannot retry node {nodeId} - node is in {nodeExecution.Status} state. Only failed nodes can be retried");
            }

            nodeExecution.RetryCount++;
            nodeExecution.Status = NodeExecutionStatus.Retrying;
            await UpdateNodeExecutionAndSyncSessionAsync(executionId, nodeId, nodeExecution, cancellationToken);

            return await ExecuteNodeInternalAsync(executionId, nodeId, isExternalCall: false, cancellationToken);
        }

        public async Task<bool> SkipNodeAsync(string executionId, string nodeId, string reason, CancellationToken cancellationToken = default)
        {
            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            if (execution == null)
            {
                throw new InvalidOperationException($"Execution {executionId} not found");
            }

            var nodeExecution = execution.NodeExecutions.FirstOrDefault(ne => ne.NodeId == nodeId);
            if (nodeExecution == null)
            {
                throw new InvalidOperationException($"Node execution {nodeId} not found");
            }

            nodeExecution.Status = NodeExecutionStatus.Skipped;
            nodeExecution.WasSkipped = true;
            nodeExecution.SkipReason = reason;
            nodeExecution.CompletedAt = DateTime.UtcNow;

            await UpdateNodeExecutionAndSyncSessionAsync(executionId, nodeId, nodeExecution, cancellationToken);
            return true;
        }

        public async Task<WorkflowDataContractDto> GetNodeOutputAsync(string executionId, string nodeId, CancellationToken cancellationToken = default)
        {
            if (_sessionManager.TryGetSession(executionId, out var session))
            {
                if (session.NodeOutputs.TryGetValue(nodeId, out var output))
                {
                    return _mapper.Map<WorkflowDataContractDto>(output);
                }
            }

            // If not in active session, try to get from database
            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            if (execution?.Results?.IntermediateResults?.ContainsKey(nodeId) == true)
            {
                var contract = new WorkflowDataContract
                {
                    SourceNodeId = nodeId,
                    Data = execution.Results.IntermediateResults[nodeId],
                    DataType = WorkflowDataType.JSON,
                    Timestamp = DateTime.UtcNow
                };
                return _mapper.Map<WorkflowDataContractDto>(contract);
            }

            throw new InvalidOperationException($"Output not found for node {nodeId} in execution {executionId}");
        }

        public async Task<Dictionary<string, WorkflowDataContractDto>> GetAllNodeOutputsAsync(string executionId, CancellationToken cancellationToken = default)
        {
            if (_sessionManager.TryGetSession(executionId, out var session))
            {
                return session.NodeOutputs.ToDictionary(kvp => kvp.Key, kvp => _mapper.Map<WorkflowDataContractDto>(kvp.Value));
            }

            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            if (execution?.Results?.IntermediateResults != null)
            {
                return execution.Results.IntermediateResults.ToDictionary(
                    kvp => kvp.Key,
                    kvp => _mapper.Map<WorkflowDataContractDto>(new WorkflowDataContract
                    {
                        SourceNodeId = kvp.Key,
                        Data = kvp.Value,
                        DataType = WorkflowDataType.JSON,
                        Timestamp = DateTime.UtcNow
                    }));
            }

            return new Dictionary<string, WorkflowDataContractDto>();
        }

        public async Task<WorkflowExecutionStatisticsResponseDto> GetExecutionStatisticsAsync(string executionId, CancellationToken cancellationToken = default)
        {
            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            if (execution == null)
            {
                throw new InvalidOperationException($"Execution {executionId} not found");
            }

            var completedNodes = execution.NodeExecutions.Where(ne => ne.Status == NodeExecutionStatus.Completed).ToList();
            var stats = new WorkflowExecutionStatistics
            {
                TotalExecutionTime = execution.CompletedAt - execution.StartedAt ?? TimeSpan.Zero,
                TotalRetries = execution.NodeExecutions.Sum(ne => ne.RetryCount)
            };

            if (completedNodes.Any())
            {
                stats.AverageNodeExecutionTime = TimeSpan.FromMilliseconds(
                    completedNodes.Average(ne => ne.Duration?.TotalMilliseconds ?? 0));

                var slowestNode = completedNodes.OrderByDescending(ne => ne.Duration).FirstOrDefault();
                stats.SlowestNode = slowestNode?.NodeId;

                var fastestNode = completedNodes.OrderBy(ne => ne.Duration).FirstOrDefault();
                stats.FastestNode = fastestNode?.NodeId;
            }

            return _mapper.Map<WorkflowExecutionStatisticsResponseDto>(stats);
        }

        public async Task<List<WorkflowExecutionLogResponseDto>> GetExecutionLogsAsync(string executionId, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        {
            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            if (execution == null)
            {
                throw new InvalidOperationException($"Execution {executionId} not found");
            }

            var logs = execution.Logs.Skip(skip).Take(take).ToList();
            return _mapper.Map<List<WorkflowExecutionLogResponseDto>>(logs);
        }

        public async Task<bool> IsExecutionCompleteAsync(string executionId, CancellationToken cancellationToken = default)
        {
            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            if (execution == null)
            {
                return false;
            }

            return execution.Status == WorkflowExecutionStatus.Completed ||
                   execution.Status == WorkflowExecutionStatus.Failed ||
                   execution.Status == WorkflowExecutionStatus.Cancelled;
        }

        public async Task<bool> CleanupExecutionAsync(string executionId, CancellationToken cancellationToken = default)
        {
            // Remove from active sessions
            _sessionManager.RemoveSession(executionId);

            // Clean up temporary files and resources
            // This would involve cleaning up storage, temporary files, etc.

            return true;
        }


        private List<string> GetDependencyNodeIds(Workflow workflow, string nodeId)
        {
            // Get all edges that point to this node (incoming edges)
            var incomingEdges = workflow.Edges.Where(e => e.TargetNodeId == nodeId && !e.IsDisabled);

            // Return the source node IDs (dependencies)
            return incomingEdges.Select(e => e.SourceNodeId).Distinct().ToList();
        }

        private async Task<string> GetProgramNameForNode(Workflow workflow, string nodeId)
        {
            var node = workflow.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
            {
                return $"UnknownNode_{nodeId}";
            }

            try
            {
                // Get the program associated with this node
                var program = await _unitOfWork.Programs.GetByIdAsync(node.ProgramId, CancellationToken.None);
                if (program != null && !string.IsNullOrEmpty(program.Name))
                {
                    // Clean the program name to be a valid identifier
                    return CleanProgramName(program.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get program name for node {nodeId}");
            }

            // Fallback to node name or ID
            if (!string.IsNullOrEmpty(node.Name))
            {
                return CleanProgramName(node.Name);
            }

            return $"Node_{nodeId}";
        }

        private static string CleanProgramName(string programName)
        {
            // Convert program name to a valid identifier
            // Remove special characters and spaces, use PascalCase
            var cleaned = new StringBuilder();
            bool capitalizeNext = true;

            foreach (char c in programName)
            {
                if (char.IsLetterOrDigit(c))
                {
                    cleaned.Append(capitalizeNext ? char.ToUpper(c) : c);
                    capitalizeNext = false;
                }
                else if (c == ' ' || c == '_' || c == '-')
                {
                    capitalizeNext = true;
                }
            }

            var result = cleaned.ToString();

            // Ensure it starts with a letter
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = "Program" + result;
            }

            return string.IsNullOrEmpty(result) ? "UnknownProgram" : result;
        }

        private async Task<ExecutionStatusInfo> GetExecutionStatusFromDatabaseAsync(string executionId, CancellationToken cancellationToken)
        {
            try
            {
                var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);

                if (execution == null)
                {
                    return new ExecutionStatusInfo
                    {
                        Exists = false,
                        ErrorMessage = $"Workflow execution {executionId} not found"
                    };
                }

                return execution.Status switch
                {
                    WorkflowExecutionStatus.Completed => new ExecutionStatusInfo
                    {
                        Exists = true,
                        Status = execution.Status,
                        ErrorMessage = $"Cannot execute node {executionId} - workflow execution has already completed at {execution.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC"
                    },
                    WorkflowExecutionStatus.Failed => new ExecutionStatusInfo
                    {
                        Exists = true,
                        Status = execution.Status,
                        ErrorMessage = $"Cannot execute node {executionId} - workflow execution failed at {execution.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC"
                    },
                    WorkflowExecutionStatus.Cancelled => new ExecutionStatusInfo
                    {
                        Exists = true,
                        Status = execution.Status,
                        ErrorMessage = $"Cannot execute node {executionId} - workflow execution was cancelled at {execution.CompletedAt:yyyy-MM-dd HH:mm:ss} UTC"
                    },
                    WorkflowExecutionStatus.Paused => new ExecutionStatusInfo
                    {
                        Exists = true,
                        Status = execution.Status,
                        ErrorMessage = $"Cannot execute node {executionId} - workflow execution is paused. Resume the execution first"
                    },
                    WorkflowExecutionStatus.Running => new ExecutionStatusInfo
                    {
                        Exists = true,
                        Status = execution.Status,
                        ErrorMessage = $"Execution session {executionId} not found in active sessions, but execution is still marked as running. This may indicate a system restart or session cleanup"
                    },
                    _ => new ExecutionStatusInfo
                    {
                        Exists = true,
                        Status = execution.Status,
                        ErrorMessage = $"Cannot execute node {executionId} - workflow execution is in {execution.Status} state"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking execution status for {executionId}");
                return new ExecutionStatusInfo
                {
                    Exists = false,
                    ErrorMessage = $"Error checking execution status: {ex.Message}"
                };
            }
        }

        public async Task<VersionFileDetailDto> DownloadExecutionFileAsync(string executionId, string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
                if (execution == null)
                {
                    throw new InvalidOperationException($"Workflow execution {executionId} not found");
                }

                // Find the output file in the execution results
                var outputFile = execution.Results?.OutputFiles?.FirstOrDefault(f => f.FilePath == filePath);
                if (outputFile == null)
                {
                    throw new FileNotFoundException($"Output file {filePath} not found in execution {executionId}");
                }

                // Find the node that generated this file and get its program info
                var nodeExecution = execution.NodeExecutions?.FirstOrDefault(ne => ne.NodeId == outputFile.NodeId);
                if (nodeExecution == null)
                {
                    throw new InvalidOperationException($"Node execution for NodeId {outputFile.NodeId} not found");
                }

                var fileContent = await _fileStorageService.GetFileContentAsync(
                    nodeExecution.ProgramId.ToString(),
                    "latest",
                    filePath,
                    cancellationToken);

                return new VersionFileDetailDto
                {
                    Path = outputFile.FilePath,
                    Content = fileContent,
                    Size = outputFile.Size,
                    ContentType = outputFile.ContentType,
                    LastModified = outputFile.CreatedAt,
                    FileType = Path.GetExtension(outputFile.FileName).TrimStart('.'),
                    Hash = string.Empty // Hash calculation can be added if needed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading execution file {FilePath} for execution {ExecutionId}", filePath, executionId);
                throw;
            }
        }

        public async Task<BulkDownloadResult> DownloadAllExecutionFilesAsync(string executionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
                if (execution == null)
                {
                    throw new InvalidOperationException($"Workflow execution {executionId} not found");
                }

                if (execution.Results?.OutputFiles == null || !execution.Results.OutputFiles.Any())
                {
                    return new BulkDownloadResult
                    {
                        ZipContent = Array.Empty<byte>(),
                        FileName = $"workflow-execution-{executionId}-files.zip",
                        FileCount = 0,
                        TotalSize = 0
                    };
                }

                // Group files by their associated program to enable bulk download per program
                var filesByProgram = execution.Results.OutputFiles
                    .Where(f => !string.IsNullOrEmpty(f.NodeId))
                    .Join(execution.NodeExecutions ?? new List<NodeExecution>(),
                          file => file.NodeId,
                          node => node.NodeId,
                          (file, node) => new { File = file, ProgramId = node.ProgramId })
                    .GroupBy(x => x.ProgramId)
                    .ToList();

                if (!filesByProgram.Any())
                {
                    return new BulkDownloadResult
                    {
                        ZipContent = new byte[0],
                        FileCount = 0,
                        TotalSize = 0
                    };
                }

                // For now, use the first program's files (this may need refinement based on requirements)
                var firstProgramGroup = filesByProgram.First();
                var filePaths = firstProgramGroup.Select(x => x.File.FilePath).ToList();

                var result = await _fileStorageService.BulkDownloadFilesAsync(
                    firstProgramGroup.Key.ToString(),
                    "latest",
                    filePaths,
                    false,
                    "optimal",
                    cancellationToken);

                return new BulkDownloadResult
                {
                    ZipContent = result.ZipContent,
                    FileName = $"workflow-execution-{executionId}-files.zip",
                    FileCount = result.FileCount,
                    TotalSize = result.TotalSize,
                    IncludedFiles = result.IncludedFiles,
                    SkippedFiles = result.SkippedFiles,
                    Errors = result.Errors
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading all execution files for execution {ExecutionId}", executionId);
                throw;
            }
        }

        public async Task<BulkDownloadResult> BulkDownloadExecutionFilesAsync(string executionId, WorkflowExecutionFileBulkDownloadRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
                if (execution == null)
                {
                    throw new InvalidOperationException($"Workflow execution {executionId} not found");
                }

                if (execution.Results?.OutputFiles == null || !execution.Results.OutputFiles.Any())
                {
                    return new BulkDownloadResult
                    {
                        ZipContent = Array.Empty<byte>(),
                        FileName = $"workflow-execution-{executionId}-selected-files.zip",
                        FileCount = 0,
                        TotalSize = 0
                    };
                }

                // Filter files based on request criteria
                var filteredFiles = execution.Results.OutputFiles.AsEnumerable();

                // Filter by specific file paths if provided
                if (request.FilePaths.Any())
                {
                    filteredFiles = filteredFiles.Where(f => request.FilePaths.Contains(f.FilePath));
                }

                // Filter by node IDs if provided
                if (request.NodeIds != null && request.NodeIds.Any())
                {
                    filteredFiles = filteredFiles.Where(f => request.NodeIds.Contains(f.NodeId));
                }

                var filesToDownload = filteredFiles.ToList();

                if (!filesToDownload.Any())
                {
                    return new BulkDownloadResult
                    {
                        ZipContent = Array.Empty<byte>(),
                        FileName = $"workflow-execution-{executionId}-selected-files.zip",
                        FileCount = 0,
                        TotalSize = 0
                    };
                }

                // Group files by their associated program
                var filesByProgram = filesToDownload
                    .Where(f => !string.IsNullOrEmpty(f.NodeId))
                    .Join(execution.NodeExecutions ?? new List<NodeExecution>(),
                          file => file.NodeId,
                          node => node.NodeId,
                          (file, node) => new { File = file, ProgramId = node.ProgramId })
                    .GroupBy(x => x.ProgramId)
                    .ToList();

                if (!filesByProgram.Any())
                {
                    return new BulkDownloadResult
                    {
                        ZipContent = Array.Empty<byte>(),
                        FileName = $"workflow-execution-{executionId}-selected-files.zip",
                        FileCount = 0,
                        TotalSize = 0
                    };
                }

                // For now, use the first program's files (this may need refinement based on requirements)
                var firstProgramGroup = filesByProgram.First();
                var filePaths = firstProgramGroup.Select(x => x.File.FilePath).ToList();

                var result = await _fileStorageService.BulkDownloadFilesAsync(
                    firstProgramGroup.Key.ToString(),
                    "latest",
                    filePaths,
                    request.IncludeMetadata,
                    request.CompressionLevel ?? "optimal",
                    cancellationToken);

                return new BulkDownloadResult
                {
                    ZipContent = result.ZipContent,
                    FileName = $"workflow-execution-{executionId}-selected-files.zip",
                    FileCount = result.FileCount,
                    TotalSize = result.TotalSize,
                    IncludedFiles = result.IncludedFiles,
                    SkippedFiles = result.SkippedFiles,
                    Errors = result.Errors
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk downloading execution files for execution {ExecutionId}", executionId);
                throw;
            }
        }

        public async Task<NodeExecutionResponseDto> CompleteUIInteractionAsync(string executionId, string nodeId, string interactionId, Dictionary<string, object> outputData, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"Completing UI interaction {interactionId} for node {nodeId} in execution {executionId}");

            try
            {
                // Get the UI interaction
                var interaction = await _unitOfWork.UIInteractions.GetByIdAsync(ObjectId.Parse(interactionId), cancellationToken);
                if (interaction == null)
                {
                    throw new InvalidOperationException($"UI interaction {interactionId} not found");
                }

                // Update UI interaction status
                interaction.Status = UIInteractionStatus.Completed;
                interaction.OutputData = ConvertJsonElementsToBsonCompatible(outputData);
                interaction.CompletedAt = DateTime.UtcNow;

                _logger.LogDebug("Saving UI interaction {InteractionId} with converted output data", interactionId);
                await _unitOfWork.UIInteractions.UpdateAsync(ObjectId.Parse(interactionId), interaction, cancellationToken);

                // Convert data for event emission and wo_rkflow continuation
                var convertedOutputData = ConvertJsonElementsToBsonCompatible(outputData);

                // Emit status changed event
                await EmitUIInteractionStatusChangedAsync(
                    interaction.WorkflowExecutionId.ToString(),
                    executionId,
                    interactionId,
                    UIInteractionStatus.Completed,
                    convertedOutputData,
                    cancellationToken);

                // Continue node execution with the provided output data
                return await ContinueNodeExecutionAfterUIInteractionAsync(executionId, nodeId, convertedOutputData, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to complete UI interaction {interactionId} for node {nodeId}");
                throw;
            }
        }

        private async Task<NodeExecutionResponseDto> ContinueNodeExecutionAfterUIInteractionAsync(string executionId, string nodeId, Dictionary<string, object> uiOutputData, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Continuing node {NodeId} execution after UI interaction completion in execution {ExecutionId}. UI Output: {UIOutput}",
                nodeId, executionId, JsonSerializer.Serialize(ConvertBsonToJsonCompatible(uiOutputData)));

            if (!_sessionManager.TryGetSession(executionId, out var session))
            {
                throw new InvalidOperationException($"Execution session {executionId} not found");
            }

            var node = session.Workflow.Nodes.First(n => n.Id == nodeId);
            var nodeExecution = session.Execution.NodeExecutions.First(ne => ne.NodeId == nodeId);

            // Update node status back to running
            nodeExecution.Status = NodeExecutionStatus.Running;
            await UpdateNodeExecutionAndSyncSessionAsync(executionId, nodeId, nodeExecution, cancellationToken);

            // Prepare input data (including UI output)
            var inputData = await PrepareNodeInputDataAsync(session, nodeId, cancellationToken);

            // Add UI interaction output to input data
            foreach (var kvp in uiOutputData)
            {
                inputData.Data[$"{kvp.Key}"] = BsonValue.Create(kvp.Value);
                break;
            }

            // Generate WorkflowInputs.py content
            var workflowInputsHelper = WorkflowInputsGenerator.GenerateWorkflowInputsFromBsonDocument(inputData.Data);
            var enhancedEnvironment = new Dictionary<string, string>(node.ExecutionSettings.Environment);
            enhancedEnvironment["WORKFLOW_INPUTS_CONTENT"] = workflowInputsHelper;

            // Add UI output data as environment variables for easy access
            enhancedEnvironment["UI_OUTPUT_DATA"] = JsonSerializer.Serialize(ConvertBsonToJsonCompatible(uiOutputData));

            // Create program execution request
            var programRequest = new ProjectExecutionRequest
            {
                ProgramId = node.ProgramId.ToString(),
                VersionId = node.VersionId?.ToString(),
                UserId = session.Execution.ExecutedBy,
                Parameters = inputData.Data,
                Environment = enhancedEnvironment,
                TimeoutMinutes = node.ExecutionSettings.TimeoutMinutes,
                ResourceLimits = new ProjectResourceLimits
                {
                    MaxCpuPercentage = node.ExecutionSettings.ResourceLimits.MaxCpuPercentage,
                    MaxMemoryMb = node.ExecutionSettings.ResourceLimits.MaxMemoryMb,
                    MaxDiskMb = node.ExecutionSettings.ResourceLimits.MaxDiskMb
                }
            };

            // Execute program with UI output
            _logger.LogInformation("Executing program {ProgramId} for node {NodeId} after UI interaction with parameters: {Parameters}",
                node.ProgramId, nodeId, ConvertBsonDocumentToJson(inputData.Data));

            var programResult = await _projectExecutionEngine.ExecuteProjectAsync(programRequest, cancellationToken);

            _logger.LogInformation("Program execution completed for node {NodeId}. Success: {Success}, ExecutionId: {ExecutionId}, Message: {Message}",
                nodeId, programResult.Success, programResult.ExecutionId, programResult.ErrorMessage ?? "No errors");

            // Process results (same as normal execution)
            var outputData = await ProcessNodeOutputAsync(session, nodeId, programResult, cancellationToken);

            // Update node execution
            nodeExecution.CompletedAt = DateTime.UtcNow;
            nodeExecution.Duration = nodeExecution.CompletedAt - nodeExecution.StartedAt.Value;
            nodeExecution.ProgramExecutionId = programResult.ExecutionId;
            nodeExecution.InputData = inputData.Data;
            nodeExecution.OutputData = outputData.Data;

            if (programResult.Success)
            {
                nodeExecution.Status = NodeExecutionStatus.Completed;
                session.NodeOutputs[nodeId] = outputData;
                _logger.LogInformation($"Node {nodeId} completed successfully after UI interaction in execution {executionId}");
            }
            else
            {
                nodeExecution.Status = NodeExecutionStatus.Failed;
                nodeExecution.Error = new NodeExecutionError
                {
                    ErrorType = NodeErrorType.ExecutionError,
                    Message = programResult.ErrorMessage ?? "Program execution failed after UI interaction",
                    ExitCode = programResult.ExitCode,
                    Timestamp = DateTime.UtcNow,
                    CanRetry = nodeExecution.RetryCount < nodeExecution.MaxRetries
                };
                _logger.LogError($"Node {nodeId} failed after UI interaction in execution {executionId}: {programResult.ErrorMessage}");
            }

            // Update node execution in database and session state
            await UpdateNodeExecutionAndSyncSessionAsync(executionId, nodeId, nodeExecution, cancellationToken);

            // Continue workflow execution by processing dependent nodes
            // This ensures the workflow continues after UI interaction completion
            if (nodeExecution.Status == NodeExecutionStatus.Completed)
            {
                _logger.LogInformation($"Triggering workflow continuation after UI interaction completion for node {nodeId} in execution {executionId}");

                // Update session state to mark this node as completed
                if (!session.CompletedNodes.Contains(nodeId))
                {
                    session.CompletedNodes.Add(nodeId);
                }

                // Use Task.Run to continue workflow execution in the background with proper DI scope
                // This prevents blocking the UI response while allowing the workflow to continue
                _ = Task.Run(async () =>
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<WorkflowExecutionEngine>>();

                    try
                    {
                        scopedLogger.LogInformation("Starting background workflow continuation for node {NodeId} in execution {ExecutionId} with independent DI scope",
                            nodeId, executionId);

                        // Get ALL scoped services - don't pass any objects from the old scope
                        var scopedWorkflowEngine = scope.ServiceProvider.GetRequiredService<IWorkflowExecutionEngine>();
                        var scopedSessionManager = scope.ServiceProvider.GetRequiredService<IWorkflowSessionManager>();
                        
                        scopedLogger.LogInformation("Background task started for ExecutionID {ExecutionId} with scoped services resolved", executionId);

                        // Cast to the concrete type to access internal continuation methods
                        var concreteEngine = (WorkflowExecutionEngine)scopedWorkflowEngine;
                        
                        // Continue workflow execution using ONLY scoped services and internal methods
                        await concreteEngine.ContinueWorkflowExecutionInBackgroundInternal(
                            scopedSessionManager,
                            scopedLogger,
                            executionId,
                            nodeId,
                            cancellationToken);

                        scopedLogger.LogInformation("Background workflow continuation completed successfully for node {NodeId} in execution {ExecutionId}",
                            nodeId, executionId);
                    }
                    catch (Exception ex)
                    {
                        scopedLogger.LogError(ex, "Error continuing workflow after UI interaction for node {NodeId} in execution {ExecutionId}",
                            nodeId, executionId);
                    }
                }, cancellationToken);
            }

            return _mapper.Map<NodeExecutionResponseDto>(nodeExecution);
        }

        /// <summary>
        /// Continues workflow execution in the background using internal methods and scoped services
        /// </summary>
        /// <param name="scopedSessionManager">Scoped session manager instance</param>
        /// <param name="scopedLogger">Scoped logger instance</param>
        /// <param name="executionId">Execution ID</param>
        /// <param name="completedNodeId">ID of the completed node</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private async Task ContinueWorkflowExecutionInBackgroundInternal(
            IWorkflowSessionManager scopedSessionManager,
            ILogger<WorkflowExecutionEngine> scopedLogger,
            string executionId,
            string completedNodeId,
            CancellationToken cancellationToken)
        {
            // Get fresh session from scoped session manager (NOT the old one)
            if (!scopedSessionManager.TryGetSession(executionId, out var session))
            {
                throw new InvalidOperationException($"Execution session {executionId} not found during background continuation");
            }

            scopedLogger.LogInformation("Retrieved session {ExecutionId} in background continuation. Session has {CompletedNodes} completed nodes and {RunningNodes} running nodes",
                executionId, session.CompletedNodes.Count, session.RunningNodes.Count);

            // Create semaphore and locks for continuation (same as original ProcessDependentNodesAsync)
            using var globalSemaphore = new SemaphoreSlim(session.Options.MaxConcurrentNodes);
            var nodeLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
            
            // Use the original ProcessDependentNodesAsync method which properly handles automatic continuation
            // This avoids the "manual execution" check that was causing the InvalidOperationException
            scopedLogger.LogInformation("Starting ProcessDependentNodesAsync for node {CompletedNodeId} using internal workflow methods", 
                completedNodeId);
            
            await ProcessDependentNodesAsync(session, completedNodeId, globalSemaphore, nodeLocks, 
                () => { /* Empty completion callback */ }, cancellationToken);

            // Wait for all running nodes to complete before disposing scope
            // This prevents ObjectDisposedException during node completion and AutoMapper operations
            scopedLogger.LogInformation("Waiting for all running nodes to complete before disposing background scope. Running nodes: {RunningNodeCount}", 
                session.RunningNodes.Count);
                
            if (session.RunningNodes.Count > 0)
            {
                try
                {
                    var runningTasks = session.RunningNodes.Values.ToArray();
                    await Task.WhenAll(runningTasks);
                    scopedLogger.LogInformation("All running nodes completed successfully");

                    // Check if workflow is complete and finalize it
                    if (IsWorkflowComplete(session))
                    {
                        scopedLogger.LogInformation("Workflow execution completed, finalizing workflow {ExecutionId}", executionId);
                        await FinalizeExecutionAsync(session, cancellationToken);
                        await CheckAndCleanupCompletedWorkflowAsync(session, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    scopedLogger.LogError(ex, "Error waiting for running nodes to complete");
                }
            }

            scopedLogger.LogInformation("Background workflow continuation completed for node {CompletedNodeId}", completedNodeId);
        }

        private async Task CheckAndCleanupCompletedWorkflowAsync(WorkflowExecutionSession session, CancellationToken cancellationToken)
        {
            try
            {
                // Check if workflow is complete (no running nodes, no waiting nodes)
                var runningNodes = session.RunningNodes.Count;
                var waitingNodes = session.Execution.NodeExecutions.Count(ne => ne.Status == NodeExecutionStatus.WaitingForInput);

                if (runningNodes == 0 && waitingNodes == 0)
                {
                    var isComplete = IsWorkflowComplete(session);
                    if (isComplete)
                    {
                        _logger.LogInformation("Workflow {ExecutionId} completed after UI interaction, cleaning up session", session.ExecutionId);

                        // Update workflow execution status
                        session.Execution.Status = WorkflowExecutionStatus.Completed;
                        session.Execution.CompletedAt = DateTime.UtcNow;

                        // Save final execution state
                        await _unitOfWork.WorkflowExecutions.UpdateAsync(ObjectId.Parse(session.ExecutionId), session.Execution, cancellationToken);

                        // Remove session as workflow is complete
                        _sessionManager.RemoveSession(session.ExecutionId);
                        _logger.LogInformation("Session {ExecutionId} cleaned up after workflow completion", session.ExecutionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking and cleaning up completed workflow for session {ExecutionId}", session.ExecutionId);
            }
        }

        private async Task<bool> IsUIInteractionRequiredAsync(Program program, CancellationToken cancellationToken)
        {
            // Check if program has actual UI components that require user interaction
            // This is more reliable than just checking the UiType field

            try
            {
                // First, check if UiType explicitly indicates no UI interaction
                if (!string.IsNullOrEmpty(program.UiType))
                {
                    var uiType = program.UiType.ToLower().Trim();
                    var nonInteractiveUiTypes = new[] { "console", "none", "cli", "batch", "service" };

                    if (nonInteractiveUiTypes.Contains(uiType))
                        return false;
                }

                // Check if program actually has UI components
                var uiComponentId = await GetUIComponentIdForProgramAsync(program, cancellationToken);

                // If no UI components found, no UI interaction is required
                if (string.IsNullOrEmpty(uiComponentId))
                {
                    _logger.LogDebug("Program {ProgramId} has no UI components, no UI interaction required", program._ID);
                    return false;
                }

                _logger.LogDebug("Program {ProgramId} has UI components, UI interaction required", program._ID);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking UI interaction requirement for program {ProgramId}, defaulting to no interaction required", program._ID);
                return false;
            }
        }

        private async Task<string?> GetUIComponentIdForProgramAsync(Program program, CancellationToken cancellationToken)
        {
            try
            {
                // Get program version (assuming current version if not specified)
                var version = await _unitOfWork.Versions.GetLatestVersionForProgramAsync(program._ID, cancellationToken);
                if (version == null)
                {
                    _logger.LogWarning("No version found for program {ProgramId}", program._ID);
                    return null;
                }

                // Get UI components for this program version
                var components = await _unitOfWork.UiComponents.GetByProgramVersionAsync(program._ID, version._ID, cancellationToken);
                if (components == null || !components.Any())
                {
                    _logger.LogWarning("No UI components found for program {ProgramId}, version {VersionId}", program._ID, version._ID);
                    return null;
                }

                // Return the first active component, or just the first one
                var activeComponent = components.FirstOrDefault(c => c.Status == "active") ?? components.First();
                return activeComponent._ID.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get UI component for program {ProgramId}", program._ID);
                return null;
            }
        }

        private static Dictionary<string, object> CreateUISchemaForProgram(Program program)
        {
            // Create a JSON schema for the UI interaction based on program type
            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["title"] = $"Input for {program.Name}",
                ["description"] = program.Description ?? $"Please provide input for {program.Name}",
                ["properties"] = new Dictionary<string, object>()
            };

            var properties = (Dictionary<string, object>)schema["properties"];

            // Add different input fields based on program type/configuration
            switch (program.UiType?.ToLower())
            {
                case "web":
                case "webapp":
                    properties["userInput"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["title"] = "User Input",
                        ["description"] = "Input data for the web application"
                    };
                    properties["parameters"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["title"] = "Additional Parameters",
                        ["description"] = "Additional configuration parameters"
                    };
                    break;

                case "desktop":
                    properties["configData"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["title"] = "Configuration Data",
                        ["description"] = "Configuration data for the desktop application"
                    };
                    break;

                case "custom":
                    // Try to extract schema from program's UiConfiguration if available
                    if (program.UiConfiguration != null)
                    {
                        try
                        {
                            // If UiConfiguration contains schema information, use it
                            var uiConfig = JsonSerializer.Serialize(program.UiConfiguration);
                            var configDict = JsonSerializer.Deserialize<Dictionary<string, object>>(uiConfig);

                            if (configDict?.ContainsKey("inputSchema") == true)
                            {
                                return (Dictionary<string, object>)configDict["inputSchema"];
                            }
                        }
                        catch (Exception)
                        {
                            // Fall through to default
                        }
                    }

                    properties["customInput"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["title"] = "Custom Input",
                        ["description"] = "Input for custom application"
                    };
                    break;

                default:
                    properties["userInput"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["title"] = "User Input",
                        ["description"] = $"Input required for {program.Name}"
                    };
                    break;
            }

            // Add required fields
            schema["required"] = properties.Keys.ToArray();

            // Add UI configuration for better frontend rendering
            schema["uiSchema"] = new Dictionary<string, object>
            {
                ["submitLabel"] = "Submit",
                ["cancelLabel"] = "Cancel",
                ["allowSkip"] = false
            };

            return schema;
        }

        /// <summary>
        /// Converts JsonElement objects to BSON-compatible types for MongoDB serialization
        /// </summary>
        /// <param name="data">Dictionary containing potentially JsonElement values</param>
        /// <returns>Dictionary with BSON-compatible values</returns>
        private Dictionary<string, object> ConvertJsonElementsToBsonCompatible(Dictionary<string, object> data)
        {
            if (data == null) return new Dictionary<string, object>();

            var convertedData = new Dictionary<string, object>();

            foreach (var kvp in data)
            {
                convertedData[kvp.Key] = ConvertValueToBsonCompatible(kvp.Value);
            }

            return convertedData;
        }

        /// <summary>
        /// Converts a single value to BSON-compatible type
        /// </summary>
        /// <param name="value">Value to convert</param>
        /// <returns>BSON-compatible value</returns>
        private object ConvertValueToBsonCompatible(object value)
        {
            if (value == null) return null;

            // Handle JsonElement specifically
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                return ConvertJsonElementToBsonCompatible(jsonElement);
            }

            // Handle Dictionary<string, object> recursively
            if (value is Dictionary<string, object> dict)
            {
                return ConvertJsonElementsToBsonCompatible(dict);
            }

            // Handle arrays/lists
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(ConvertValueToBsonCompatible(item));
                }
                return list;
            }

            // Return primitive types as-is (string, int, bool, etc.)
            return value;
        }

        /// <summary>
        /// Converts JsonElement to appropriate .NET type for BSON serialization
        /// </summary>
        /// <param name="jsonElement">JsonElement to convert</param>
        /// <returns>BSON-compatible value</returns>
        private object ConvertJsonElementToBsonCompatible(System.Text.Json.JsonElement jsonElement)
        {
            switch (jsonElement.ValueKind)
            {
                case System.Text.Json.JsonValueKind.String:
                    return jsonElement.GetString();

                case System.Text.Json.JsonValueKind.Number:
                    if (jsonElement.TryGetInt32(out int intValue))
                        return intValue;
                    if (jsonElement.TryGetInt64(out long longValue))
                        return longValue;
                    if (jsonElement.TryGetDouble(out double doubleValue))
                        return doubleValue;
                    return jsonElement.GetDecimal();

                case System.Text.Json.JsonValueKind.True:
                    return true;

                case System.Text.Json.JsonValueKind.False:
                    return false;

                case System.Text.Json.JsonValueKind.Null:
                    return null;

                case System.Text.Json.JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var property in jsonElement.EnumerateObject())
                    {
                        dict[property.Name] = ConvertJsonElementToBsonCompatible(property.Value);
                    }
                    return dict;

                case System.Text.Json.JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in jsonElement.EnumerateArray())
                    {
                        list.Add(ConvertJsonElementToBsonCompatible(item));
                    }
                    return list;

                default:
                    return jsonElement.ToString();
            }
        }

        /// <summary>
        /// Converts BSON objects to JSON-compatible types for System.Text.Json serialization
        /// </summary>
        /// <param name="data">Dictionary potentially containing BSON objects</param>
        /// <returns>Dictionary with JSON-compatible values</returns>
        private Dictionary<string, object> ConvertBsonToJsonCompatible(Dictionary<string, object> data)
        {
            if (data == null) return new Dictionary<string, object>();

            var convertedData = new Dictionary<string, object>();

            foreach (var kvp in data)
            {
                convertedData[kvp.Key] = ConvertBsonValueToJsonCompatible(kvp.Value);
            }

            return convertedData;
        }

        /// <summary>
        /// Converts a BSON document to JSON string safely
        /// </summary>
        /// <param name="bsonDoc">BSON document</param>
        /// <returns>JSON string representation</returns>
        private string ConvertBsonDocumentToJson(MongoDB.Bson.BsonDocument bsonDoc)
        {
            if (bsonDoc == null) return "{}";

            try
            {
                // Use MongoDB's native JSON conversion which handles BSON types properly
                return bsonDoc.ToJson();
            }
            catch (Exception)
            {
                // Fallback to manual conversion
                var dict = new Dictionary<string, object>();
                foreach (var element in bsonDoc)
                {
                    dict[element.Name] = ConvertBsonValueToJsonCompatible(element.Value);
                }
                return JsonSerializer.Serialize(dict);
            }
        }

        /// <summary>
        /// Converts a single BSON value to JSON-compatible type
        /// </summary>
        /// <param name="value">BSON value to convert</param>
        /// <returns>JSON-compatible value</returns>
        private object ConvertBsonValueToJsonCompatible(object value)
        {
            if (value == null) return null;

            // Handle BSON types
            if (value is MongoDB.Bson.BsonValue bsonValue)
            {
                switch (bsonValue.BsonType)
                {
                    case MongoDB.Bson.BsonType.String:
                        return bsonValue.AsString;
                    case MongoDB.Bson.BsonType.Int32:
                        return bsonValue.AsInt32;
                    case MongoDB.Bson.BsonType.Int64:
                        return bsonValue.AsInt64;
                    case MongoDB.Bson.BsonType.Double:
                        return bsonValue.AsDouble;
                    case MongoDB.Bson.BsonType.Decimal128:
                        return bsonValue.AsDecimal;
                    case MongoDB.Bson.BsonType.Boolean:
                        return bsonValue.AsBoolean;
                    case MongoDB.Bson.BsonType.DateTime:
                        return bsonValue.ToUniversalTime();
                    case MongoDB.Bson.BsonType.Null:
                        return null;
                    case MongoDB.Bson.BsonType.Array:
                        return bsonValue.AsBsonArray.Select(ConvertBsonValueToJsonCompatible).ToList();
                    case MongoDB.Bson.BsonType.Document:
                        var dict = new Dictionary<string, object>();
                        foreach (var element in bsonValue.AsBsonDocument)
                        {
                            dict[element.Name] = ConvertBsonValueToJsonCompatible(element.Value);
                        }
                        return dict;
                    default:
                        return bsonValue.ToString();
                }
            }

            // Handle Dictionary<string, object> recursively
            if (value is Dictionary<string, object> dictValue)
            {
                return ConvertBsonToJsonCompatible(dictValue);
            }

            // Handle arrays/lists
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(ConvertBsonValueToJsonCompatible(item));
                }
                return list;
            }

            // Return primitive types as-is
            return value;
        }
    }

    public class WorkflowExecutionSession
    {
        public required string ExecutionId { get; set; }
        public required Workflow Workflow { get; set; }
        public required WorkflowExecution Execution { get; set; }
        public required WorkflowExecutionOptions Options { get; set; }
        public required CancellationTokenSource CancellationTokenSource { get; set; }
        
        // Thread-safe collections - ConcurrentDictionary provides its own optimized thread-safety
        public ConcurrentDictionary<string, WorkflowDataContract> NodeOutputs { get; set; } = new();
        public ConcurrentDictionary<string, Task<NodeExecution>> RunningNodes { get; set; } = new();
        
        // Use ConcurrentDictionary as thread-safe sets with dummy values
        private readonly ConcurrentDictionary<string, byte> _completedNodes = new();
        private readonly ConcurrentDictionary<string, byte> _failedNodes = new();
        
        // Thread-safe properties using ConcurrentDictionary
        public HashSet<string> CompletedNodes 
        { 
            get => _completedNodes.Keys.ToHashSet(); 
            set 
            { 
                _completedNodes.Clear();
                foreach (var nodeId in value ?? new HashSet<string>())
                    _completedNodes.TryAdd(nodeId, 0);
            } 
        }
        
        public HashSet<string> FailedNodes 
        { 
            get => _failedNodes.Keys.ToHashSet(); 
            set 
            { 
                _failedNodes.Clear();
                foreach (var nodeId in value ?? new HashSet<string>())
                    _failedNodes.TryAdd(nodeId, 0);
            } 
        }

        // Thread-safe methods for node state management
        public void MarkNodeCompleted(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            
            _completedNodes.TryAdd(nodeId, 0);
            _failedNodes.TryRemove(nodeId, out _);
        }
        
        public void MarkNodeFailed(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
            
            _failedNodes.TryAdd(nodeId, 0);
            _completedNodes.TryRemove(nodeId, out _);
        }
        
        public bool IsNodeCompleted(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && _completedNodes.ContainsKey(nodeId);
        }
        
        public bool IsNodeFailed(string nodeId)
        {
            return !string.IsNullOrEmpty(nodeId) && _failedNodes.ContainsKey(nodeId);
        }
    }

    public class ExecutionStatusInfo
    {
        public bool Exists { get; set; }
        public WorkflowExecutionStatus? Status { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}