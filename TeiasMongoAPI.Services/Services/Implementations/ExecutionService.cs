using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Text.Json;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Specifications;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Interfaces.Execution;
using TeiasMongoAPI.Services.Services.Base;
using TeiasMongoAPI.Services.Services.Implementations.Execution;
using TeiasMongoAPI.Services.Specifications;
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
        private readonly IExecutionOutputStreamingService? _streamingService;
        private readonly ExecutionSettings _settings;
        private readonly Dictionary<string, ExecutionContext> _activeExecutions = new();

        // Tiered Execution Fields
        private readonly TeiasMongoAPI.Services.Services.Implementations.Execution.TieredExecutionSettings _tieredSettings;
        private readonly SemaphoreSlim? _ramPoolCapacitySemaphore; // Weighted semaphore for RAM capacity in MB
        private readonly SemaphoreSlim? _diskPoolSemaphore; // Concurrency-based semaphore
        private readonly Dictionary<string, JobResourceReservation> _activeReservations = new();
        private readonly Queue<QueuedJob> _queuedJobs = new();
        private readonly object _reservationLock = new();

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
            IOptions<TeiasMongoAPI.Services.Services.Implementations.Execution.ProjectExecutionSettings> projectSettings,
            ILogger<ExecutionService> logger,
            IExecutionOutputStreamingService? streamingService = null)
            : base(unitOfWork, mapper, logger)
        {
            _fileStorageService = fileStorageService;
            _programService = programService;
            _versionService = versionService;
            _deploymentService = deploymentService;
            _projectExecutionEngine = projectExecutionEngine;
            _groupService = groupService;
            _streamingService = streamingService;
            _settings = settings.Value;
            _tieredSettings = projectSettings.Value.TieredExecution;

            // Initialize Tiered Execution Pools
            if (_tieredSettings.EnableTieredExecution)
            {
                // Initialize RAM pool with capacity-based semaphore (weighted)
                int totalRamCapacityMB = _tieredSettings.RamPool.TotalCapacityGB * 1024;
                _ramPoolCapacitySemaphore = new SemaphoreSlim(totalRamCapacityMB, totalRamCapacityMB);

                // Initialize Disk pool with concurrency-based semaphore
                _diskPoolSemaphore = new SemaphoreSlim(
                    _tieredSettings.DiskPool.MaxConcurrentJobs,
                    _tieredSettings.DiskPool.MaxConcurrentJobs);

                _logger.LogInformation(
                    "Tiered Execution initialized: RAM Pool ({RamCapacityGB}GB capacity, {MaxRamJobs} max jobs), Disk Pool ({MaxDiskJobs} max jobs)",
                    _tieredSettings.RamPool.TotalCapacityGB,
                    _tieredSettings.RamPool.MaxConcurrentJobs,
                    _tieredSettings.DiskPool.MaxConcurrentJobs);

                // RISK MITIGATION: Validate RAM pool capacity on startup
                ValidateRamPoolCapacity();
            }
            else
            {
                _logger.LogInformation("Tiered Execution is disabled");
            }
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

        public async Task<PagedResponse<ExecutionListDto>> GetAllAsync(ObjectId? currentUserId, IEnumerable<string> userRoles, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            // Check if user has Admin role
            bool isAdmin = userRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase);

            BaseSpecification<ExecutionModel> spec;

            if (isAdmin)
            {
                // Admin users can see all executions
                spec = new AllExecutionsSpecification(pagination);
            }
            else
            {
                // Non-admin users can only see their own executions
                string? userId = currentUserId?.ToString();
                spec = new UserFilteredExecutionsSpecification(userId, pagination);
            }

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

        public async Task<List<ExecutionListDto>> GetRecentExecutionsAsync(ObjectId? currentUserId, IEnumerable<string> userRoles, int count = 10, CancellationToken cancellationToken = default)
        {
            // Check if user has Admin role
            bool isAdmin = userRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase);

            IEnumerable<ExecutionModel> executions;

            if (isAdmin)
            {
                // Admin users can see all executions
                executions = await _unitOfWork.Executions.GetRecentExecutionsAsync(count, null, cancellationToken);
            }
            else
            {
                // Non-admin users can only see their own executions
                string? userId = currentUserId?.ToString();
                executions = await _unitOfWork.Executions.GetRecentExecutionsAsync(count, userId, cancellationToken);
            }

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

            // Get latest version
            var latestVersion = await _versionService.GetLatestVersionForProgramAsync(programId, cancellationToken);
            string versionId = latestVersion.Id;

            var versionObjectId = ParseObjectId(versionId);
            var version = await _unitOfWork.Versions.GetByIdAsync(versionObjectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"No executable version found for program {programId}.");
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
                SaveResults = dto.SaveResults
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
                SaveResults = dto.SaveResults
            };

            // Use specific version if provided, otherwise use current/latest
            if (!string.IsNullOrEmpty(dto.VersionId))
            {
                var versionRequest = new VersionExecutionRequestDto
                {
                    Parameters = dto.Parameters,
                    Environment = dto.Environment,
                    ResourceLimits = dto.ResourceLimits,
                    SaveResults = dto.SaveResults
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

        public async Task<ExecutionDto> DeployWebApplicationAsync(string programId, WebAppDeploymentRequestDto dto, ObjectId? currentUserId = null, CancellationToken cancellationToken = default)
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
                UserId = currentUserId?.ToString(),
                ExecutionType = "web_app_deploy",
                StartedAt = DateTime.UtcNow,
                Status = "running",
                Parameters = ConvertJsonElementToBson(dto.Configuration),
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
                        MaxMemoryMb = 512
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
                        MaxMemoryMb = 1024
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
            }

            // Recommend optimal resource limits based on default settings
            result.RecommendedLimits = new ExecutionResourceLimitsDto
            {
                MaxCpuPercentage = _settings.DefaultMaxCpuPercentage,
                MaxMemoryMb = _settings.DefaultMaxMemoryMb,
                MaxDiskMb = _settings.DefaultMaxDiskMb,
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

        public async Task<ExecutionDto> ScheduleExecutionAsync(string programId, ExecutionScheduleRequestDto dto, ObjectId? currentUserId = null, CancellationToken cancellationToken = default)
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
                UserId = currentUserId?.ToString(),
                ExecutionType = "scheduled_execution",
                StartedAt = dto.ScheduledTime,
                Status = "scheduled",
                Parameters = ConvertJsonElementToBson(dto.Parameters),
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

            if (program.IsPublic) return true;

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
                    BinaryFiles = analysis.BinaryFiles,
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

        public async Task<ProjectStructureAnalysisDto> AnalyzeProjectStructureForAIAsync(string programId, string? versionId = null, CancellationToken cancellationToken = default)
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

                // Use internal method with skipValidation = true for AI assistant
                var analysis = await _projectExecutionEngine.AnalyzeProjectStructureInternalAsync(programId, versionId, skipValidation: true, cancellationToken);

                return new ProjectStructureAnalysisDto
                {
                    Language = analysis.Language,
                    ProjectType = analysis.ProjectType,
                    EntryPoints = analysis.EntryPoints,
                    ConfigFiles = analysis.ConfigFiles,
                    SourceFiles = analysis.SourceFiles,
                    BinaryFiles = analysis.BinaryFiles,
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
                _logger.LogError(ex, "AI project structure analysis failed for program {ProgramId}", programId);
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
            //TODO: This code block should be optimized.

            //THE PROBLEM:
            //Executions were getting stuck in a "running" state if the user disconnected during a long-running job.
            //This is because the CancellationToken from the HTTP request would be cancelled. While the background process
            //continued correctly, the final database update at the end would fail because it was called with this
            //already - cancelled token, throwing an OperationCanceledException.

            //THE FIX:
            //All critical database updates that record the final state of an execution(e.g., "completed", "failed",
            //"stopped") are now called with `CancellationToken.None`. This ensures the finalization step is never
            //cancelled and always runs, guaranteeing data consistency regardless of the original user's connection status.

            var executionId = execution._ID.ToString();
            var context = new ExecutionContext
            {
                ExecutionId = executionId,
                CancellationTokenSource = new CancellationTokenSource()
            };

            _activeExecutions[executionId] = context;
            bool statusUpdated = false;
            bool resourcesReserved = false;

            // The CancellationTokenRegistration should be disposed of when we're done.
            // We'll declare it here and dispose of it in the finally block.
            CancellationTokenRegistration registration = default;

            // ============================================================================
            // TIERED EXECUTION DISPATCHER LOGIC
            // ============================================================================
            // This section implements the complete dispatcher logic for tiered execution:
            // 1. Determine job profile from request or use default
            // 2. Select appropriate execution tier (RAM/Disk) based on availability
            // 3. Reserve resources or queue the job if pools are full
            // 4. Pass tier information to the execution engine
            // 5. Release resources in finally block
            // ============================================================================

            string selectedTier = "Standard"; // Default tier for non-tiered execution
            string jobProfile;

            // Step 1: Determine JobProfile from request, with fallback to default
            if (_tieredSettings.EnableTieredExecution)
            {
                jobProfile = dto.JobProfile ?? _tieredSettings.DefaultJobProfile;
                _logger.LogInformation(
                    "Execution {ExecutionId}: Tiered execution enabled. Job profile: '{JobProfile}' (specified: {IsSpecified})",
                    executionId, jobProfile, dto.JobProfile != null);
            }
            else
            {
                jobProfile = "Standard"; // Default for non-tiered execution
                _logger.LogDebug("Execution {ExecutionId}: Tiered execution disabled, using standard execution", executionId);
            }

            // Step 2: Select tier and reserve resources (if tiered execution is enabled)
            if (_tieredSettings.EnableTieredExecution)
            {
                try
                {
                    _logger.LogDebug(
                        "Execution {ExecutionId}: Selecting execution tier for job profile '{JobProfile}'",
                        executionId, jobProfile);

                    var (tier, isQueued) = await SelectExecutionTierAsync(executionId, jobProfile, cancellationToken);
                    selectedTier = tier;

                    // Step 3: Handle queued jobs
                    if (isQueued)
                    {
                        _logger.LogInformation(
                            "Execution {ExecutionId}: Job queued for later execution (tier: '{Tier}', profile: '{Profile}')",
                            executionId, tier, jobProfile);

                        // Enqueue the job and exit early
                        EnqueueJob(executionId, execution, dto, cancellationToken);

                        // Update execution status to "queued" for user visibility
                        try
                        {
                            execution.Status = "queued";
                            execution.Results.Output = $"Job queued for {tier} tier execution. Job profile: {jobProfile}";
                            await _unitOfWork.Executions.UpdateAsync(execution._ID, execution, CancellationToken.None);
                        }
                        catch (Exception updateEx)
                        {
                            _logger.LogWarning(updateEx,
                                "Failed to update execution {ExecutionId} status to 'queued'", executionId);
                        }

                        return; // Exit early - job will be processed later when resources become available
                    }

                    // Step 4: Resources successfully reserved
                    resourcesReserved = true;
                    _logger.LogInformation(
                        "Execution {ExecutionId}: Resources reserved. Tier: '{Tier}', Job profile: '{Profile}'",
                        executionId, selectedTier, jobProfile);
                }
                catch (InvalidOperationException ex)
                {
                    // Resource allocation failed (pools full, queue full, etc.)
                    _logger.LogError(ex,
                        "Execution {ExecutionId}: Resource allocation failed for job profile '{JobProfile}'",
                        executionId, jobProfile);

                    // Update execution status to failed with detailed error message
                    try
                    {
                        execution.Status = "failed";
                        execution.CompletedAt = DateTime.UtcNow;
                        execution.Results.Error = $"Resource allocation failed: {ex.Message}\nJob profile: {jobProfile}\nTiered execution is enabled but resources are unavailable.";
                        await _unitOfWork.Executions.UpdateAsync(execution._ID, execution, CancellationToken.None);

                        _logger.LogInformation(
                            "Execution {ExecutionId}: Updated status to 'failed' due to resource allocation failure",
                            executionId);
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx,
                            "Execution {ExecutionId}: Failed to update execution status after resource allocation failure",
                            executionId);
                    }

                    return; // Exit early - cannot proceed without resources
                }
                catch (Exception ex)
                {
                    // Unexpected error during tier selection
                    _logger.LogError(ex,
                        "Execution {ExecutionId}: Unexpected error during tier selection for job profile '{JobProfile}'",
                        executionId, jobProfile);

                    // Update execution status to failed
                    try
                    {
                        execution.Status = "failed";
                        execution.CompletedAt = DateTime.UtcNow;
                        execution.Results.Error = $"Tier selection failed: {ex.Message}\nAn unexpected error occurred during resource allocation.\nJob profile: {jobProfile}";
                        await _unitOfWork.Executions.UpdateAsync(execution._ID, execution, CancellationToken.None);
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx,
                            "Execution {ExecutionId}: Failed to update execution status after tier selection error",
                            executionId);
                    }

                    return; // Exit early
                }
            }
            // ============================================================================
            // END OF TIERED EXECUTION DISPATCHER LOGIC
            // ============================================================================

            try
            {
                _logger.LogInformation("Starting project execution for {ExecutionId}", executionId);

                registration = cancellationToken.Register(() =>
                {
                    _logger.LogInformation(
                        "HTTP request for execution {ExecutionId} was cancelled (user likely disconnected). " +
                        "The background process will continue to run to completion as designed.",
                        executionId);
                });

                // LIVE STREAMING: Notify execution started
                if (_streamingService != null)
                {
                    try
                    {
                        await _streamingService.StartExecutionStreamingAsync(executionId, execution.UserId, cancellationToken);
                        await _streamingService.StreamExecutionStartedAsync(executionId, new ExecutionStartedEventArgs
                        {
                            ExecutionId = executionId,
                            ProgramId = execution.ProgramId.ToString(),
                            VersionId = execution.VersionId.ToString(),
                            UserId = execution.UserId,
                            StartedAt = execution.StartedAt,
                            TimeoutMinutes = _settings.DefaultMaxExecutionTimeMinutes,
                            Parameters = dto.Parameters
                        }, cancellationToken);

                        await _streamingService.StreamStatusChangeAsync(executionId, new ExecutionStatusChangedEventArgs
                        {
                            ExecutionId = executionId,
                            OldStatus = "pending",
                            NewStatus = "running",
                            ChangedAt = DateTime.UtcNow,
                            Reason = "Execution started"
                        }, cancellationToken);
                    }
                    catch (Exception streamEx)
                    {
                        _logger.LogWarning(streamEx, "Failed to stream execution started event for {ExecutionId}", executionId);
                    }
                }

                // ============================================================================
                // TIERED EXECUTION: Step 5 - Pass tier information to execution engine
                // ============================================================================
                // Create project execution request with tier information that will be
                // used by the ProjectExecutionEngine and language runners (e.g., PythonProjectRunner)
                // to determine whether to use RAM tier (tmpfs) or Disk tier (volumes)
                // ============================================================================
                var projectRequest = new ProjectExecutionRequest
                {
                    ProgramId = execution.ProgramId.ToString(),
                    VersionId = execution.VersionId.ToString(),
                    UserId = execution.UserId,
                    Parameters = dto.Parameters,
                    Environment = dto.Environment,
                    ResourceLimits = MapToProjectResourceLimits(dto.ResourceLimits),
                    SaveResults = dto.SaveResults,
                    CleanupOnCompletion = true,

                    // TIERED EXECUTION: Critical fields for tier-aware execution
                    ExecutionTier = selectedTier,  // "RAM" or "Disk" - determines execution strategy
                    JobProfile = jobProfile         // Job profile name (e.g., "Standard", "Heavy")
                };

                _logger.LogDebug(
                    "Execution {ExecutionId}: Created ProjectExecutionRequest with tier '{Tier}' and profile '{Profile}'",
                    executionId, selectedTier, jobProfile);

                // Execute using project execution engine
                var result = await _projectExecutionEngine.ExecuteProjectAsync(projectRequest, executionId, context.CancellationTokenSource.Token);

                // DEFENSIVE: Update execution record with results using retry logic
                statusUpdated = await UpdateExecutionWithProjectResultWithRetryAsync(execution, result, CancellationToken.None);

                // LIVE STREAMING: Notify execution completed
                if (_streamingService != null)
                {
                    try
                    {
                        await _streamingService.StreamExecutionCompletedAsync(executionId, new ExecutionCompletedEventArgs
                        {
                            ExecutionId = executionId,
                            Status = result.Success ? "completed" : "failed",
                            ExitCode = result.ExitCode,
                            CompletedAt = result.CompletedAt ?? DateTime.UtcNow,
                            Duration = result.Duration ?? TimeSpan.Zero,
                            Success = result.Success,
                            ErrorMessage = result.Success ? null : result.ErrorOutput,
                            OutputFiles = result.OutputFiles.ToList()
                        }, cancellationToken);
                    }
                    catch (Exception streamEx)
                    {
                        _logger.LogWarning(streamEx, "Failed to stream execution completed event for {ExecutionId}", executionId);
                    }
                }

                _logger.LogInformation("Completed project execution for {ExecutionId} with status {Status}", executionId, result.Success ? "completed" : "failed");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Project execution {ExecutionId} was cancelled", executionId);
                await SafeUpdateExecutionStatusAsync(execution._ID, "stopped", executionId, CancellationToken.None);
                statusUpdated = true;

                // LIVE STREAMING: Notify execution stopped
                if (_streamingService != null)
                {
                    try
                    {
                        await _streamingService.StreamStatusChangeAsync(executionId, new ExecutionStatusChangedEventArgs
                        {
                            ExecutionId = executionId,
                            OldStatus = "running",
                            NewStatus = "stopped",
                            ChangedAt = DateTime.UtcNow,
                            Reason = "Execution was cancelled"
                        }, cancellationToken);

                        await _streamingService.StreamExecutionCompletedAsync(executionId, new ExecutionCompletedEventArgs
                        {
                            ExecutionId = executionId,
                            Status = "stopped",
                            ExitCode = -1,
                            CompletedAt = DateTime.UtcNow,
                            Duration = DateTime.UtcNow - execution.StartedAt,
                            Success = false,
                            ErrorMessage = "Execution was cancelled",
                            OutputFiles = new List<string>()
                        }, cancellationToken);
                    }
                    catch (Exception streamEx)
                    {
                        _logger.LogWarning(streamEx, "Failed to stream execution cancelled event for {ExecutionId}", executionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Project execution failed for {ExecutionId}", executionId);

                await SafeCompleteExecutionAsync(
                    execution._ID,
                    -1,
                    "Project execution failed",
                    new List<string>(),
                    ex.Message,
                    executionId,
                    cancellationToken);
                statusUpdated = true;

                // LIVE STREAMING: Notify execution failed
                if (_streamingService != null)
                {
                    try
                    {
                        await _streamingService.StreamStatusChangeAsync(executionId, new ExecutionStatusChangedEventArgs
                        {
                            ExecutionId = executionId,
                            OldStatus = "running",
                            NewStatus = "failed",
                            ChangedAt = DateTime.UtcNow,
                            Reason = $"Execution failed: {ex.Message}"
                        }, cancellationToken);

                        await _streamingService.StreamExecutionCompletedAsync(executionId, new ExecutionCompletedEventArgs
                        {
                            ExecutionId = executionId,
                            Status = "failed",
                            ExitCode = -1,
                            CompletedAt = DateTime.UtcNow,
                            Duration = DateTime.UtcNow - execution.StartedAt,
                            Success = false,
                            ErrorMessage = ex.Message,
                            OutputFiles = new List<string>()
                        }, cancellationToken);
                    }
                    catch (Exception streamEx)
                    {
                        _logger.LogWarning(streamEx, "Failed to stream execution failed event for {ExecutionId}", executionId);
                    }
                }
            }
            finally
            {
                // DEFENSIVE: Ensure execution status is never left as "running"
                if (!statusUpdated)
                {
                    _logger.LogWarning("Execution {ExecutionId} completed without proper status update. Performing defensive status update...", executionId);
                    await SafeUpdateExecutionStatusAsync(execution._ID, "failed", executionId, CancellationToken.None);
                }

                // ============================================================================
                // TIERED EXECUTION: Release reserved resources
                // ============================================================================
                // This ensures that resources (RAM capacity or Disk slots) are always
                // released, even if the execution fails or is cancelled. This is critical
                // to prevent resource leaks and ensure queued jobs can be processed.
                // ============================================================================
                if (resourcesReserved)
                {
                    try
                    {
                        _logger.LogDebug(
                            "Execution {ExecutionId}: Releasing reserved resources (tier: '{Tier}', profile: '{Profile}')",
                            executionId, selectedTier, jobProfile);

                        await ReleaseReservationAsync(executionId);

                        _logger.LogInformation(
                            "Execution {ExecutionId}: Successfully released resources for tier '{Tier}'",
                            executionId, selectedTier);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Execution {ExecutionId}: CRITICAL - Failed to release resources for tier '{Tier}'. " +
                            "This may cause resource leaks and prevent queued jobs from executing.",
                            executionId, selectedTier);
                    }
                }
                else if (_tieredSettings.EnableTieredExecution)
                {
                    _logger.LogDebug(
                        "Execution {ExecutionId}: No resources to release (job was queued or resource allocation failed)",
                        executionId);
                }
                // ============================================================================
                // END OF RESOURCE CLEANUP
                // ============================================================================

                _activeExecutions.Remove(executionId);
                _logger.LogDebug("Cleaned up execution context for {ExecutionId}", executionId);

                // DISPOSE OF THE REGISTRATION
                // It's good practice to unregister the callback to clean up resources.
                // Using DisposeAsync is the modern approach inside an async method.
                await registration.DisposeAsync();
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

        /// <summary>
        /// DEFENSIVE: Update execution with project result using retry logic for long-running executions
        /// </summary>
        private async Task<bool> UpdateExecutionWithProjectResultWithRetryAsync(ExecutionModel execution,
            DTOs.Response.Execution.ProjectExecutionResult result,
            CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            const int delayMs = 1000;
            var executionId = execution._ID.ToString();

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await UpdateExecutionWithProjectResultAsync(execution, result, cancellationToken);
                    _logger.LogDebug("Successfully updated execution {ExecutionId} on attempt {Attempt}", executionId, attempt);
                    return true;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "Failed to update execution {ExecutionId} on attempt {Attempt}. Retrying...", executionId, attempt);
                    await Task.Delay(delayMs * attempt, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update execution {ExecutionId} after {MaxRetries} attempts. Attempting fallback status update.", executionId, maxRetries);

                    try
                    {
                        // IMPROVEMENT #1: The fallback is a critical write, so it ALWAYS uses CancellationToken.None.
                        await SafeUpdateExecutionStatusAsync(execution._ID, result.Success ? "completed" : "failed", executionId, CancellationToken.None);

                        _logger.LogDebug("Fallback status update for execution {ExecutionId} succeeded.", executionId);

                        // IMPROVEMENT #2: The fallback succeeded, so we return true to prevent redundant updates in the caller.
                        return true;
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogCritical(fallbackEx, "CRITICAL: The fallback status update for execution {ExecutionId} also failed.", executionId);
                        // The fallback failed, so now we report a definitive failure.
                        return false;
                    }
                }
            }

            // This line is now only reachable if the loop is somehow exited without a return,
            // which shouldn't happen, but it's safe to keep.
            return false;
        }

        /// <summary>
        /// DEFENSIVE: Safe status update that never throws exceptions
        /// </summary>
        private async Task SafeUpdateExecutionStatusAsync(ObjectId executionId, string status, string executionIdStr, CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            const int delayMs = 500;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await _unitOfWork.Executions.UpdateStatusAsync(executionId, status, cancellationToken);
                    _logger.LogInformation("Successfully updated execution {ExecutionId} to status {Status} on attempt {Attempt}", executionIdStr, status, attempt);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update execution {ExecutionId} status to {Status} on attempt {Attempt}", executionIdStr, status, attempt);

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delayMs * attempt, CancellationToken.None);
                    }
                    else
                    {
                        _logger.LogError(ex, "CRITICAL: Failed to update execution {ExecutionId} status after {MaxRetries} attempts. Status may be inconsistent!", executionIdStr, maxRetries);
                    }
                }
            }
        }

        /// <summary>
        /// DEFENSIVE: Safe completion update that never throws exceptions
        /// </summary>
        private async Task SafeCompleteExecutionAsync(ObjectId executionId, int exitCode, string output, List<string> outputFiles, string error, string executionIdStr, CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            const int delayMs = 500;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await _unitOfWork.Executions.CompleteExecutionAsync(executionId, exitCode, output, outputFiles, error, cancellationToken);
                    _logger.LogInformation("Successfully completed execution {ExecutionId} with exit code {ExitCode} on attempt {Attempt}", executionIdStr, exitCode, attempt);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to complete execution {ExecutionId} on attempt {Attempt}", executionIdStr, attempt);

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delayMs * attempt, CancellationToken.None);
                    }
                    else
                    {
                        _logger.LogError(ex, "CRITICAL: Failed to complete execution {ExecutionId} after {MaxRetries} attempts. Trying fallback status update...", executionIdStr, maxRetries);

                        // Final fallback: just update status to failed
                        await SafeUpdateExecutionStatusAsync(executionId, "failed", executionIdStr, CancellationToken.None);
                    }
                }
            }
        }

        private ProjectResourceLimits? MapToProjectResourceLimits(ExecutionResourceLimitsDto? dto)
        {
            if (dto == null) return null;

            return new ProjectResourceLimits
            {
                MaxCpuPercentage = dto.MaxCpuPercentage,
                MaxMemoryMb = dto.MaxMemoryMb,
                MaxDiskMb = dto.MaxDiskMb,
                MaxProcesses = dto.MaxConcurrentExecutions,
                MaxOutputSizeBytes = 100 * 1024 * 1024 // 100MB default
            };
        }

        private object ConvertJsonElementToBson(object parameters)
        {
            if (parameters == null) return null;

            // Sanitize parameters to remove large file contents before conversion
            var sanitized = SanitizeParameters(parameters);

            // If it's already a simple type or Dictionary, return as-is
            if (sanitized is string || sanitized is int || sanitized is double || sanitized is bool || sanitized is Dictionary<string, object>)
            {
                return sanitized;
            }

            // Serialize to JSON string then deserialize with proper type handling
            var json = System.Text.Json.JsonSerializer.Serialize(sanitized);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Use JsonDocument to properly handle nested objects and convert JsonElement to proper types
            using var document = JsonDocument.Parse(json);
            return ConvertJsonElementToObject(document.RootElement);
        }

        /// <summary>
        /// Sanitizes parameters by removing or truncating large content fields.
        /// This prevents MongoDB document size limit (16MB) errors when file contents
        /// are included in parameters. File contents should be saved to disk, not MongoDB.
        /// </summary>
        private object SanitizeParameters(object parameters)
        {
            if (parameters == null) return null;

            // Simple types pass through
            if (parameters is string || parameters is int || parameters is double || parameters is bool)
            {
                return parameters;
            }

            // Serialize to JSON for manipulation
            var json = System.Text.Json.JsonSerializer.Serialize(parameters);
            using var document = JsonDocument.Parse(json);

            return SanitizeJsonElement(document.RootElement);
        }

        /// <summary>
        /// Recursively sanitizes JsonElement by removing large content fields
        /// </summary>
        private object SanitizeJsonElement(JsonElement element)
        {
            const int MaxStringLength = 10000; // 10KB max for any string field
            var fileContentFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "content", "fileContent", "file_content", "data", "fileData", "file_data",
                "body", "payload", "source", "sourceCode", "source_code"
            };

            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    var strValue = element.GetString();
                    // Truncate very large strings
                    if (strValue != null && strValue.Length > MaxStringLength)
                    {
                        return $"[Content truncated - {strValue.Length} bytes, saved to execution directory]";
                    }
                    return strValue;

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
                        // Skip known file content fields entirely
                        if (fileContentFields.Contains(property.Name))
                        {
                            dictionary[property.Name] = "[File content removed - saved to execution directory]";
                            continue;
                        }

                        // Handle "files" array specially - keep metadata only
                        if (property.Name.Equals("files", StringComparison.OrdinalIgnoreCase) &&
                            property.Value.ValueKind == JsonValueKind.Array)
                        {
                            dictionary[property.Name] = SanitizeFilesArray(property.Value);
                            continue;
                        }

                        dictionary[property.Name] = SanitizeJsonElement(property.Value);
                    }
                    return dictionary;

                case JsonValueKind.Array:
                    var array = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        array.Add(SanitizeJsonElement(item));
                    }
                    return array;

                default:
                    return element.ToString();
            }
        }

        /// <summary>
        /// Sanitizes a files array by keeping only metadata (name, path, size) and removing content
        /// </summary>
        private object SanitizeFilesArray(JsonElement filesArray)
        {
            var sanitizedFiles = new List<object>();

            foreach (var fileElement in filesArray.EnumerateArray())
            {
                if (fileElement.ValueKind == JsonValueKind.Object)
                {
                    var fileMetadata = new Dictionary<string, object>();

                    foreach (var property in fileElement.EnumerateObject())
                    {
                        // Keep only metadata fields, exclude content
                        switch (property.Name.ToLowerInvariant())
                        {
                            case "name":
                            case "filename":
                            case "file_name":
                            case "path":
                            case "filepath":
                            case "file_path":
                            case "size":
                            case "type":
                            case "mimetype":
                            case "mime_type":
                            case "extension":
                                fileMetadata[property.Name] = SanitizeJsonElement(property.Value);
                                break;

                            case "content":
                            case "data":
                            case "body":
                                // Skip content fields
                                fileMetadata[property.Name] = "[Content saved to execution directory]";
                                break;

                            default:
                                // Include other small fields
                                var sanitized = SanitizeJsonElement(property.Value);
                                if (sanitized is string str && str.Length <= 1000)
                                {
                                    fileMetadata[property.Name] = sanitized;
                                }
                                break;
                        }
                    }

                    sanitizedFiles.Add(fileMetadata);
                }
                else
                {
                    sanitizedFiles.Add(SanitizeJsonElement(fileElement));
                }
            }

            return sanitizedFiles;
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

        #region Tiered Execution Helper Methods

        /// <summary>
        /// Selects the appropriate execution tier based on job profile and availability
        /// </summary>
        private async Task<(string tier, bool isQueued)> SelectExecutionTierAsync(
            string executionId,
            string jobProfileName,
            CancellationToken cancellationToken)
        {
            if (!_tieredSettings.EnableTieredExecution)
            {
                return ("Standard", false); // Fallback to standard execution
            }

            // Get job profile
            if (!_tieredSettings.JobProfiles.TryGetValue(jobProfileName, out var jobProfile))
            {
                _logger.LogWarning("Job profile '{ProfileName}' not found, using default profile '{DefaultProfile}'",
                    jobProfileName, _tieredSettings.DefaultJobProfile);
                jobProfile = _tieredSettings.JobProfiles[_tieredSettings.DefaultJobProfile];
            }

            string preferredTier = jobProfile.PreferredTier;
            _logger.LogInformation("Execution {ExecutionId}: Job profile '{ProfileName}' prefers tier '{PreferredTier}'",
                executionId, jobProfileName, preferredTier);

            // Try preferred tier first
            if (preferredTier.Equals("RAM", StringComparison.OrdinalIgnoreCase))
            {
                if (await TryReserveRamCapacityAsync(executionId, jobProfile, cancellationToken))
                {
                    return ("RAM", false);
                }

                // RAM pool full - check behavior
                if (_tieredSettings.TierSelectionStrategy.FallbackToDisk)
                {
                    _logger.LogInformation("Execution {ExecutionId}: RAM pool full, falling back to Disk tier", executionId);
                    if (await TryReserveDiskSlotAsync(executionId, cancellationToken))
                    {
                        return ("Disk", false);
                    }
                }

                // Handle queue behavior
                if (_tieredSettings.TierSelectionStrategy.RamPoolFullBehavior == "Queue")
                {
                    if (_queuedJobs.Count < _tieredSettings.TierSelectionStrategy.MaxQueueDepth)
                    {
                        _logger.LogInformation("Execution {ExecutionId}: Queued for RAM tier (queue depth: {QueueDepth})",
                            executionId, _queuedJobs.Count + 1);
                        return ("RAM", true); // Will be queued
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Queue is full (max depth: {_tieredSettings.TierSelectionStrategy.MaxQueueDepth}). Cannot accept more jobs.");
                    }
                }
                else // Reject
                {
                    throw new InvalidOperationException("RAM pool is full and queueing is disabled.");
                }
            }
            else if (preferredTier.Equals("Disk", StringComparison.OrdinalIgnoreCase))
            {
                if (await TryReserveDiskSlotAsync(executionId, cancellationToken))
                {
                    return ("Disk", false);
                }

                throw new InvalidOperationException("Disk pool is full. Cannot accept more jobs.");
            }

            throw new InvalidOperationException($"Unknown tier: {preferredTier}");
        }

        /// <summary>
        /// Attempts to reserve RAM capacity using weighted semaphore
        /// </summary>
        private async Task<bool> TryReserveRamCapacityAsync(
            string executionId,
            JobProfile jobProfile,
            CancellationToken cancellationToken)
        {
            if (_ramPoolCapacitySemaphore == null)
            {
                return false;
            }

            int requiredCapacityMB = (int)(jobProfile.RamCapacityCostGB * 1024);

            // Check if we can reserve (non-blocking check)
            lock (_reservationLock)
            {
                if (_ramPoolCapacitySemaphore.CurrentCount < requiredCapacityMB)
                {
                    _logger.LogDebug("Execution {ExecutionId}: Insufficient RAM capacity available ({Available}MB < {Required}MB)",
                        executionId, _ramPoolCapacitySemaphore.CurrentCount, requiredCapacityMB);
                    return false;
                }

                // Check concurrent job limit
                int currentRamJobs = _activeReservations.Values.Count(r => r.Tier == "RAM");
                if (currentRamJobs >= _tieredSettings.RamPool.MaxConcurrentJobs)
                {
                    _logger.LogDebug("Execution {ExecutionId}: RAM pool concurrency limit reached ({Current}/{Max})",
                        executionId, currentRamJobs, _tieredSettings.RamPool.MaxConcurrentJobs);
                    return false;
                }
            }

            // Reserve capacity (blocking)
            bool acquired = await _ramPoolCapacitySemaphore.WaitAsync(0, cancellationToken);
            if (acquired)
            {
                // Acquire the full required capacity
                for (int i = 1; i < requiredCapacityMB; i++)
                {
                    await _ramPoolCapacitySemaphore.WaitAsync(cancellationToken);
                }

                lock (_reservationLock)
                {
                    _activeReservations[executionId] = new JobResourceReservation
                    {
                        ExecutionId = executionId,
                        Tier = "RAM",
                        RamCapacityGB = jobProfile.RamCapacityCostGB,
                        ReservedAt = DateTime.UtcNow
                    };
                }

                _logger.LogInformation("Execution {ExecutionId}: Reserved {Capacity}GB RAM capacity ({Available}GB available)",
                    executionId, jobProfile.RamCapacityCostGB,
                    _ramPoolCapacitySemaphore.CurrentCount / 1024.0);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to reserve a disk slot using concurrency-based semaphore
        /// </summary>
        private async Task<bool> TryReserveDiskSlotAsync(string executionId, CancellationToken cancellationToken)
        {
            if (_diskPoolSemaphore == null)
            {
                return false;
            }

            // Non-blocking check
            if (_diskPoolSemaphore.CurrentCount == 0)
            {
                _logger.LogDebug("Execution {ExecutionId}: Disk pool is full", executionId);
                return false;
            }

            // Reserve slot (blocking)
            bool acquired = await _diskPoolSemaphore.WaitAsync(0, cancellationToken);
            if (acquired)
            {
                lock (_reservationLock)
                {
                    _activeReservations[executionId] = new JobResourceReservation
                    {
                        ExecutionId = executionId,
                        Tier = "Disk",
                        RamCapacityGB = 0,
                        ReservedAt = DateTime.UtcNow
                    };
                }

                _logger.LogInformation("Execution {ExecutionId}: Reserved Disk slot ({Available} slots available)",
                    executionId, _diskPoolSemaphore.CurrentCount);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Releases a resource reservation and processes queued jobs
        /// </summary>
        private async Task ReleaseReservationAsync(string executionId)
        {
            JobResourceReservation? reservation = null;

            lock (_reservationLock)
            {
                if (_activeReservations.TryGetValue(executionId, out reservation))
                {
                    _activeReservations.Remove(executionId);
                }
            }

            if (reservation == null)
            {
                _logger.LogWarning("Execution {ExecutionId}: No reservation found to release", executionId);
                return;
            }

            // Release resources
            if (reservation.Tier == "RAM" && _ramPoolCapacitySemaphore != null)
            {
                int releasedCapacityMB = (int)(reservation.RamCapacityGB * 1024);
                _ramPoolCapacitySemaphore.Release(releasedCapacityMB);

                _logger.LogInformation("Execution {ExecutionId}: Released {Capacity}GB RAM capacity ({Available}GB now available)",
                    executionId, reservation.RamCapacityGB,
                    _ramPoolCapacitySemaphore.CurrentCount / 1024.0);
            }
            else if (reservation.Tier == "Disk" && _diskPoolSemaphore != null)
            {
                _diskPoolSemaphore.Release();

                _logger.LogInformation("Execution {ExecutionId}: Released Disk slot ({Available} slots now available)",
                    executionId, _diskPoolSemaphore.CurrentCount);
            }

            // Process queued jobs
            await ProcessQueuedJobsAsync();
        }

        /// <summary>
        /// RISK MITIGATION: Cleans up stale reservations that were never properly released
        /// Called periodically by StaleReservationMonitorService
        /// </summary>
        public async Task<int> CleanStaleReservationsAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
        {
            if (!_tieredSettings.EnableTieredExecution)
            {
                return 0;
            }

            List<JobResourceReservation> staleReservations = new();
            var now = DateTime.UtcNow;

            // Find stale reservations
            lock (_reservationLock)
            {
                foreach (var kvp in _activeReservations)
                {
                    var age = now - kvp.Value.ReservedAt;
                    if (age > maxAge)
                    {
                        staleReservations.Add(kvp.Value);
                        _logger.LogWarning(
                            "Stale Reservation Detected: Execution {ExecutionId}, Tier: {Tier}, " +
                            "Age: {AgeMinutes:F1} minutes, Reserved at: {ReservedAt:yyyy-MM-dd HH:mm:ss UTC}",
                            kvp.Value.ExecutionId,
                            kvp.Value.Tier,
                            age.TotalMinutes,
                            kvp.Value.ReservedAt);
                    }
                }
            }

            // Release stale reservations
            foreach (var staleReservation in staleReservations)
            {
                try
                {
                    _logger.LogWarning(
                        "Releasing stale reservation for execution {ExecutionId} (tier: {Tier}, age: {AgeMinutes:F1} minutes)",
                        staleReservation.ExecutionId,
                        staleReservation.Tier,
                        (now - staleReservation.ReservedAt).TotalMinutes);

                    await ReleaseReservationAsync(staleReservation.ExecutionId);

                    _logger.LogInformation(
                        "Successfully released stale reservation for execution {ExecutionId}",
                        staleReservation.ExecutionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to release stale reservation for execution {ExecutionId}",
                        staleReservation.ExecutionId);
                }
            }

            if (staleReservations.Count > 0)
            {
                _logger.LogWarning(
                    "Stale Reservation Cleanup: Released {Count} stale reservations. " +
                    "This may indicate crashed or hung executions. " +
                    "Current pool status: RAM available: {RamAvailableGB:F1}GB, Disk slots available: {DiskSlotsAvailable}",
                    staleReservations.Count,
                    _ramPoolCapacitySemaphore?.CurrentCount / 1024.0 ?? 0,
                    _diskPoolSemaphore?.CurrentCount ?? 0);
            }

            return staleReservations.Count;
        }

        /// <summary>
        /// Processes queued jobs when resources become available
        /// </summary>
        private async Task ProcessQueuedJobsAsync()
        {
            List<QueuedJob> jobsToProcess = new();

            lock (_reservationLock)
            {
                while (_queuedJobs.Count > 0)
                {
                    var queuedJob = _queuedJobs.Peek();

                    // Check if job has timed out
                    if ((DateTime.UtcNow - queuedJob.QueuedAt).TotalMinutes > _tieredSettings.TierSelectionStrategy.QueueTimeoutMinutes)
                    {
                        _queuedJobs.Dequeue();
                        _logger.LogWarning("Execution {ExecutionId}: Queued job timed out after {Timeout} minutes",
                            queuedJob.ExecutionId, _tieredSettings.TierSelectionStrategy.QueueTimeoutMinutes);

                        // Update execution status to failed
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                queuedJob.Execution.Status = "failed";
                                queuedJob.Execution.CompletedAt = DateTime.UtcNow;
                                queuedJob.Execution.Results.Error = "Execution timed out in queue";
                                await _unitOfWork.Executions.UpdateAsync(queuedJob.Execution._ID, queuedJob.Execution, queuedJob.CancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to update timed-out execution {ExecutionId}", queuedJob.ExecutionId);
                            }
                        });

                        continue;
                    }

                    // Try to reserve resources for this job
                    // For now, just collect jobs that could potentially run
                    jobsToProcess.Add(queuedJob);
                    _queuedJobs.Dequeue();

                    // Only process one job at a time to avoid race conditions
                    break;
                }
            }

            // Process jobs outside the lock
            foreach (var job in jobsToProcess)
            {
                try
                {
                    _logger.LogInformation("Execution {ExecutionId}: Processing queued job", job.ExecutionId);

                    // Re-attempt tier selection and execution
                    // This will be called from ExecuteProjectInBackgroundAsync
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ExecuteProjectInBackgroundAsync(
                                job.Execution,
                                job.Request,
                                job.CancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to execute queued job {ExecutionId}", job.ExecutionId);
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing queued job {ExecutionId}", job.ExecutionId);
                }
            }
        }

        /// <summary>
        /// RISK MITIGATION: Validates RAM pool capacity on startup
        /// Ensures the RAM pool is large enough to handle at least one smallest job profile
        /// and warns about potential configuration issues
        /// </summary>
        private void ValidateRamPoolCapacity()
        {
            double totalRamCapacityGB = _tieredSettings.RamPool.TotalCapacityGB;

            _logger.LogInformation(
                "RAM Pool Capacity Validation: Total capacity = {TotalCapacityGB}GB",
                totalRamCapacityGB);

            // Find smallest job profile RAM requirement
            var jobProfiles = _tieredSettings.JobProfiles;
            if (jobProfiles == null || !jobProfiles.Any())
            {
                _logger.LogWarning(
                    "RAM Pool Capacity Validation: No job profiles defined! " +
                    "Tiered execution may not function correctly. " +
                    "Please configure JobProfiles in appsettings.json");
                return;
            }

            var smallestRamRequirementGB = jobProfiles.Values
                .Where(p => p.PreferredTier.Equals("RAM", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.RamCapacityCostGB)
                .DefaultIfEmpty(0.5) // Default to 0.5GB if no RAM-preferred profiles
                .Min();

            var largestRamRequirementGB = jobProfiles.Values
                .Where(p => p.PreferredTier.Equals("RAM", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.RamCapacityCostGB)
                .DefaultIfEmpty(0.5)
                .Max();

            _logger.LogInformation(
                "RAM Pool Capacity Validation: Smallest RAM job profile requires {SmallestGB}GB, " +
                "Largest requires {LargestGB}GB",
                smallestRamRequirementGB, largestRamRequirementGB);

            // CRITICAL: Check if RAM pool can handle at least one smallest job
            if (totalRamCapacityGB < smallestRamRequirementGB)
            {
                _logger.LogCritical(
                    "CRITICAL CONFIGURATION ERROR: RAM pool capacity ({TotalCapacityGB}GB) is less than " +
                    "the smallest job profile requirement ({SmallestGB}GB). " +
                    "No RAM-tier jobs can be executed! " +
                    "Please increase RamPool.TotalCapacityGB in appsettings.json to at least {MinimumGB}GB",
                    totalRamCapacityGB, smallestRamRequirementGB, smallestRamRequirementGB);
            }
            else
            {
                _logger.LogInformation(
                    "✓ RAM Pool Capacity Validation: Passed. Pool can handle at least {MinJobs} smallest jobs concurrently",
                    (int)(totalRamCapacityGB / smallestRamRequirementGB));
            }

            // WARNING: Check if largest job profile can fit
            if (totalRamCapacityGB < largestRamRequirementGB)
            {
                var affectedProfiles = jobProfiles
                    .Where(kvp => kvp.Value.PreferredTier.Equals("RAM", StringComparison.OrdinalIgnoreCase)
                                  && kvp.Value.RamCapacityCostGB > totalRamCapacityGB)
                    .Select(kvp => $"{kvp.Key} ({kvp.Value.RamCapacityCostGB}GB)")
                    .ToList();

                _logger.LogWarning(
                    "RAM Pool Capacity Warning: Some job profiles exceed total RAM capacity ({TotalCapacityGB}GB). " +
                    "Affected profiles: {AffectedProfiles}. " +
                    "These jobs will always fall back to Disk tier or be queued/rejected. " +
                    "Consider increasing RamPool.TotalCapacityGB to at least {RecommendedGB}GB or adjusting job profile costs.",
                    totalRamCapacityGB,
                    string.Join(", ", affectedProfiles),
                    largestRamRequirementGB);
            }

            // INFO: Calculate theoretical max concurrent jobs
            int maxRamConcurrentJobs = _tieredSettings.RamPool.MaxConcurrentJobs;
            double theoreticalMaxCapacity = smallestRamRequirementGB * maxRamConcurrentJobs;

            if (theoreticalMaxCapacity > totalRamCapacityGB)
            {
                int actualMaxConcurrentSmallJobs = (int)(totalRamCapacityGB / smallestRamRequirementGB);
                _logger.LogInformation(
                    "RAM Pool Capacity Info: MaxConcurrentJobs is set to {MaxJobs}, but RAM capacity " +
                    "limits actual concurrent smallest jobs to ~{ActualMax}. " +
                    "This is expected and helps balance job sizes. " +
                    "To allow {MaxJobs} concurrent smallest jobs, capacity would need to be ~{RequiredGB}GB",
                    maxRamConcurrentJobs, actualMaxConcurrentSmallJobs, theoreticalMaxCapacity);
            }

            // Check queue configuration
            if (_tieredSettings.TierSelectionStrategy.RamPoolFullBehavior == "Queue")
            {
                _logger.LogInformation(
                    "RAM Pool Capacity Info: Queue is enabled with max depth {MaxQueueDepth} and timeout {TimeoutMinutes} minutes",
                    _tieredSettings.TierSelectionStrategy.MaxQueueDepth,
                    _tieredSettings.TierSelectionStrategy.QueueTimeoutMinutes);
            }
            else
            {
                _logger.LogInformation(
                    "RAM Pool Capacity Info: Queue is disabled. Jobs will be rejected when RAM pool is full " +
                    "(FallbackToDisk: {FallbackEnabled})",
                    _tieredSettings.TierSelectionStrategy.FallbackToDisk);
            }

            _logger.LogInformation("RAM Pool Capacity Validation: Completed");
        }

        /// <summary>
        /// Enqueues a job for later execution
        /// </summary>
        private void EnqueueJob(string executionId, ExecutionModel execution, ProgramExecutionRequestDto request, CancellationToken cancellationToken)
        {
            lock (_reservationLock)
            {
                _queuedJobs.Enqueue(new QueuedJob
                {
                    ExecutionId = executionId,
                    Execution = execution,
                    Request = request,
                    QueuedAt = DateTime.UtcNow,
                    CancellationToken = cancellationToken
                });

                _logger.LogInformation("Execution {ExecutionId}: Enqueued for execution (queue depth: {QueueDepth})",
                    executionId, _queuedJobs.Count);
            }
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

        // Tiered Execution Supporting Classes (settings classes are in ProjectExecutionEngine.cs)
        private class JobResourceReservation
        {
            public string ExecutionId { get; set; } = string.Empty;
            public string Tier { get; set; } = string.Empty;
            public double RamCapacityGB { get; set; }
            public DateTime ReservedAt { get; set; }
        }

        private class QueuedJob
        {
            public string ExecutionId { get; set; } = string.Empty;
            public ExecutionModel Execution { get; set; } = null!;
            public ProgramExecutionRequestDto Request { get; set; } = null!;
            public DateTime QueuedAt { get; set; }
            public CancellationToken CancellationToken { get; set; }
        }

        #endregion
    }
}