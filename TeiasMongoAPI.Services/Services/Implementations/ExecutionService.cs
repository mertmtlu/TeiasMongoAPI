using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using System.Diagnostics;
using System.Text.Json;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Interfaces.Execution;
using TeiasMongoAPI.Services.Specifications;
using TeiasMongoAPI.Services.Services.Base;
using ExecutionModel = TeiasMongoAPI.Core.Models.Collaboration.Execution;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class ExecutionService : BaseService, IExecutionService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IProgramService _programService;
        private readonly IVersionService _versionService;
        private readonly IDeploymentService _deploymentService;
        private readonly IProjectExecutionEngine _projectExecutionEngine;
        private readonly IGroupService _groupService;
        private readonly ExecutionSettings _settings;
        private readonly Dictionary<string, ExecutionContext> _activeExecutions = new();

        public ExecutionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IFileStorageService fileStorageService,
            IProgramService programService,
            IVersionService versionService,
            IDeploymentService deploymentService,
            IProjectExecutionEngine projectExecutionEngine,
            IGroupService groupService,
            IOptions<ExecutionSettings> settings,
            ILogger<ExecutionService> logger)
            : base(unitOfWork, mapper, logger)
        {
            _fileStorageService = fileStorageService;
            _programService = programService;
            _versionService = versionService;
            _deploymentService = deploymentService;
            _projectExecutionEngine = projectExecutionEngine;
            _groupService = groupService;
            _settings = settings.Value;
        }

        #region Basic CRUD Operations

        public async Task<ExecutionDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            var dto = _mapper.Map<ExecutionDetailDto>(execution);

            // Get program details
            try
            {
                var program = await _unitOfWork.Programs.GetByIdAsync(execution.ProgramId, cancellationToken);
                if (program != null)
                {
                    dto.ProgramName = program.Name;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get program details for execution {ExecutionId}", id);
            }

            // Get user details
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(execution.UserId), cancellationToken);
                if (user != null)
                {
                    dto.UserName = user.FullName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get user details for execution {ExecutionId}", id);
            }

            // Get version details
            try
            {
                var version = await _unitOfWork.Versions.GetByIdAsync(execution.VersionId, cancellationToken);
                if (version != null)
                {
                    dto.VersionNumber = version.VersionNumber;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get version details for execution {ExecutionId}", id);
            }


            // Get recent logs
            dto.RecentLogs = await GetExecutionLogsAsync(id, 50, cancellationToken);

            // Get execution environment
            dto.Environment = await GetExecutionEnvironmentAsync(execution.ProgramId.ToString(), cancellationToken);

            // Get web app details if applicable
            if (execution.ExecutionType == "web_app_deploy" && !string.IsNullOrEmpty(execution.Results.WebAppUrl))
            {
                dto.WebAppUrl = execution.Results.WebAppUrl;
                dto.WebAppStatus = await GetWebApplicationStatusAsync(id, cancellationToken);
            }

            return dto;
        }

        public async Task<PagedResponse<ExecutionListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            // Use Specification Pattern for database-level pagination
            var spec = new AllExecutionsSpecification(pagination);
            var (executions, totalCount) = await _unitOfWork.Executions.FindWithSpecificationAsync(spec, cancellationToken);

            var dtos = await MapExecutionListDtosAsync(executions.ToList(), cancellationToken);

            return new PagedResponse<ExecutionListDto>(dtos, pagination.PageNumber, pagination.PageSize, (int)totalCount);
        }

        public async Task<PagedResponse<ExecutionListDto>> SearchAsync(ExecutionSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            // Use Specification Pattern for database-level pagination and filtering
            var spec = new ExecutionSearchSpecification(searchDto, pagination);
            var (executions, totalCount) = await _unitOfWork.Executions.FindWithSpecificationAsync(spec, cancellationToken);

            var dtos = await MapExecutionListDtosAsync(executions.ToList(), cancellationToken);

            return new PagedResponse<ExecutionListDto>(dtos, pagination.PageNumber, pagination.PageSize, (int)totalCount);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            // Cannot delete running executions
            if (execution.Status == "running" || execution.Status == "paused")
            {
                throw new InvalidOperationException("Cannot delete a running or paused execution. Stop it first.");
            }

            // Stop execution if still active
            if (_activeExecutions.ContainsKey(id))
            {
                await StopExecutionAsync(id, cancellationToken);
            }

            // Delete output files (these are execution-specific, not version files)
            try
            {
                foreach (var outputFile in execution.Results.OutputFiles)
                {
                    // Output files are stored separately from version files
                    // They should be deleted through a different mechanism
                    _logger.LogDebug("Would delete output file {OutputFile} for execution {ExecutionId}", outputFile, id);
                }
                _logger.LogInformation("Cleaned up output files for execution {ExecutionId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete some output files for execution {ExecutionId}", id);
            }

            var success = await _unitOfWork.Executions.DeleteAsync(objectId, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Deleted execution {ExecutionId}", id);
            }

            return success;
        }

        #endregion

        #region Execution Filtering and History

        public async Task<PagedResponse<ExecutionListDto>> GetByProgramIdAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var executions = await _unitOfWork.Executions.GetByProgramIdAsync(programObjectId, cancellationToken);
            return await CreatePagedExecutionResponse(executions, pagination, cancellationToken);
        }

        public async Task<PagedResponse<ExecutionListDto>> GetByVersionIdAsync(string versionId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var versionObjectId = ParseObjectId(versionId);

            if (!await _unitOfWork.Versions.ExistsAsync(versionObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            var executions = await _unitOfWork.Executions.GetByVersionIdAsync(versionObjectId, cancellationToken);
            return await CreatePagedExecutionResponse(executions, pagination, cancellationToken);
        }

        public async Task<PagedResponse<ExecutionListDto>> GetByUserIdAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.Executions.GetByUserIdAsync(userId, cancellationToken);
            return await CreatePagedExecutionResponse(executions, pagination, cancellationToken);
        }

        public async Task<PagedResponse<ExecutionListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.Executions.GetByStatusAsync(status, cancellationToken);
            return await CreatePagedExecutionResponse(executions, pagination, cancellationToken);
        }

        public async Task<PagedResponse<ExecutionListDto>> GetRunningExecutionsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.Executions.GetRunningExecutionsAsync(cancellationToken);
            return await CreatePagedExecutionResponse(executions, pagination, cancellationToken);
        }

        public async Task<PagedResponse<ExecutionListDto>> GetCompletedExecutionsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.Executions.GetCompletedExecutionsAsync(cancellationToken);
            return await CreatePagedExecutionResponse(executions, pagination, cancellationToken);
        }

        public async Task<PagedResponse<ExecutionListDto>> GetFailedExecutionsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.Executions.GetFailedExecutionsAsync(cancellationToken);
            return await CreatePagedExecutionResponse(executions, pagination, cancellationToken);
        }

        public async Task<List<ExecutionListDto>> GetRecentExecutionsAsync(int count = 10, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.Executions.GetRecentExecutionsAsync(count, cancellationToken);
            return await MapExecutionListDtosAsync(executions.ToList(), cancellationToken);
        }

        #endregion

        #region Program Execution Operations

        public async Task<ExecutionDto> ExecuteProgramAsync(string programId, ObjectId? currentUser, ProgramExecutionRequestDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Determine version to execute
            string versionId;
            if (!string.IsNullOrEmpty(program.CurrentVersion))
            {
                versionId = program.CurrentVersion;
            }
            else
            {
                // Get latest approved version
                var latestVersion = await _versionService.GetLatestVersionForProgramAsync(programId, cancellationToken);
                versionId = latestVersion.Id;
            }

            var versionObjectId = ParseObjectId(versionId);
            var version = await _unitOfWork.Versions.GetByIdAsync(versionObjectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"No executable version found for program {programId}.");
            }

            if (version.Status != "approved")
            {
                throw new InvalidOperationException("Can only execute approved versions.");
            }

            string user = "Undefined";

            if (currentUser is ObjectId userId)
            {
                user = userId.ToString();
            }

            // Check execution permissions and limits
            await ValidateExecutionLimitsAsync(programId, user, cancellationToken);

            // Create execution record
            var execution = new ExecutionModel
            {
                ProgramId = programObjectId,
                VersionId = versionObjectId,
                UserId = user, // Should come from current user context
                ExecutionType = "project_execution",
                StartedAt = DateTime.UtcNow,
                Status = "running",
                Parameters = ConvertJsonElementToBson(dto.Parameters),
                Results = new ExecutionResults(),
                ResourceUsage = new ResourceUsage()
            };

            var createdExecution = await _unitOfWork.Executions.CreateAsync(execution, cancellationToken);
            var executionId = createdExecution._ID.ToString();

            _logger.LogInformation("Started project execution {ExecutionId} for program {ProgramId} version {VersionNumber}",
                executionId, programId, version.VersionNumber);

            // Execute in background using version-specific files
            _ = Task.Run(async () => await ExecuteProjectInBackgroundAsync(createdExecution, dto, cancellationToken));

            return _mapper.Map<ExecutionDto>(createdExecution);
        }

        public async Task<ExecutionDto> ExecuteVersionAsync(string versionId, VersionExecutionRequestDto dto, ObjectId? currentUserId, CancellationToken cancellationToken = default)
        {
            var versionObjectId = ParseObjectId(versionId);
            var version = await _unitOfWork.Versions.GetByIdAsync(versionObjectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            if (version.Status != "approved")
            {
                throw new InvalidOperationException("Can only execute approved versions.");
            }

            // Check execution limits
            await ValidateExecutionLimitsAsync(version.ProgramId.ToString(), currentUserId.ToString(), cancellationToken);

            // Create execution record
            var execution = new ExecutionModel
            {
                ProgramId = version.ProgramId,
                VersionId = versionObjectId,
                UserId = currentUserId.ToString(), // Should come from current user context
                ExecutionType = "project_execution",
                StartedAt = DateTime.UtcNow,
                Status = "running",
                //Parameters = dto.Parameters,
                Results = new ExecutionResults(),
                ResourceUsage = new ResourceUsage()
            };

            var createdExecution = await _unitOfWork.Executions.CreateAsync(execution, cancellationToken);
            var executionId = createdExecution._ID.ToString();

            _logger.LogInformation("Started version execution {ExecutionId} for version {VersionId}",
                executionId, versionId);

            // Convert to program execution request and execute
            var programRequest = new ProgramExecutionRequestDto
            {
                Parameters = dto.Parameters,
                Environment = dto.Environment,
                ResourceLimits = dto.ResourceLimits,
                SaveResults = dto.SaveResults,
                TimeoutMinutes = dto.TimeoutMinutes
            };

            _ = Task.Run(async () => await ExecuteProjectInBackgroundAsync(createdExecution, programRequest, cancellationToken));

            return _mapper.Map<ExecutionDto>(createdExecution);
        }

        public async Task<ExecutionDto> ExecuteWithParametersAsync(string programId, ObjectId? currentUser, ExecutionParametersDto dto, CancellationToken cancellationToken = default)
        {
            var programRequest = new ProgramExecutionRequestDto
            {
                Parameters = dto.Parameters,
                Environment = dto.Environment,
                ResourceLimits = dto.ResourceLimits,
                SaveResults = dto.SaveResults,
                TimeoutMinutes = dto.TimeoutMinutes
            };

            // Use specific version if provided, otherwise use current/latest
            if (!string.IsNullOrEmpty(dto.VersionId))
            {
                var versionRequest = new VersionExecutionRequestDto
                {
                    Parameters = dto.Parameters,
                    Environment = dto.Environment,
                    ResourceLimits = dto.ResourceLimits,
                    SaveResults = dto.SaveResults,
                    TimeoutMinutes = dto.TimeoutMinutes
                };
                return await ExecuteVersionAsync(dto.VersionId, versionRequest, currentUser, cancellationToken);
            }

            return await ExecuteProgramAsync(programId, currentUser, programRequest, cancellationToken);
        }

        #endregion

        #region Execution Control and Monitoring

        public async Task<bool> StopExecutionAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            if (execution.Status != "running" && execution.Status != "paused")
            {
                _logger.LogWarning("Attempted to stop execution {ExecutionId} with status {Status}", id, execution.Status);
                return false;
            }

            // Try to cancel via project execution engine first
            var cancelled = await _projectExecutionEngine.CancelExecutionAsync(id, cancellationToken);

            // Update status in database
            var success = await _unitOfWork.Executions.UpdateStatusAsync(objectId, "stopped", cancellationToken);

            // Stop active execution context
            if (_activeExecutions.TryGetValue(id, out var context))
            {
                try
                {
                    context.CancellationTokenSource.Cancel();
                    if (context.Process != null && !context.Process.HasExited)
                    {
                        context.Process.Kill();
                        context.Process.WaitForExit(5000);
                    }
                    _activeExecutions.Remove(id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to stop execution process for {ExecutionId}", id);
                }
            }

            if (success)
            {
                _logger.LogInformation("Stopped execution {ExecutionId}", id);
            }

            return success;
        }

        public async Task<bool> PauseExecutionAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            if (execution.Status != "running")
            {
                return false;
            }

            var success = await _unitOfWork.Executions.UpdateStatusAsync(objectId, "paused", cancellationToken);

            if (success)
            {
                _logger.LogInformation("Paused execution {ExecutionId}", id);
            }

            return success;
        }

        public async Task<bool> ResumeExecutionAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            if (execution.Status != "paused")
            {
                return false;
            }

            var success = await _unitOfWork.Executions.UpdateStatusAsync(objectId, "running", cancellationToken);

            if (success)
            {
                _logger.LogInformation("Resumed execution {ExecutionId}", id);
            }

            return success;
        }

        public async Task<ExecutionStatusDto> GetExecutionStatusAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            var progress = CalculateExecutionProgress(execution);
            var currentStep = GetCurrentExecutionStep(execution);

            return new ExecutionStatusDto
            {
                Id = id,
                Status = execution.Status,
                StartedAt = execution.StartedAt,
                CompletedAt = execution.CompletedAt,
                Progress = progress,
                CurrentStep = currentStep,
                ResourceUsage = _mapper.Map<ExecutionResourceUsageDto>(execution.ResourceUsage),
                StatusMessage = GetStatusMessage(execution)
            };
        }

        public async Task<string> GetExecutionOutputStreamAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            // Return current output (this would be streamed in real implementation)
            return execution.Results.Output ?? string.Empty;
        }

        public async Task<List<string>> GetExecutionLogsAsync(string id, int lines = 100, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            // In a real implementation, this would read from log files
            var logs = new List<string>
            {
                $"Execution {id} started at {execution.StartedAt:yyyy-MM-dd HH:mm:ss}",
                $"Status: {execution.Status}",
                $"Program: {execution.ProgramId}",
                $"Version: {execution.VersionId}",
                $"User: {execution.UserId}"
            };

            if (!string.IsNullOrEmpty(execution.Results.Output))
            {
                var outputLines = execution.Results.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                logs.AddRange(outputLines.TakeLast(lines - logs.Count));
            }

            if (!string.IsNullOrEmpty(execution.Results.Error))
            {
                logs.Add($"ERROR: {execution.Results.Error}");
            }

            return logs.TakeLast(lines).ToList();
        }

        #endregion

        #region Execution Results Management

        public async Task<ExecutionResultDto> GetExecutionResultAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            return _mapper.Map<ExecutionResultDto>(execution.Results);
        }


        #endregion

        #region Web Application Execution

        public async Task<ExecutionDto> DeployWebApplicationAsync(string programId, WebAppDeploymentRequestDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Get version to deploy
            string versionId;
            if (!string.IsNullOrEmpty(program.CurrentVersion))
            {
                versionId = program.CurrentVersion;
            }
            else
            {
                var latestVersion = await _versionService.GetLatestVersionForProgramAsync(programId, cancellationToken);
                versionId = latestVersion.Id;
            }

            var versionObjectId = ParseObjectId(versionId);
            var version = await _unitOfWork.Versions.GetByIdAsync(versionObjectId, cancellationToken);

            if (version?.Status != "approved")
            {
                throw new InvalidOperationException("Can only deploy approved versions.");
            }

            // Create execution record for web app deployment
            var execution = new ExecutionModel
            {
                ProgramId = programObjectId,
                VersionId = versionObjectId,
                UserId = "system", // Should come from current user context BaseController holds CurrentUserId property
                ExecutionType = "web_app_deploy",
                StartedAt = DateTime.UtcNow,
                Status = "running",
                Parameters = dto.Configuration,
                Results = new ExecutionResults(),
                ResourceUsage = new ResourceUsage()
            };

            var createdExecution = await _unitOfWork.Executions.CreateAsync(execution, cancellationToken);

            // Deploy web application using deployment service
            try
            {
                var deploymentResult = await _programService.DeployPreBuiltAppAsync(programId, new ProgramDeploymentRequestDto
                {
                    DeploymentType = AppDeploymentType.PreBuiltWebApp,
                    Configuration = dto.Configuration,
                    SupportedFeatures = dto.Features
                }, cancellationToken);

                // Update execution with deployment results
                execution.Results.WebAppUrl = deploymentResult.ApplicationUrl;
                execution.Status = "completed";
                execution.CompletedAt = DateTime.UtcNow;

                await _unitOfWork.Executions.UpdateAsync(createdExecution._ID, execution, cancellationToken);

                _logger.LogInformation("Deployed web application for program {ProgramId} with execution {ExecutionId}",
                    programId, createdExecution._ID);
            }
            catch (Exception ex)
            {
                execution.Status = "failed";
                execution.Results.Error = ex.Message;
                execution.CompletedAt = DateTime.UtcNow;
                await _unitOfWork.Executions.UpdateAsync(createdExecution._ID, execution, cancellationToken);

                _logger.LogError(ex, "Failed to deploy web application for program {ProgramId}", programId);
                throw;
            }

            return _mapper.Map<ExecutionDto>(execution);
        }

        public async Task<string> GetWebApplicationUrlAsync(string executionId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(executionId);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {executionId} not found.");
            }

            if (execution.ExecutionType != "web_app_deploy")
            {
                throw new InvalidOperationException("Execution is not a web application deployment.");
            }

            return execution.Results.WebAppUrl ?? string.Empty;
        }

        public async Task<WebAppStatusDto> GetWebApplicationStatusAsync(string executionId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(executionId);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {executionId} not found.");
            }

            if (execution.ExecutionType != "web_app_deploy")
            {
                throw new InvalidOperationException("Execution is not a web application deployment.");
            }

            try
            {
                var health = await _deploymentService.GetApplicationHealthAsync(execution.ProgramId.ToString(), cancellationToken);

                return new WebAppStatusDto
                {
                    Status = health.Status == "healthy" ? "active" : "inactive",
                    Url = execution.Results.WebAppUrl ?? string.Empty,
                    IsHealthy = health.Status == "healthy",
                    LastHealthCheck = health.LastCheck,
                    ResponseTime = health.ResponseTimeMs,
                    ErrorMessage = health.ErrorMessage,
                    Metrics = health.Details
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get web app status for execution {ExecutionId}", executionId);

                return new WebAppStatusDto
                {
                    Status = "unknown",
                    Url = execution.Results.WebAppUrl ?? string.Empty,
                    IsHealthy = false,
                    LastHealthCheck = DateTime.UtcNow,
                    ErrorMessage = "Failed to check application status"
                };
            }
        }

        public async Task<bool> RestartWebApplicationAsync(string executionId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(executionId);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {executionId} not found.");
            }

            if (execution.ExecutionType != "web_app_deploy")
            {
                throw new InvalidOperationException("Execution is not a web application deployment.");
            }

            try
            {
                var success = await _deploymentService.RestartApplicationAsync(execution.ProgramId.ToString(), cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Restarted web application for execution {ExecutionId}", executionId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restart web application for execution {ExecutionId}", executionId);
                return false;
            }
        }

        public async Task<bool> StopWebApplicationAsync(string executionId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(executionId);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {executionId} not found.");
            }

            if (execution.ExecutionType != "web_app_deploy")
            {
                throw new InvalidOperationException("Execution is not a web application deployment.");
            }

            try
            {
                var success = await _deploymentService.StopApplicationAsync(execution.ProgramId.ToString(), cancellationToken);

                if (success)
                {
                    // Update execution status
                    await _unitOfWork.Executions.UpdateStatusAsync(objectId, "stopped", cancellationToken);
                    _logger.LogInformation("Stopped web application for execution {ExecutionId}", executionId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop web application for execution {ExecutionId}", executionId);
                return false;
            }
        }

        #endregion

        #region Resource Management and Monitoring

        public async Task<ExecutionResourceUsageDto> GetResourceUsageAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            return _mapper.Map<ExecutionResourceUsageDto>(execution.ResourceUsage);
        }

        public async Task<bool> UpdateResourceUsageAsync(string id, ExecutionResourceUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);

            if (!await _unitOfWork.Executions.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            var success = await _unitOfWork.Executions.UpdateResourceUsageAsync(
                objectId, dto.CpuTime, dto.MemoryUsed, dto.DiskUsed, cancellationToken);

            if (success)
            {
                _logger.LogDebug("Updated resource usage for execution {ExecutionId}", id);
            }

            return success;
        }

        public async Task<List<ExecutionResourceTrendDto>> GetResourceTrendsAsync(string programId, int days = 7, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var executions = await _unitOfWork.Executions.GetByProgramIdAsync(programObjectId, cancellationToken);

            var trends = executions
                .Where(e => e.StartedAt >= cutoffDate)
                .GroupBy(e => e.StartedAt.Date)
                .Select(g => new ExecutionResourceTrendDto
                {
                    Timestamp = g.Key,
                    CpuUsage = g.Average(e => e.ResourceUsage.CpuTime),
                    MemoryUsage = (long)g.Average(e => e.ResourceUsage.MemoryUsed),
                    DiskUsage = (long)g.Average(e => e.ResourceUsage.DiskUsed),
                    ActiveExecutions = g.Count(e => e.Status == "running")
                })
                .OrderBy(t => t.Timestamp)
                .ToList();

            return trends;
        }

        public async Task<ExecutionResourceLimitsDto> GetResourceLimitsAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Return default resource limits (in a real implementation, these could be configurable per program)
            return new ExecutionResourceLimitsDto
            {
                MaxCpuPercentage = _settings.DefaultMaxCpuPercentage,
                MaxMemoryMb = _settings.DefaultMaxMemoryMb,
                MaxDiskMb = _settings.DefaultMaxDiskMb,
                MaxExecutionTimeMinutes = _settings.DefaultMaxExecutionTimeMinutes,
                MaxConcurrentExecutions = _settings.DefaultMaxConcurrentExecutions
            };
        }

        public async Task<bool> UpdateResourceLimitsAsync(string programId, ExecutionResourceLimitsUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // In a real implementation, this would update program-specific resource limits
            _logger.LogInformation("Updated resource limits for program {ProgramId}", programId);
            return true;
        }

        #endregion

        #region Execution Statistics and Analytics

        public async Task<ExecutionStatsDto> GetExecutionStatsAsync(ExecutionStatsFilterDto? filter = null, CancellationToken cancellationToken = default)
        {
            var allExecutions = await _unitOfWork.Executions.GetAllAsync(cancellationToken);
            var executions = allExecutions.AsQueryable();

            // Apply filters
            if (filter != null)
            {
                if (!string.IsNullOrEmpty(filter.ProgramId))
                {
                    var programObjectId = ParseObjectId(filter.ProgramId);
                    executions = executions.Where(e => e.ProgramId == programObjectId);
                }

                if (!string.IsNullOrEmpty(filter.UserId))
                {
                    executions = executions.Where(e => e.UserId == filter.UserId);
                }

                if (filter.FromDate.HasValue)
                {
                    executions = executions.Where(e => e.StartedAt >= filter.FromDate.Value);
                }

                if (filter.ToDate.HasValue)
                {
                    executions = executions.Where(e => e.StartedAt <= filter.ToDate.Value);
                }

                if (filter.Statuses?.Any() == true)
                {
                    executions = executions.Where(e => filter.Statuses.Contains(e.Status));
                }

                if (!string.IsNullOrEmpty(filter.ExecutionType))
                {
                    executions = executions.Where(e => e.ExecutionType == filter.ExecutionType);
                }
            }

            var executionsList = executions.ToList();
            var completedExecutions = executionsList.Where(e => e.CompletedAt.HasValue).ToList();

            return new ExecutionStatsDto
            {
                TotalExecutions = executionsList.Count,
                SuccessfulExecutions = executionsList.Count(e => e.Status == "completed" && e.Results.ExitCode == 0),
                FailedExecutions = executionsList.Count(e => e.Status == "failed" || (e.Status == "completed" && e.Results.ExitCode != 0)),
                RunningExecutions = executionsList.Count(e => e.Status == "running"),
                AverageExecutionTime = completedExecutions.Any()
                    ? completedExecutions.Average(e => (e.CompletedAt!.Value - e.StartedAt).TotalMinutes)
                    : 0,
                SuccessRate = executionsList.Any()
                    ? (double)executionsList.Count(e => e.Status == "completed" && e.Results.ExitCode == 0) / executionsList.Count * 100
                    : 0,
                TotalCpuTime = (long)executionsList.Sum(e => e.ResourceUsage.CpuTime),
                TotalMemoryUsed = executionsList.Sum(e => e.ResourceUsage.MemoryUsed),
                ExecutionsByStatus = executionsList.GroupBy(e => e.Status).ToDictionary(g => g.Key, g => g.Count()),
                ExecutionsByType = executionsList.GroupBy(e => e.ExecutionType).ToDictionary(g => g.Key, g => g.Count())
            };
        }

        public async Task<List<ExecutionTrendDto>> GetExecutionTrendsAsync(string? programId = null, int days = 30, CancellationToken cancellationToken = default)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            IEnumerable<ExecutionModel> executions;

            if (!string.IsNullOrEmpty(programId))
            {
                var programObjectId = ParseObjectId(programId);
                executions = await _unitOfWork.Executions.GetByProgramIdAsync(programObjectId, cancellationToken);
            }
            else
            {
                executions = await _unitOfWork.Executions.GetAllAsync(cancellationToken);
            }

            var trends = executions
                .Where(e => e.StartedAt >= cutoffDate)
                .GroupBy(e => e.StartedAt.Date)
                .Select(g => new ExecutionTrendDto
                {
                    Date = g.Key,
                    ExecutionCount = g.Count(),
                    SuccessfulCount = g.Count(e => e.Status == "completed" && e.Results.ExitCode == 0),
                    FailedCount = g.Count(e => e.Status == "failed" || (e.Status == "completed" && e.Results.ExitCode != 0)),
                    AverageExecutionTime = g.Where(e => e.CompletedAt.HasValue).Any()
                        ? g.Where(e => e.CompletedAt.HasValue).Average(e => (e.CompletedAt!.Value - e.StartedAt).TotalMinutes)
                        : 0,
                    TotalResourceUsage = (long)g.Sum(e => e.ResourceUsage.CpuTime + e.ResourceUsage.MemoryUsed / 1024)
                })
                .OrderBy(t => t.Date)
                .ToList();

            return trends;
        }

        public async Task<List<ExecutionPerformanceDto>> GetExecutionPerformanceAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var executions = await _unitOfWork.Executions.GetByProgramIdAsync(programObjectId, cancellationToken);
            var executionsList = executions.ToList();
            var completedExecutions = executionsList.Where(e => e.CompletedAt.HasValue).ToList();

            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            return new List<ExecutionPerformanceDto>
            {
                new ExecutionPerformanceDto
                {
                    ProgramId = programId,
                    ProgramName = program?.Name ?? "Unknown",
                    ExecutionCount = executionsList.Count,
                    SuccessRate = executionsList.Any()
                        ? (double)executionsList.Count(e => e.Status == "completed" && e.Results.ExitCode == 0) / executionsList.Count * 100
                        : 0,
                    AverageExecutionTime = completedExecutions.Any()
                        ? completedExecutions.Average(e => (e.CompletedAt!.Value - e.StartedAt).TotalMinutes)
                        : 0,
                    AverageResourceUsage = executionsList.Any()
                        ? executionsList.Average(e => e.ResourceUsage.CpuTime + e.ResourceUsage.MemoryUsed / 1024 / 1024)
                        : 0,
                    LastExecution = executionsList.OrderByDescending(e => e.StartedAt).FirstOrDefault()?.StartedAt ?? DateTime.MinValue
                }
            };
        }

        public async Task<ExecutionSummaryDto> GetUserExecutionSummaryAsync(string userId, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.Executions.GetByUserIdAsync(userId, cancellationToken);
            var executionsList = executions.ToList();

            var programPerformance = new List<ExecutionPerformanceDto>();

            var programGroups = executionsList.GroupBy(e => e.ProgramId);
            foreach (var group in programGroups)
            {
                var program = await _unitOfWork.Programs.GetByIdAsync(group.Key, cancellationToken);
                var groupExecutions = group.ToList();
                var completedExecutions = groupExecutions.Where(e => e.CompletedAt.HasValue).ToList();

                programPerformance.Add(new ExecutionPerformanceDto
                {
                    ProgramId = group.Key.ToString(),
                    ProgramName = program?.Name ?? "Unknown",
                    ExecutionCount = groupExecutions.Count,
                    SuccessRate = groupExecutions.Any()
                        ? (double)groupExecutions.Count(e => e.Status == "completed" && e.Results.ExitCode == 0) / groupExecutions.Count * 100
                        : 0,
                    AverageExecutionTime = completedExecutions.Any()
                        ? completedExecutions.Average(e => (e.CompletedAt!.Value - e.StartedAt).TotalMinutes)
                        : 0,
                    AverageResourceUsage = groupExecutions.Any()
                        ? groupExecutions.Average(e => e.ResourceUsage.CpuTime + e.ResourceUsage.MemoryUsed / 1024 / 1024)
                        : 0,
                    LastExecution = groupExecutions.OrderByDescending(e => e.StartedAt).FirstOrDefault()?.StartedAt ?? DateTime.MinValue
                });
            }

            return new ExecutionSummaryDto
            {
                UserId = userId,
                TotalExecutions = executionsList.Count,
                SuccessfulExecutions = executionsList.Count(e => e.Status == "completed" && e.Results.ExitCode == 0),
                FailedExecutions = executionsList.Count(e => e.Status == "failed" || (e.Status == "completed" && e.Results.ExitCode != 0)),
                TotalCpuTime = executionsList.Sum(e => e.ResourceUsage.CpuTime),
                TotalMemoryUsed = executionsList.Sum(e => e.ResourceUsage.MemoryUsed),
                LastExecution = executionsList.OrderByDescending(e => e.StartedAt).FirstOrDefault()?.StartedAt,
                ProgramPerformance = programPerformance
            };
        }

        #endregion

        #region Execution Configuration and Environment

        public async Task<ExecutionEnvironmentDto> GetExecutionEnvironmentAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var resourceLimits = await GetResourceLimitsAsync(programId, cancellationToken);

            return new ExecutionEnvironmentDto
            {
                ProgramId = programId,
                Environment = new Dictionary<string, string>
                {
                    { "PROGRAM_ID", programId },
                    { "EXECUTION_TIMEOUT", _settings.DefaultMaxExecutionTimeMinutes.ToString() },
                    { "MAX_MEMORY", _settings.DefaultMaxMemoryMb.ToString() },
                    { "MAX_CPU", _settings.DefaultMaxCpuPercentage.ToString() }
                },
                ResourceLimits = resourceLimits,
                Configuration = new Dictionary<string, object>
                {
                    { "isolation", true },
                    { "networking", false },
                    { "fileSystemAccess", "restricted" }
                },
                LastUpdated = DateTime.UtcNow
            };
        }

        public async Task<bool> UpdateExecutionEnvironmentAsync(string programId, ExecutionEnvironmentUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // In a real implementation, this would store program-specific environment settings
            _logger.LogInformation("Updated execution environment for program {ProgramId}", programId);
            return true;
        }

        public async Task<List<ExecutionTemplateDto>> GetExecutionTemplatesAsync(string? language = null, CancellationToken cancellationToken = default)
        {
            var templates = new List<ExecutionTemplateDto>
            {
                new ExecutionTemplateDto
                {
                    Id = "python-basic",
                    Name = "Basic Python Execution",
                    Description = "Standard Python script execution with common libraries",
                    Language = "python",
                    ParameterSchema = new { input_file = "string", output_format = "string" },
                    DefaultEnvironment = new Dictionary<string, string> { { "PYTHONPATH", "/usr/local/lib/python3.9" } },
                    DefaultResourceLimits = new ExecutionResourceLimitsDto
                    {
                        MaxCpuPercentage = 50,
                        MaxMemoryMb = 512,
                        MaxExecutionTimeMinutes = 10
                    }
                },
                new ExecutionTemplateDto
                {
                    Id = "csharp-console",
                    Name = "C# Console Application",
                    Description = "Execute C# console applications with .NET runtime",
                    Language = "csharp",
                    ParameterSchema = new { args = "array", config_file = "string" },
                    DefaultEnvironment = new Dictionary<string, string> { { "DOTNET_CLI_TELEMETRY_OPTOUT", "1" } },
                    DefaultResourceLimits = new ExecutionResourceLimitsDto
                    {
                        MaxCpuPercentage = 70,
                        MaxMemoryMb = 1024,
                        MaxExecutionTimeMinutes = 15
                    }
                }
            };

            if (!string.IsNullOrEmpty(language))
            {
                templates = templates.Where(t => t.Language.Equals(language, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return templates;
        }

        public async Task<ExecutionValidationResult> ValidateExecutionRequestAsync(ProgramExecutionRequestDto dto, CancellationToken cancellationToken = default)
        {
            var result = new ExecutionValidationResult { IsValid = true };

            // Validate resource limits
            if (dto.ResourceLimits != null)
            {
                if (dto.ResourceLimits.MaxMemoryMb > _settings.MaxAllowedMemoryMb)
                {
                    result.Errors.Add($"Memory limit exceeds maximum allowed ({_settings.MaxAllowedMemoryMb} MB)");
                    result.IsValid = false;
                }

                if (dto.ResourceLimits.MaxExecutionTimeMinutes > _settings.MaxAllowedExecutionTimeMinutes)
                {
                    result.Errors.Add($"Execution time limit exceeds maximum allowed ({_settings.MaxAllowedExecutionTimeMinutes} minutes)");
                    result.IsValid = false;
                }
            }

            // Validate timeout
            if (dto.TimeoutMinutes > _settings.MaxAllowedExecutionTimeMinutes)
            {
                result.Errors.Add($"Timeout exceeds maximum allowed ({_settings.MaxAllowedExecutionTimeMinutes} minutes)");
                result.IsValid = false;
            }

            // Recommend optimal resource limits based on default settings
            result.RecommendedLimits = new ExecutionResourceLimitsDto
            {
                MaxCpuPercentage = _settings.DefaultMaxCpuPercentage,
                MaxMemoryMb = _settings.DefaultMaxMemoryMb,
                MaxDiskMb = _settings.DefaultMaxDiskMb,
                MaxExecutionTimeMinutes = Math.Min(dto.TimeoutMinutes, _settings.DefaultMaxExecutionTimeMinutes),
                MaxConcurrentExecutions = _settings.DefaultMaxConcurrentExecutions
            };

            return result;
        }

        #endregion

        #region Execution Queue and Scheduling

        public async Task<ExecutionQueueStatusDto> GetExecutionQueueStatusAsync(CancellationToken cancellationToken = default)
        {
            var runningExecutions = await _unitOfWork.Executions.GetRunningExecutionsAsync(cancellationToken);
            var runningCount = runningExecutions.Count();

            // In a real implementation, this would check actual queue status
            return new ExecutionQueueStatusDto
            {
                QueueLength = 0, // No queue implementation in this basic version
                RunningExecutions = runningCount,
                MaxConcurrentExecutions = _settings.MaxConcurrentExecutions,
                AverageWaitTime = 0,
                QueuedExecutions = new List<ExecutionListDto>()
            };
        }

        public async Task<ExecutionDto> ScheduleExecutionAsync(string programId, ExecutionScheduleRequestDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Get version to schedule
            string versionId;
            if (!string.IsNullOrEmpty(program.CurrentVersion))
            {
                versionId = program.CurrentVersion;
            }
            else
            {
                var latestVersion = await _versionService.GetLatestVersionForProgramAsync(programId, cancellationToken);
                versionId = latestVersion.Id;
            }

            var versionObjectId = ParseObjectId(versionId);

            // Create scheduled execution
            var execution = new ExecutionModel
            {
                ProgramId = programObjectId,
                VersionId = versionObjectId,
                UserId = "system", // Should come from current user context BaseController holds CurrentUserId property
                ExecutionType = "scheduled_execution",
                StartedAt = dto.ScheduledTime,
                Status = "scheduled",
                Parameters = dto.Parameters,
                Results = new ExecutionResults(),
                ResourceUsage = new ResourceUsage()
            };

            var createdExecution = await _unitOfWork.Executions.CreateAsync(execution, cancellationToken);

            _logger.LogInformation("Scheduled execution {ExecutionId} for program {ProgramId} at {ScheduledTime}",
                createdExecution._ID, programId, dto.ScheduledTime);

            return _mapper.Map<ExecutionDto>(createdExecution);
        }

        public async Task<bool> CancelScheduledExecutionAsync(string executionId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(executionId);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {executionId} not found.");
            }

            if (execution.Status != "scheduled")
            {
                throw new InvalidOperationException("Can only cancel scheduled executions.");
            }

            var success = await _unitOfWork.Executions.UpdateStatusAsync(objectId, "cancelled", cancellationToken);

            if (success)
            {
                _logger.LogInformation("Cancelled scheduled execution {ExecutionId}", executionId);
            }

            return success;
        }

        public async Task<List<ExecutionListDto>> GetScheduledExecutionsAsync(CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.Executions.GetByStatusAsync("scheduled", cancellationToken);
            return await MapExecutionListDtosAsync(executions.ToList(), cancellationToken);
        }

        #endregion

        #region Execution Cleanup and Maintenance

        public async Task<int> CleanupOldExecutionsAsync(int daysToKeep = 30, CancellationToken cancellationToken = default)
        {
            var cleanedCount = await _unitOfWork.Executions.CleanupOldExecutionsAsync(daysToKeep, cancellationToken);

            _logger.LogInformation("Cleaned up {CleanedCount} old executions (keeping {DaysToKeep} days)",
                cleanedCount, daysToKeep);

            return cleanedCount;
        }

        public async Task<bool> ArchiveExecutionAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            if (execution.Status == "running" || execution.Status == "paused")
            {
                throw new InvalidOperationException("Cannot archive a running or paused execution.");
            }

            // In a real implementation, this would move execution data to archive storage
            _logger.LogInformation("Archived execution {ExecutionId}", id);
            return true;
        }

        public async Task<List<ExecutionCleanupReportDto>> GetCleanupReportAsync(CancellationToken cancellationToken = default)
        {
            // Generate a cleanup report showing what could be cleaned up
            var allExecutions = await _unitOfWork.Executions.GetAllAsync(cancellationToken);
            var oldExecutions = allExecutions.Where(e => e.StartedAt < DateTime.UtcNow.AddDays(-30)).ToList();

            var report = new List<ExecutionCleanupReportDto>
            {
                new ExecutionCleanupReportDto
                {
                    CleanupDate = DateTime.UtcNow,
                    ExecutionsRemoved = oldExecutions.Count,
                    SpaceFreed = oldExecutions.Sum(e => e.ResourceUsage.DiskUsed),
                    DaysRetained = 30,
                    RemovedByStatus = oldExecutions.GroupBy(e => e.Status).ToDictionary(g => g.Key, g => g.Count())
                }
            };

            return report;
        }

        #endregion

        #region Execution Security and Validation

        public async Task<bool> ValidateExecutionPermissionsAsync(string programId, string userId, CancellationToken cancellationToken = default)
        {
            // Check direct user permissions first
            var hasUserPermission = await _programService.ValidateUserAccessAsync(programId, userId, "Execute", cancellationToken) ||
                                   await _programService.ValidateUserAccessAsync(programId, userId, "admin", cancellationToken) ||
                                   await _programService.ValidateUserAccessAsync(programId, userId, "Write", cancellationToken);

            if (hasUserPermission)
                return true;

            // Check group-based permissions
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program?.Permissions?.Groups == null || !program.Permissions.Groups.Any())
                return false;

            // Check if user belongs to any group with execution permissions
            foreach (var groupPermission in program.Permissions.Groups)
            {
                if (IsExecutionPermission(groupPermission.AccessLevel))
                {
                    var isMember = await _groupService.IsMemberAsync(groupPermission.GroupId, userId, cancellationToken);
                    if (isMember)
                        return true;
                }
            }

            return false;
        }

        private static bool IsExecutionPermission(string accessLevel)
        {
            return string.Equals(accessLevel, "Execute", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(accessLevel, "admin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(accessLevel, "Write", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ExecutionSecurityScanResult> RunSecurityScanAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Simple security scan implementation
            var issues = new List<string>();
            var warnings = new List<string>();
            var riskLevel = 1;

            try
            {
                // Get current version files for security scan
                string? versionId = program.CurrentVersion;
                if (string.IsNullOrEmpty(versionId))
                {
                    var latestVersion = await _versionService.GetLatestVersionForProgramAsync(programId, cancellationToken);
                    versionId = latestVersion.Id;
                }

                // Check program files for security issues using IFileStorageService
                var files = await _fileStorageService.ListVersionFilesAsync(programId, versionId!, cancellationToken);

                foreach (var file in files)
                {
                    var extension = Path.GetExtension(file.Path).ToLowerInvariant();

                    // Check for potentially dangerous file types
                    if (new[] { ".exe", ".bat", ".cmd", ".ps1", ".sh" }.Contains(extension))
                    {
                        issues.Add($"Executable file detected: {file.Path}");
                        riskLevel = Math.Max(riskLevel, 4);
                    }

                    // Check file size
                    if (file.Size > 100 * 1024 * 1024) // 100MB
                    {
                        warnings.Add($"Large file detected: {file.Path} ({file.Size / 1024 / 1024} MB)");
                        riskLevel = Math.Max(riskLevel, 2);
                    }
                }

                // Check program configuration
                if (program.UiType == "web" && program.DeploymentInfo?.DeploymentType == AppDeploymentType.DockerContainer)
                {
                    warnings.Add("Container deployment may require additional security review");
                    riskLevel = Math.Max(riskLevel, 3);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to complete security scan for program {ProgramId}", programId);
                issues.Add("Security scan could not be completed");
                riskLevel = 3;
            }

            return new ExecutionSecurityScanResult
            {
                IsSecure = !issues.Any(),
                SecurityIssues = issues,
                SecurityWarnings = warnings,
                RiskLevel = riskLevel,
                ScanDate = DateTime.UtcNow
            };
        }

        public async Task<bool> IsExecutionAllowedAsync(string programId, string userId, CancellationToken cancellationToken = default)
        {
            // Check user permissions
            var hasPermission = await ValidateExecutionPermissionsAsync(programId, userId, cancellationToken);
            if (!hasPermission)
            {
                return false;
            }

            // Check if user has reached execution limits
            var userExecutions = await _unitOfWork.Executions.GetByUserIdAsync(userId, cancellationToken);
            var runningExecutions = userExecutions.Count(e => e.Status == "running");

            if (runningExecutions >= _settings.MaxConcurrentExecutionsPerUser)
            {
                return false;
            }

            // Check program-specific limits
            var programExecutions = await _unitOfWork.Executions.GetByProgramIdAsync(ParseObjectId(programId), cancellationToken);
            var programRunningExecutions = programExecutions.Count(e => e.Status == "running");

            if (programRunningExecutions >= _settings.MaxConcurrentExecutionsPerProgram)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Project Validation and Analysis (Using IFileStorageService)

        public async Task<ProjectValidationResultDto> ValidateProjectAsync(string programId, string? versionId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // If no versionId provided, use current version
                if (string.IsNullOrEmpty(versionId))
                {
                    var program = await _unitOfWork.Programs.GetByIdAsync(ParseObjectId(programId), cancellationToken);
                    if (program == null)
                    {
                        throw new KeyNotFoundException($"Program with ID {programId} not found");
                    }

                    if (string.IsNullOrEmpty(program.CurrentVersion))
                    {
                        var latestVersion = await _versionService.GetLatestVersionForProgramAsync(programId, cancellationToken);
                        versionId = latestVersion.Id;
                    }
                    else
                    {
                        versionId = program.CurrentVersion;
                    }
                }

                // Validate using project execution engine
                var validation = await _projectExecutionEngine.ValidateProjectAsync(programId, versionId, cancellationToken);

                return new ProjectValidationResultDto
                {
                    IsValid = validation.IsValid,
                    Errors = validation.Errors,
                    Warnings = validation.Warnings,
                    Suggestions = validation.Suggestions,
                    SecurityScan = validation.SecurityScan != null ? new ProjectSecurityScanDto
                    {
                        HasSecurityIssues = validation.SecurityScan.HasSecurityIssues,
                        Issues = validation.SecurityScan.Issues.Select(i => new SecurityIssueDto
                        {
                            Type = i.Type,
                            Description = i.Description,
                            File = i.File,
                            Line = i.Line,
                            Severity = i.Severity
                        }).ToList(),
                        RiskLevel = validation.SecurityScan.RiskLevel
                    } : null,
                    Complexity = validation.Complexity != null ? new ProjectComplexityDto
                    {
                        TotalFiles = validation.Complexity.TotalFiles,
                        TotalLines = validation.Complexity.TotalLines,
                        Dependencies = validation.Complexity.Dependencies,
                        ComplexityLevel = validation.Complexity.ComplexityLevel,
                        ComplexityScore = validation.Complexity.ComplexityScore
                    } : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Project validation failed for program {ProgramId}", programId);
                return new ProjectValidationResultDto
                {
                    IsValid = false,
                    Errors = { $"Validation failed: {ex.Message}" }
                };
            }
        }

        public async Task<ProjectStructureAnalysisDto> AnalyzeProjectStructureAsync(string programId, string? versionId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // If no versionId provided, use current version
                if (string.IsNullOrEmpty(versionId))
                {
                    var program = await _unitOfWork.Programs.GetByIdAsync(ParseObjectId(programId), cancellationToken);
                    if (program == null)
                    {
                        throw new KeyNotFoundException($"Program with ID {programId} not found");
                    }

                    if (string.IsNullOrEmpty(program.CurrentVersion))
                    {
                        var latestVersion = await _versionService.GetLatestVersionForProgramAsync(programId, cancellationToken);
                        versionId = latestVersion.Id;
                    }
                    else
                    {
                        versionId = program.CurrentVersion;
                    }
                }

                var analysis = await _projectExecutionEngine.AnalyzeProjectStructureAsync(programId, versionId, cancellationToken);

                return new ProjectStructureAnalysisDto
                {
                    Language = analysis.Language,
                    ProjectType = analysis.ProjectType,
                    EntryPoints = analysis.EntryPoints,
                    ConfigFiles = analysis.ConfigFiles,
                    SourceFiles = analysis.SourceFiles,
                    Dependencies = analysis.Dependencies,
                    HasBuildFile = analysis.HasBuildFile,
                    MainEntryPoint = analysis.MainEntryPoint,
                    Files = analysis.Files.Select(f => new ProjectFileDto
                    {
                        Path = f.Path,
                        Type = f.Type,
                        Size = f.Size,
                        Extension = f.Extension,
                        IsEntryPoint = f.IsEntryPoint,
                        LineCount = f.LineCount
                    }).ToList(),
                    Complexity = new ProjectComplexityDto
                    {
                        TotalFiles = analysis.Complexity.TotalFiles,
                        TotalLines = analysis.Complexity.TotalLines,
                        Dependencies = analysis.Complexity.Dependencies,
                        ComplexityLevel = analysis.Complexity.ComplexityLevel,
                        ComplexityScore = analysis.Complexity.ComplexityScore
                    },
                    Metadata = analysis.Metadata
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Project structure analysis failed for program {ProgramId}", programId);
                throw;
            }
        }

        public async Task<List<string>> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default)
        {
            return await _projectExecutionEngine.GetSupportedLanguagesAsync(cancellationToken);
        }

        #endregion

        #region Private Helper Methods

        private async Task<List<ExecutionListDto>> MapExecutionListDtosAsync(List<ExecutionModel> executions, CancellationToken cancellationToken)
        {
            var dtos = new List<ExecutionListDto>();

            foreach (var execution in executions)
            {
                var dto = _mapper.Map<ExecutionListDto>(execution);

                // Get program name
                try
                {
                    var program = await _unitOfWork.Programs.GetByIdAsync(execution.ProgramId, cancellationToken);
                    if (program != null)
                    {
                        dto.ProgramName = program.Name;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get program name for execution {ExecutionId}", execution._ID);
                    dto.ProgramName = "Unknown";
                }

                // Get user name
                try
                {
                    var user = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(execution.UserId), cancellationToken);
                    if (user != null)
                    {
                        dto.UserName = user.FullName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get user name for execution {ExecutionId}", execution._ID);
                    dto.UserName = "Unknown";
                }

                // Get version number
                try
                {
                    var version = await _unitOfWork.Versions.GetByIdAsync(execution.VersionId, cancellationToken);
                    if (version != null)
                    {
                        dto.VersionNumber = version.VersionNumber;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get version number for execution {ExecutionId}", execution._ID);
                }

                // Calculate duration
                if (execution.CompletedAt.HasValue)
                {
                    dto.Duration = (execution.CompletedAt.Value - execution.StartedAt).TotalMinutes;
                }

                // Set error flag
                dto.HasError = execution.Status == "failed" ||
                              (execution.Status == "completed" && execution.Results.ExitCode != 0) ||
                              !string.IsNullOrEmpty(execution.Results.Error);

                dtos.Add(dto);
            }

            return dtos;
        }

        private async Task<PagedResponse<ExecutionListDto>> CreatePagedExecutionResponse(IEnumerable<ExecutionModel> executions, PaginationRequestDto pagination, CancellationToken cancellationToken)
        {
            // This method is now deprecated in favor of Specification Pattern
            // Convert the enumerable to list and manually apply pagination for backward compatibility
            var executionsList = executions.ToList();
            var totalCount = executionsList.Count;
            var paginatedExecutions = executionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = await MapExecutionListDtosAsync(paginatedExecutions, cancellationToken);
            return new PagedResponse<ExecutionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        private async Task ValidateExecutionLimitsAsync(string programId, string userId, CancellationToken cancellationToken)
        {
            if (!await IsExecutionAllowedAsync(programId, userId, cancellationToken))
            {
                throw new InvalidOperationException("Execution not allowed due to limits or permissions");
            }
        }

        private async Task ExecuteProjectInBackgroundAsync(ExecutionModel execution, ProgramExecutionRequestDto dto, CancellationToken cancellationToken)
        {
            var executionId = execution._ID.ToString();
            var context = new ExecutionContext
            {
                ExecutionId = executionId,
                CancellationTokenSource = new CancellationTokenSource()
            };

            _activeExecutions[executionId] = context;

            try
            {
                _logger.LogInformation("Starting project execution for {ExecutionId}", executionId);

                // Create project execution request with proper file paths
                var projectRequest = new ProjectExecutionRequest
                {
                    ProgramId = execution.ProgramId.ToString(),
                    VersionId = execution.VersionId.ToString(),
                    UserId = execution.UserId,
                    Parameters = dto.Parameters,
                    Environment = dto.Environment,
                    ResourceLimits = MapToProjectResourceLimits(dto.ResourceLimits),
                    SaveResults = dto.SaveResults,
                    TimeoutMinutes = dto.TimeoutMinutes,
                    CleanupOnCompletion = true
                };

                // Execute using project execution engine
                var result = await _projectExecutionEngine.ExecuteProjectAsync(projectRequest, context.CancellationTokenSource.Token);

                // Update execution record with results
                await UpdateExecutionWithProjectResultAsync(execution, result, cancellationToken);

                _logger.LogInformation("Completed project execution for {ExecutionId}", executionId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Project execution {ExecutionId} was cancelled", executionId);
                await _unitOfWork.Executions.UpdateStatusAsync(execution._ID, "stopped", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Project execution failed for {ExecutionId}", executionId);

                await _unitOfWork.Executions.CompleteExecutionAsync(
                    execution._ID,
                    -1,
                    "Project execution failed",
                    new List<string>(),
                    ex.Message,
                    cancellationToken);
            }
            finally
            {
                _activeExecutions.Remove(executionId);
            }
        }

        private async Task UpdateExecutionWithProjectResultAsync(ExecutionModel execution,
            DTOs.Response.Execution.ProjectExecutionResult result,
            CancellationToken cancellationToken)
        {
            // Map project execution result to database execution
            execution.Results.ExitCode = result.ExitCode;
            execution.Results.Output = result.Output;
            execution.Results.Error = result.ErrorOutput;
            execution.Status = result.Success ? "completed" : "failed";
            execution.CompletedAt = result.CompletedAt;

            // Map resource usage
            execution.ResourceUsage.CpuTime = result.ResourceUsage.CpuTimeSeconds;
            execution.ResourceUsage.MemoryUsed = result.ResourceUsage.PeakMemoryBytes;
            execution.ResourceUsage.DiskUsed = result.ResourceUsage.DiskSpaceUsedBytes;

            // Store output files if any (these would be stored in execution output storage)
            if (result.OutputFiles.Any())
            {
                execution.Results.OutputFiles = result.OutputFiles;
            }

            await _unitOfWork.Executions.UpdateAsync(execution._ID, execution, cancellationToken);
        }

        private ProjectResourceLimits? MapToProjectResourceLimits(ExecutionResourceLimitsDto? dto)
        {
            if (dto == null) return null;

            return new ProjectResourceLimits
            {
                MaxCpuPercentage = dto.MaxCpuPercentage,
                MaxMemoryMb = dto.MaxMemoryMb,
                MaxDiskMb = dto.MaxDiskMb,
                MaxExecutionTimeMinutes = dto.MaxExecutionTimeMinutes,
                MaxProcesses = dto.MaxConcurrentExecutions,
                MaxOutputSizeBytes = 100 * 1024 * 1024 // 100MB default
            };
        }

        private object ConvertJsonElementToBson(object parameters)
        {
            if (parameters == null) return null;

            // If it's already a simple type or Dictionary, return as-is
            if (parameters is string || parameters is int || parameters is double || parameters is bool || parameters is Dictionary<string, object>)
            {
                return parameters;
            }

            // Serialize to JSON string then deserialize with proper type handling
            var json = System.Text.Json.JsonSerializer.Serialize(parameters);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            // Use JsonDocument to properly handle nested objects and convert JsonElement to proper types
            using var document = JsonDocument.Parse(json);
            return ConvertJsonElementToObject(document.RootElement);
        }

        private object ConvertJsonElementToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        return intValue;
                    if (element.TryGetInt64(out long longValue))
                        return longValue;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Object:
                    var dictionary = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dictionary[property.Name] = ConvertJsonElementToObject(property.Value);
                    }
                    return dictionary;
                case JsonValueKind.Array:
                    var array = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        array.Add(ConvertJsonElementToObject(item));
                    }
                    return array;
                default:
                    return element.ToString();
            }
        }

        private double? CalculateExecutionProgress(ExecutionModel execution)
        {
            if (execution.Status == "completed" || execution.Status == "failed")
            {
                return 100.0;
            }

            if (execution.Status == "running")
            {
                // Simple progress calculation based on elapsed time vs expected time
                var elapsed = DateTime.UtcNow - execution.StartedAt;
                var expectedDuration = TimeSpan.FromMinutes(_settings.DefaultMaxExecutionTimeMinutes);
                return Math.Min(95.0, (elapsed.TotalMinutes / expectedDuration.TotalMinutes) * 100);
            }

            return null;
        }

        private string? GetCurrentExecutionStep(ExecutionModel execution)
        {
            return execution.Status switch
            {
                "running" => "Executing program logic",
                "completed" => "Execution completed",
                "failed" => "Execution failed",
                "stopped" => "Execution stopped",
                "paused" => "Execution paused",
                "scheduled" => "Waiting for scheduled time",
                _ => null
            };
        }

        private string GetStatusMessage(ExecutionModel execution)
        {
            return execution.Status switch
            {
                "running" => "Execution is currently running",
                "completed" => execution.Results.ExitCode == 0 ? "Execution completed successfully" : "Execution completed with errors",
                "failed" => "Execution failed",
                "stopped" => "Execution was stopped by user",
                "paused" => "Execution is paused",
                "scheduled" => $"Execution scheduled for {execution.StartedAt:yyyy-MM-dd HH:mm:ss}",
                _ => "Unknown status"
            };
        }

        #endregion

        #region Supporting Classes

        private class ExecutionContext
        {
            public string ExecutionId { get; set; } = string.Empty;
            public CancellationTokenSource CancellationTokenSource { get; set; } = new();
            public Process? Process { get; set; }
        }

        public class ExecutionSettings
        {
            public int DefaultMaxCpuPercentage { get; set; } = 80;
            public long DefaultMaxMemoryMb { get; set; } = 1024;
            public long DefaultMaxDiskMb { get; set; } = 2048;
            public int DefaultMaxExecutionTimeMinutes { get; set; } = 30;
            public int DefaultMaxConcurrentExecutions { get; set; } = 5;
            public int MaxConcurrentExecutions { get; set; } = 20;
            public int MaxConcurrentExecutionsPerUser { get; set; } = 3;
            public int MaxConcurrentExecutionsPerProgram { get; set; } = 10;
            public long MaxAllowedMemoryMb { get; set; } = 4096;
            public int MaxAllowedExecutionTimeMinutes { get; set; } = 120;
        }

        #endregion
    }
}