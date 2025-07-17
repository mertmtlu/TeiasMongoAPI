using AutoMapper;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using System.Collections.Concurrent;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Interfaces.Execution;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class WorkflowExecutionEngine : BaseService, IWorkflowExecutionEngine
    {
        private readonly IWorkflowValidationService _validationService;
        private readonly IProjectExecutionEngine _projectExecutionEngine;
        private readonly IFileStorageService _fileStorageService;
        private readonly ConcurrentDictionary<string, WorkflowExecutionSession> _activeSessions = new();
        private readonly SemaphoreSlim _executionSemaphore = new(10, 10); // Limit concurrent workflows

        public WorkflowExecutionEngine(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<WorkflowExecutionEngine> logger,
            IWorkflowValidationService validationService,
            IProjectExecutionEngine projectExecutionEngine,
            IFileStorageService fileStorageService)
            : base(unitOfWork, mapper, logger)
        {
            _validationService = validationService;
            _projectExecutionEngine = projectExecutionEngine;
            _fileStorageService = fileStorageService;
        }

        public async Task<WorkflowExecution> ExecuteWorkflowAsync(WorkflowExecutionRequest request, CancellationToken cancellationToken = default)
        {
            var executionId = Guid.NewGuid().ToString();
            _logger.LogInformation($"Starting workflow execution {executionId} for workflow {request.WorkflowId} by user {request.UserId}");

            try
            {
                // Get workflow
                var workflow = await _unitOfWork.Workflows.GetByIdAsync(ObjectId.Parse(request.WorkflowId), cancellationToken);
                if (workflow == null)
                {
                    throw new InvalidOperationException($"Workflow {request.WorkflowId} not found");
                }

                // Validate workflow
                var validationResult = await _validationService.ValidateWorkflowAsync(workflow, cancellationToken);
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException($"Workflow validation failed: {string.Join(", ", validationResult.Errors.Select(e => e.Message))}");
                }

                // Validate permissions
                var permissionResult = await _validationService.ValidateWorkflowPermissionsAsync(workflow, request.UserId, cancellationToken);
                if (!permissionResult.IsValid)
                {
                    throw new UnauthorizedAccessException($"User {request.UserId} does not have permission to execute workflow {workflow.Name}");
                }

                // Validate execution context
                var executionValidationResult = await _validationService.ValidateWorkflowExecutionAsync(workflow, request.ExecutionContext, cancellationToken);
                if (!executionValidationResult.IsValid)
                {
                    throw new InvalidOperationException($"Execution validation failed: {string.Join(", ", executionValidationResult.Errors.Select(e => e.Message))}");
                }

                // Create workflow execution record
                var execution = new WorkflowExecution
                {
                    _ID = ObjectId.Parse(executionId),
                    WorkflowId = workflow._ID,
                    WorkflowVersion = workflow.Version,
                    ExecutionName = request.ExecutionName,
                    ExecutedBy = request.UserId,
                    StartedAt = DateTime.UtcNow,
                    Status = WorkflowExecutionStatus.Running,
                    ExecutionContext = request.ExecutionContext,
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
                    ExecutionId = executionId,
                    Workflow = workflow,
                    Execution = execution,
                    Options = request.Options,
                    NodeOutputs = new ConcurrentDictionary<string, WorkflowDataContract>(),
                    RunningNodes = new ConcurrentDictionary<string, Task<NodeExecution>>(),
                    CompletedNodes = new HashSet<string>(),
                    FailedNodes = new HashSet<string>(),
                    CancellationTokenSource = new CancellationTokenSource()
                };

                _activeSessions[executionId] = session;

                // Start execution in background
                _ = Task.Run(async () => await ExecuteWorkflowInternalAsync(session, cancellationToken), cancellationToken);

                _logger.LogInformation($"Workflow execution {executionId} started successfully");
                return execution;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to start workflow execution {executionId}");
                throw;
            }
        }

        private async Task ExecuteWorkflowInternalAsync(WorkflowExecutionSession session, CancellationToken cancellationToken)
        {
            var combinedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, session.CancellationTokenSource.Token).Token;

            try
            {
                await _executionSemaphore.WaitAsync(combinedCancellationToken);

                // Update status
                await UpdateExecutionStatusAsync(session.ExecutionId, WorkflowExecutionStatus.Running, "Analyzing dependencies", cancellationToken);

                // Get execution order
                var executionOrder = await _validationService.GetTopologicalOrderAsync(session.Workflow, cancellationToken);
                _logger.LogInformation($"Execution order for workflow {session.ExecutionId}: {string.Join(" -> ", executionOrder)}");

                // Execute nodes in dependency order
                var nodeSemaphore = new SemaphoreSlim(session.Options.MaxConcurrentNodes, session.Options.MaxConcurrentNodes);
                var executionTasks = new List<Task>();

                foreach (var nodeId in executionOrder)
                {
                    if (combinedCancellationToken.IsCancellationRequested)
                        break;

                    var node = session.Workflow.Nodes.First(n => n.Id == nodeId);
                    if (node.IsDisabled)
                    {
                        await SkipNodeAsync(session.ExecutionId, nodeId, "Node is disabled", cancellationToken);
                        continue;
                    }

                    // Check if dependencies are satisfied
                    if (await AreDependenciesSatisfiedAsync(session, nodeId, cancellationToken))
                    {
                        var task = ExecuteNodeWithSemaphoreAsync(session, nodeId, nodeSemaphore, combinedCancellationToken);
                        executionTasks.Add(task);
                    }
                    else
                    {
                        _logger.LogWarning($"Dependencies not satisfied for node {nodeId} in execution {session.ExecutionId}");
                    }
                }

                // Wait for all nodes to complete
                await Task.WhenAll(executionTasks);

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
                _executionSemaphore.Release();
                _activeSessions.TryRemove(session.ExecutionId, out _);
            }
        }

        private async Task<bool> AreDependenciesSatisfiedAsync(WorkflowExecutionSession session, string nodeId, CancellationToken cancellationToken)
        {
            var incomingEdges = session.Workflow.Edges.Where(e => e.TargetNodeId == nodeId && !e.IsDisabled);
            
            foreach (var edge in incomingEdges)
            {
                var sourceNodeExecution = session.Execution.NodeExecutions.First(ne => ne.NodeId == edge.SourceNodeId);
                
                if (sourceNodeExecution.Status != NodeExecutionStatus.Completed)
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

        private async Task<Task> ExecuteNodeWithSemaphoreAsync(WorkflowExecutionSession session, string nodeId, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var nodeExecution = await ExecuteNodeAsync(session.ExecutionId, nodeId, cancellationToken);
                    
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

        public async Task<NodeExecution> ExecuteNodeAsync(string executionId, string nodeId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation($"Executing node {nodeId} in workflow execution {executionId}");

            try
            {
                if (!_activeSessions.TryGetValue(executionId, out var session))
                {
                    throw new InvalidOperationException($"Execution session {executionId} not found");
                }

                var node = session.Workflow.Nodes.First(n => n.Id == nodeId);
                var nodeExecution = session.Execution.NodeExecutions.First(ne => ne.NodeId == nodeId);

                // Update node status
                nodeExecution.Status = NodeExecutionStatus.Running;
                nodeExecution.StartedAt = DateTime.UtcNow;
                await _unitOfWork.WorkflowExecutions.UpdateNodeExecutionAsync(ObjectId.Parse(executionId), nodeId, nodeExecution, cancellationToken);

                // Prepare input data
                var inputData = await PrepareNodeInputDataAsync(session, nodeId, cancellationToken);

                // Create program execution request
                var programRequest = new ProjectExecutionRequest
                {
                    ProgramId = node.ProgramId.ToString(),
                    VersionId = node.VersionId?.ToString(),
                    UserId = session.Execution.ExecutedBy,
                    Parameters = inputData.Data,
                    Environment = node.ExecutionSettings.Environment,
                    TimeoutMinutes = node.ExecutionSettings.TimeoutMinutes,
                    ResourceLimits = new ProjectResourceLimits
                    {
                        MaxCpuPercentage = node.ExecutionSettings.ResourceLimits.MaxCpuPercentage,
                        MaxMemoryMb = node.ExecutionSettings.ResourceLimits.MaxMemoryMb,
                        MaxDiskMb = node.ExecutionSettings.ResourceLimits.MaxDiskMb
                    }
                };

                // Execute program
                var programResult = await _projectExecutionEngine.ExecuteProjectAsync(programRequest, cancellationToken);

                // Process results
                var outputData = await ProcessNodeOutputAsync(session, nodeId, programResult, cancellationToken);

                // Update node execution
                nodeExecution.CompletedAt = DateTime.UtcNow;
                nodeExecution.Duration = nodeExecution.CompletedAt - nodeExecution.StartedAt;
                nodeExecution.ProgramExecutionId = ObjectId.Parse(programResult.ExecutionId);
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

                // Update node execution in database
                await _unitOfWork.WorkflowExecutions.UpdateNodeExecutionAsync(ObjectId.Parse(executionId), nodeId, nodeExecution, cancellationToken);

                return nodeExecution;
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

            // Add inputs from other nodes
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
                    throw new InvalidOperationException($"Required input '{inputMapping.InputName}' not available for node '{nodeId}'");
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
                        ["fileName"] = file,
                        ["path"] = file // Relative path
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

            await _unitOfWork.WorkflowExecutions.UpdateExecutionProgressAsync(ObjectId.Parse(session.ExecutionId), progress, cancellationToken);
        }

        private async Task FinalizeExecutionAsync(WorkflowExecutionSession session, CancellationToken cancellationToken)
        {
            var execution = session.Execution;
            
            if (session.FailedNodes.Any() && !session.Options.ContinueOnError)
            {
                execution.Status = WorkflowExecutionStatus.Failed;
                execution.Error = new WorkflowExecutionError
                {
                    ErrorType = WorkflowErrorType.ExecutionError,
                    Message = $"Workflow failed due to {session.FailedNodes.Count} failed nodes",
                    Timestamp = DateTime.UtcNow
                };
            }
            else
            {
                execution.Status = WorkflowExecutionStatus.Completed;
                
                // Collect final outputs
                var finalOutputs = new BsonDocument();
                foreach (var nodeOutput in session.NodeOutputs)
                {
                    finalOutputs[nodeOutput.Key] = nodeOutput.Value.Data;
                }
                
                execution.Results = new WorkflowExecutionResults
                {
                    FinalOutputs = finalOutputs,
                    IntermediateResults = session.NodeOutputs.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => kvp.Value.Data),
                    Summary = $"Workflow completed with {session.CompletedNodes.Count} successful nodes and {session.FailedNodes.Count} failed nodes"
                };
            }

            execution.CompletedAt = DateTime.UtcNow;
            await _unitOfWork.WorkflowExecutions.UpdateAsync(ObjectId.Parse(session.ExecutionId), execution, cancellationToken);
            
            _logger.LogInformation($"Workflow execution {session.ExecutionId} finalized with status {execution.Status}");
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

            await _unitOfWork.WorkflowExecutions.UpdateAsync(ObjectId.Parse(session.ExecutionId), execution, cancellationToken);
        }

        public async Task<WorkflowExecution> ResumeWorkflowAsync(string executionId, CancellationToken cancellationToken = default)
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
            return execution;
        }

        public async Task<bool> PauseWorkflowAsync(string executionId, CancellationToken cancellationToken = default)
        {
            if (_activeSessions.TryGetValue(executionId, out var session))
            {
                session.CancellationTokenSource.Cancel();
            }

            return await _unitOfWork.WorkflowExecutions.PauseExecutionAsync(ObjectId.Parse(executionId), cancellationToken);
        }

        public async Task<bool> CancelWorkflowAsync(string executionId, CancellationToken cancellationToken = default)
        {
            if (_activeSessions.TryGetValue(executionId, out var session))
            {
                session.CancellationTokenSource.Cancel();
                _activeSessions.TryRemove(executionId, out _);
            }

            return await _unitOfWork.WorkflowExecutions.CancelExecutionAsync(ObjectId.Parse(executionId), cancellationToken);
        }

        public async Task<WorkflowExecution> GetExecutionStatusAsync(string executionId, CancellationToken cancellationToken = default)
        {
            return await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
        }

        public async Task<List<WorkflowExecution>> GetActiveExecutionsAsync(CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.WorkflowExecutions.GetRunningExecutionsAsync(cancellationToken);
            return executions.ToList();
        }

        public async Task<NodeExecution> RetryNodeAsync(string executionId, string nodeId, CancellationToken cancellationToken = default)
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

            if (nodeExecution.RetryCount >= nodeExecution.MaxRetries)
            {
                throw new InvalidOperationException($"Maximum retry attempts exceeded for node {nodeId}");
            }

            nodeExecution.RetryCount++;
            nodeExecution.Status = NodeExecutionStatus.Retrying;
            await _unitOfWork.WorkflowExecutions.UpdateNodeExecutionAsync(ObjectId.Parse(executionId), nodeId, nodeExecution, cancellationToken);

            return await ExecuteNodeAsync(executionId, nodeId, cancellationToken);
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

            return await _unitOfWork.WorkflowExecutions.UpdateNodeExecutionAsync(ObjectId.Parse(executionId), nodeId, nodeExecution, cancellationToken);
        }

        public async Task<WorkflowDataContract> GetNodeOutputAsync(string executionId, string nodeId, CancellationToken cancellationToken = default)
        {
            if (_activeSessions.TryGetValue(executionId, out var session))
            {
                if (session.NodeOutputs.TryGetValue(nodeId, out var output))
                {
                    return output;
                }
            }

            // If not in active session, try to get from database
            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            if (execution?.Results?.IntermediateResults?.ContainsKey(nodeId) == true)
            {
                return new WorkflowDataContract
                {
                    SourceNodeId = nodeId,
                    Data = execution.Results.IntermediateResults[nodeId],
                    DataType = WorkflowDataType.JSON,
                    Timestamp = DateTime.UtcNow
                };
            }

            throw new InvalidOperationException($"Output not found for node {nodeId} in execution {executionId}");
        }

        public async Task<Dictionary<string, WorkflowDataContract>> GetAllNodeOutputsAsync(string executionId, CancellationToken cancellationToken = default)
        {
            if (_activeSessions.TryGetValue(executionId, out var session))
            {
                return session.NodeOutputs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            if (execution?.Results?.IntermediateResults != null)
            {
                return execution.Results.IntermediateResults.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new WorkflowDataContract
                    {
                        SourceNodeId = kvp.Key,
                        Data = kvp.Value,
                        DataType = WorkflowDataType.JSON,
                        Timestamp = DateTime.UtcNow
                    });
            }

            return new Dictionary<string, WorkflowDataContract>();
        }

        public async Task<WorkflowExecutionStatistics> GetExecutionStatisticsAsync(string executionId, CancellationToken cancellationToken = default)
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

            return stats;
        }

        public async Task<List<WorkflowExecutionLog>> GetExecutionLogsAsync(string executionId, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        {
            var execution = await _unitOfWork.WorkflowExecutions.GetByIdAsync(ObjectId.Parse(executionId), cancellationToken);
            if (execution == null)
            {
                throw new InvalidOperationException($"Execution {executionId} not found");
            }

            return execution.Logs.Skip(skip).Take(take).ToList();
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
            _activeSessions.TryRemove(executionId, out _);

            // Clean up temporary files and resources
            // This would involve cleaning up storage, temporary files, etc.
            
            return true;
        }
    }

    public class WorkflowExecutionSession
    {
        public required string ExecutionId { get; set; }
        public required Workflow Workflow { get; set; }
        public required WorkflowExecution Execution { get; set; }
        public required WorkflowExecutionOptions Options { get; set; }
        public required ConcurrentDictionary<string, WorkflowDataContract> NodeOutputs { get; set; }
        public required ConcurrentDictionary<string, Task<NodeExecution>> RunningNodes { get; set; }
        public required HashSet<string> CompletedNodes { get; set; }
        public required HashSet<string> FailedNodes { get; set; }
        public required CancellationTokenSource CancellationTokenSource { get; set; }
    }
}