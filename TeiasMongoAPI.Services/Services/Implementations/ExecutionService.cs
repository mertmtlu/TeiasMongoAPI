using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
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
    public class ExecutionService : BaseService, IExecutionService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IProgramService _programService;
        private readonly IVersionService _versionService;
        private readonly IDeploymentService _deploymentService;
        private readonly ExecutionSettings _settings;
        private readonly Dictionary<string, ExecutionContext> _activeExecutions = new();

        public ExecutionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IFileStorageService fileStorageService,
            IProgramService programService,
            IVersionService versionService,
            IDeploymentService deploymentService,
            IOptions<ExecutionSettings> settings,
            ILogger<ExecutionService> logger)
            : base(unitOfWork, mapper, logger)
        {
            _fileStorageService = fileStorageService;
            _programService = programService;
            _versionService = versionService;
            _deploymentService = deploymentService;
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

            // Get output files
            dto.OutputFiles = await GetExecutionOutputFilesAsync(id, cancellationToken);

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
            var executions = await _unitOfWork.Executions.GetAllAsync(cancellationToken);
            var executionsList = executions.ToList();

            // Apply pagination
            var totalCount = executionsList.Count;
            var paginatedExecutions = executionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = await MapExecutionListDtosAsync(paginatedExecutions, cancellationToken);

            return new PagedResponse<ExecutionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<ExecutionListDto>> SearchAsync(ExecutionSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var allExecutions = await _unitOfWork.Executions.GetAllAsync(cancellationToken);
            var filteredExecutions = allExecutions.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchDto.ProgramId))
            {
                var programObjectId = ParseObjectId(searchDto.ProgramId);
                filteredExecutions = filteredExecutions.Where(e => e.ProgramId == programObjectId);
            }

            if (!string.IsNullOrEmpty(searchDto.VersionId))
            {
                var versionObjectId = ParseObjectId(searchDto.VersionId);
                filteredExecutions = filteredExecutions.Where(e => e.VersionId == versionObjectId);
            }

            if (!string.IsNullOrEmpty(searchDto.UserId))
            {
                filteredExecutions = filteredExecutions.Where(e => e.UserId == searchDto.UserId);
            }

            if (!string.IsNullOrEmpty(searchDto.Status))
            {
                filteredExecutions = filteredExecutions.Where(e => e.Status == searchDto.Status);
            }

            if (!string.IsNullOrEmpty(searchDto.ExecutionType))
            {
                filteredExecutions = filteredExecutions.Where(e => e.ExecutionType == searchDto.ExecutionType);
            }

            if (searchDto.StartedFrom.HasValue)
            {
                filteredExecutions = filteredExecutions.Where(e => e.StartedAt >= searchDto.StartedFrom.Value);
            }

            if (searchDto.StartedTo.HasValue)
            {
                filteredExecutions = filteredExecutions.Where(e => e.StartedAt <= searchDto.StartedTo.Value);
            }

            if (searchDto.CompletedFrom.HasValue)
            {
                filteredExecutions = filteredExecutions.Where(e => e.CompletedAt >= searchDto.CompletedFrom.Value);
            }

            if (searchDto.CompletedTo.HasValue)
            {
                filteredExecutions = filteredExecutions.Where(e => e.CompletedAt <= searchDto.CompletedTo.Value);
            }

            if (searchDto.ExitCodeFrom.HasValue)
            {
                filteredExecutions = filteredExecutions.Where(e => e.Results.ExitCode >= searchDto.ExitCodeFrom.Value);
            }

            if (searchDto.ExitCodeTo.HasValue)
            {
                filteredExecutions = filteredExecutions.Where(e => e.Results.ExitCode <= searchDto.ExitCodeTo.Value);
            }

            if (searchDto.HasErrors.HasValue)
            {
                if (searchDto.HasErrors.Value)
                {
                    filteredExecutions = filteredExecutions.Where(e => !string.IsNullOrEmpty(e.Results.Error) || e.Results.ExitCode != 0);
                }
                else
                {
                    filteredExecutions = filteredExecutions.Where(e => string.IsNullOrEmpty(e.Results.Error) && e.Results.ExitCode == 0);
                }
            }

            var executionsList = filteredExecutions.ToList();

            // Apply pagination
            var totalCount = executionsList.Count;
            var paginatedExecutions = executionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = await MapExecutionListDtosAsync(paginatedExecutions, cancellationToken);

            return new PagedResponse<ExecutionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
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

            // Delete output files
            try
            {
                foreach (var outputFile in execution.Results.OutputFiles)
                {
                    await _fileStorageService.DeleteFileAsync(outputFile, cancellationToken);
                }
                _logger.LogInformation("Deleted output files for execution {ExecutionId}", id);
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

        public async Task<ExecutionDto> ExecuteProgramAsync(string programId, ProgramExecutionRequestDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Get current version or latest version
            var version = string.IsNullOrEmpty(program.CurrentVersion)
                ? await _unitOfWork.Versions.GetLatestVersionForProgramAsync(programObjectId, cancellationToken)
                : await _unitOfWork.Versions.GetByIdAsync(ParseObjectId(program.CurrentVersion), cancellationToken);

            if (version == null)
            {
                throw new InvalidOperationException($"No version found for program {programId}.");
            }

            if (version.Status != "approved")
            {
                throw new InvalidOperationException("Can only execute approved versions.");
            }

            // Check execution limits
            await ValidateExecutionLimitsAsync(programId, "system", cancellationToken);

            // Create execution record
            var execution = new Execution
            {
                ProgramId = programObjectId,
                VersionId = version._ID,
                UserId = "system", // Should come from current user context
                ExecutionType = "code_execution",
                StartedAt = DateTime.UtcNow,
                Status = "running",
                Parameters = dto.Parameters,
                Results = new ExecutionResults(),
                ResourceUsage = new ResourceUsage()
            };

            var createdExecution = await _unitOfWork.Executions.CreateAsync(execution, cancellationToken);
            var executionId = createdExecution._ID.ToString();

            _logger.LogInformation("Started execution {ExecutionId} for program {ProgramId} version {VersionNumber}",
                executionId, programId, version.VersionNumber);

            // Start execution asynchronously
            _ = Task.Run(async () => await ExecuteInBackgroundAsync(createdExecution, dto, cancellationToken));

            return _mapper.Map<ExecutionDto>(createdExecution);
        }

        public async Task<ExecutionDto> ExecuteVersionAsync(string versionId, VersionExecutionRequestDto dto, CancellationToken cancellationToken = default)
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
            await ValidateExecutionLimitsAsync(version.ProgramId.ToString(), "system", cancellationToken);

            // Create execution record
            var execution = new Execution
            {
                ProgramId = version.ProgramId,
                VersionId = versionObjectId,
                UserId = "system", // Should come from current user context
                ExecutionType = "code_execution",
                StartedAt = DateTime.UtcNow,
                Status = "running",
                Parameters = dto.Parameters,
                Results = new ExecutionResults(),
                ResourceUsage = new ResourceUsage()
            };

            var createdExecution = await _unitOfWork.Executions.CreateAsync(execution, cancellationToken);
            var executionId = createdExecution._ID.ToString();

            _logger.LogInformation("Started execution {ExecutionId} for version {VersionId}",
                executionId, versionId);

            // Start execution asynchronously
            _ = Task.Run(async () => await ExecuteVersionInBackgroundAsync(createdExecution, dto, cancellationToken));

            return _mapper.Map<ExecutionDto>(createdExecution);
        }

        public async Task<ExecutionDto> ExecuteWithParametersAsync(string programId, ExecutionParametersDto dto, CancellationToken cancellationToken = default)
        {
            var programRequest = new ProgramExecutionRequestDto
            {
                Parameters = dto.Parameters,
                Environment = dto.Environment,
                ResourceLimits = dto.ResourceLimits,
                SaveResults = dto.SaveResults,
                TimeoutMinutes = dto.TimeoutMinutes
            };

            return await ExecuteProgramAsync(programId, programRequest, cancellationToken);
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

            // Update status in database
            var success = await _unitOfWork.Executions.UpdateStatusAsync(objectId, "stopped", cancellationToken);

            // Stop active execution
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

        public async Task<List<ExecutionOutputFileDto>> GetExecutionOutputFilesAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            var outputFiles = new List<ExecutionOutputFileDto>();

            foreach (var outputFile in execution.Results.OutputFiles)
            {
                try
                {
                    var metadata = await _fileStorageService.GetFileMetadataAsync(outputFile, cancellationToken);
                    outputFiles.Add(new ExecutionOutputFileDto
                    {
                        FileName = Path.GetFileName(metadata.FilePath),
                        Path = metadata.FilePath,
                        Size = metadata.Size,
                        ContentType = metadata.ContentType,
                        CreatedAt = metadata.CreatedAt,
                        DownloadUrl = $"/api/executions/{id}/results/{Path.GetFileName(metadata.FilePath)}"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get metadata for output file {OutputFile}", outputFile);
                }
            }

            return outputFiles;
        }

        public async Task<ExecutionOutputFileContentDto> GetExecutionOutputFileAsync(string id, string fileName, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            // Find the output file
            var outputFileKey = execution.Results.OutputFiles
                .FirstOrDefault(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));

            if (outputFileKey == null)
            {
                throw new KeyNotFoundException($"Output file {fileName} not found for execution {id}.");
            }

            var content = await _fileStorageService.GetFileContentAsync(outputFileKey, cancellationToken);
            var metadata = await _fileStorageService.GetFileMetadataAsync(outputFileKey, cancellationToken);

            return new ExecutionOutputFileContentDto
            {
                FileName = fileName,
                ContentType = metadata.ContentType,
                Content = content,
                Size = metadata.Size,
                CreatedAt = metadata.CreatedAt
            };
        }

        public async Task<bool> DownloadExecutionResultsAsync(string id, string downloadPath, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            try
            {
                Directory.CreateDirectory(downloadPath);

                foreach (var outputFile in execution.Results.OutputFiles)
                {
                    var content = await _fileStorageService.GetFileContentAsync(outputFile, cancellationToken);
                    var metadata = await _fileStorageService.GetFileMetadataAsync(outputFile, cancellationToken);
                    var filePath = Path.Combine(downloadPath, Path.GetFileName(metadata.FilePath));
                    await File.WriteAllBytesAsync(filePath, content, cancellationToken);
                }

                _logger.LogInformation("Downloaded execution results for {ExecutionId} to {DownloadPath}", id, downloadPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download execution results for {ExecutionId}", id);
                return false;
            }
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

            // Create execution record for web app deployment
            var execution = new Execution
            {
                ProgramId = programObjectId,
                VersionId = program.CurrentVersion != null ? ParseObjectId(program.CurrentVersion) : programObjectId,
                UserId = "system", // Should come from current user context
                ExecutionType = "web_app_deploy",
                StartedAt = DateTime.UtcNow,
                Status = "running",
                Parameters = dto.Configuration,
                Results = new ExecutionResults(),
                ResourceUsage = new ResourceUsage()
            };

            var createdExecution = await _unitOfWork.Executions.CreateAsync(execution, cancellationToken);

            // Deploy web application
            try
            {
                var deploymentRequest = new AppDeploymentRequestDto
                {
                    DeploymentType = AppDeploymentType.PreBuiltWebApp,
                    Configuration = dto.Configuration,
                    Environment = dto.Environment,
                    SupportedFeatures = dto.Features,
                    AutoStart = dto.AutoStart,
                    Port = dto.Port,
                    DomainName = dto.DomainName
                };

                var deployment = await _deploymentService.DeployPreBuiltAppAsync(programId, deploymentRequest, cancellationToken);

                // Update execution with deployment results
                await _unitOfWork.Executions.CompleteExecutionAsync(
                    createdExecution._ID,
                    0,
                    "Web application deployed successfully",
                    new List<string>(),
                    null,
                    cancellationToken);

                // Update with web app URL
                createdExecution.Results.WebAppUrl = deployment.ApplicationUrl;
                await _unitOfWork.Executions.UpdateAsync(createdExecution._ID, createdExecution, cancellationToken);

                _logger.LogInformation("Deployed web application for program {ProgramId} with execution {ExecutionId}",
                    programId, createdExecution._ID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deploy web application for program {ProgramId}", programId);

                await _unitOfWork.Executions.CompleteExecutionAsync(
                    createdExecution._ID,
                    1,
                    "Web application deployment failed",
                    new List<string>(),
                    ex.Message,
                    cancellationToken);
            }

            return _mapper.Map<ExecutionDto>(createdExecution);
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
            IEnumerable<Execution> executions;

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
            if (dto.ScheduledTime <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Scheduled time must be in the future.");
            }

            // For this implementation, we'll create a pending execution that would be picked up by a scheduler
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var execution = new Execution
            {
                ProgramId = programObjectId,
                VersionId = program.CurrentVersion != null ? ParseObjectId(program.CurrentVersion) : programObjectId,
                UserId = "system", // Should come from current user context
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
            return await _programService.ValidateUserAccessAsync(programId, userId, "write", cancellationToken);
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
                // Check program files for security issues
                var files = await _fileStorageService.ListProgramFilesAsync(programId, null, cancellationToken);

                foreach (var file in files)
                {
                    var extension = Path.GetExtension(file.FilePath).ToLowerInvariant();

                    // Check for potentially dangerous file types
                    if (new[] { ".exe", ".bat", ".cmd", ".ps1", ".sh" }.Contains(extension))
                    {
                        issues.Add($"Executable file detected: {file.FilePath}");
                        riskLevel = Math.Max(riskLevel, 4);
                    }

                    // Check file size
                    if (file.Size > 100 * 1024 * 1024) // 100MB
                    {
                        warnings.Add($"Large file detected: {file.FilePath} ({file.Size / 1024 / 1024} MB)");
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

        #region Private Helper Methods

        private async Task<List<ExecutionListDto>> MapExecutionListDtosAsync(List<Execution> executions, CancellationToken cancellationToken)
        {
            var dtos = new List<ExecutionListDto>();

            foreach (var execution in executions)
            {
                var dto = _mapper.Map<ExecutionListDto>(execution);

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
                    _logger.LogWarning(ex, "Failed to get program details for execution {ExecutionId}", execution._ID);
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
                    _logger.LogWarning(ex, "Failed to get user details for execution {ExecutionId}", execution._ID);
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
                    _logger.LogWarning(ex, "Failed to get version details for execution {ExecutionId}", execution._ID);
                }

                // Calculate duration and other properties
                dto.ExitCode = execution.Results.ExitCode;
                dto.HasError = !string.IsNullOrEmpty(execution.Results.Error) || execution.Results.ExitCode != 0;

                if (execution.CompletedAt.HasValue)
                {
                    dto.Duration = (execution.CompletedAt.Value - execution.StartedAt).TotalMinutes;
                }

                dto.ResourceUsage = _mapper.Map<ExecutionResourceUsageDto>(execution.ResourceUsage);

                dtos.Add(dto);
            }

            return dtos;
        }

        private async Task<PagedResponse<ExecutionListDto>> CreatePagedExecutionResponse(IEnumerable<Execution> executions, PaginationRequestDto pagination, CancellationToken cancellationToken)
        {
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

        private async Task ExecuteInBackgroundAsync(Execution execution, ProgramExecutionRequestDto dto, CancellationToken cancellationToken)
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
                // Simulate program execution
                _logger.LogInformation("Starting background execution for {ExecutionId}", executionId);

                // Get program files
                var files = await _fileStorageService.ListProgramFilesAsync(execution.ProgramId.ToString(), null, cancellationToken);

                // Simulate execution based on program type
                var program = await _unitOfWork.Programs.GetByIdAsync(execution.ProgramId, cancellationToken);

                await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationTokenSource.Token); // Simulate work

                // Generate mock results
                var output = $"Execution completed for program {program?.Name ?? "Unknown"}\n";
                output += $"Processed {files.Count} files\n";
                output += $"Parameters: {System.Text.Json.JsonSerializer.Serialize(dto.Parameters)}\n";
                output += "Execution finished successfully.";

                await _unitOfWork.Executions.CompleteExecutionAsync(
                    execution._ID,
                    0,
                    output,
                    new List<string>(),
                    null,
                    cancellationToken);

                _logger.LogInformation("Completed background execution for {ExecutionId}", executionId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Execution {ExecutionId} was cancelled", executionId);
                await _unitOfWork.Executions.UpdateStatusAsync(execution._ID, "stopped", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background execution failed for {ExecutionId}", executionId);

                await _unitOfWork.Executions.CompleteExecutionAsync(
                    execution._ID,
                    1,
                    "Execution failed",
                    new List<string>(),
                    ex.Message,
                    cancellationToken);
            }
            finally
            {
                _activeExecutions.Remove(executionId);
            }
        }

        private async Task ExecuteVersionInBackgroundAsync(Execution execution, VersionExecutionRequestDto dto, CancellationToken cancellationToken)
        {
            var programRequest = new ProgramExecutionRequestDto
            {
                Parameters = dto.Parameters,
                Environment = dto.Environment,
                ResourceLimits = dto.ResourceLimits,
                SaveResults = dto.SaveResults,
                TimeoutMinutes = dto.TimeoutMinutes
            };

            await ExecuteInBackgroundAsync(execution, programRequest, cancellationToken);
        }

        private double? CalculateExecutionProgress(Execution execution)
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

        private string? GetCurrentExecutionStep(Execution execution)
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

        private string GetStatusMessage(Execution execution)
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

        #endregion
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
}