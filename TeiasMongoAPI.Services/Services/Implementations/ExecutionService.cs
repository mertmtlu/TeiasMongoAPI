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
    public class ExecutionService : BaseService, IExecutionService
    {
        private readonly Dictionary<string, ExecutionResourceLimitsDto> _defaultResourceLimits;

        public ExecutionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<ExecutionService> logger)
            : base(unitOfWork, mapper, logger)
        {
            _defaultResourceLimits = InitializeDefaultResourceLimits();
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

            // Enrich with additional data
            await EnrichExecutionDetailAsync(dto, execution, cancellationToken);

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

            var dtos = _mapper.Map<List<ExecutionListDto>>(paginatedExecutions);

            // Enrich list items
            await EnrichExecutionListAsync(dtos, cancellationToken);

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
                filteredExecutions = filteredExecutions.Where(e =>
                    searchDto.HasErrors.Value ? !string.IsNullOrEmpty(e.Results.Error) : string.IsNullOrEmpty(e.Results.Error));
            }

            var executionsList = filteredExecutions.ToList();

            // Apply pagination
            var totalCount = executionsList.Count;
            var paginatedExecutions = executionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ExecutionListDto>>(paginatedExecutions);

            // Enrich list items
            await EnrichExecutionListAsync(dtos, cancellationToken);

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

            // Check if execution can be deleted (not running)
            if (execution.Status == "running")
            {
                throw new InvalidOperationException("Cannot delete a running execution. Stop it first.");
            }

            // TODO: Clean up execution files and resources
            await CleanupExecutionResourcesAsync(objectId, cancellationToken);

            return await _unitOfWork.Executions.DeleteAsync(objectId, cancellationToken);
        }

        #endregion

        #region Execution Filtering and History

        public async Task<PagedResponse<ExecutionListDto>> GetByProgramIdAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var executions = await _unitOfWork.Executions.GetByProgramIdAsync(programObjectId, cancellationToken);
            var executionsList = executions.ToList();

            // Apply pagination
            var totalCount = executionsList.Count;
            var paginatedExecutions = executionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ExecutionListDto>>(paginatedExecutions);
            await EnrichExecutionListAsync(dtos, cancellationToken);

            return new PagedResponse<ExecutionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<ExecutionListDto>> GetByVersionIdAsync(string versionId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var versionObjectId = ParseObjectId(versionId);
            var executions = await _unitOfWork.Executions.GetByVersionIdAsync(versionObjectId, cancellationToken);
            var executionsList = executions.ToList();

            // Apply pagination
            var totalCount = executionsList.Count;
            var paginatedExecutions = executionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ExecutionListDto>>(paginatedExecutions);
            await EnrichExecutionListAsync(dtos, cancellationToken);

            return new PagedResponse<ExecutionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<ExecutionListDto>> GetByUserIdAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.Executions.GetByUserIdAsync(userId, cancellationToken);
            var executionsList = executions.ToList();

            // Apply pagination
            var totalCount = executionsList.Count;
            var paginatedExecutions = executionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ExecutionListDto>>(paginatedExecutions);
            await EnrichExecutionListAsync(dtos, cancellationToken);

            return new PagedResponse<ExecutionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<ExecutionListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.Executions.GetByStatusAsync(status, cancellationToken);
            var executionsList = executions.ToList();

            // Apply pagination
            var totalCount = executionsList.Count;
            var paginatedExecutions = executionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ExecutionListDto>>(paginatedExecutions);
            await EnrichExecutionListAsync(dtos, cancellationToken);

            return new PagedResponse<ExecutionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<ExecutionListDto>> GetRunningExecutionsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            return await GetByStatusAsync("running", pagination, cancellationToken);
        }

        public async Task<PagedResponse<ExecutionListDto>> GetCompletedExecutionsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            return await GetByStatusAsync("completed", pagination, cancellationToken);
        }

        public async Task<PagedResponse<ExecutionListDto>> GetFailedExecutionsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            return await GetByStatusAsync("failed", pagination, cancellationToken);
        }

        public async Task<List<ExecutionListDto>> GetRecentExecutionsAsync(int count = 10, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.Executions.GetRecentExecutionsAsync(count, cancellationToken);
            var dtos = _mapper.Map<List<ExecutionListDto>>(executions);
            await EnrichExecutionListAsync(dtos, cancellationToken);

            return dtos;
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

            // Get the current version or latest version
            var version = await GetExecutionVersionAsync(program, null, cancellationToken);

            return await CreateAndStartExecutionAsync(program, version, dto.Parameters, dto.Environment,
                dto.ResourceLimits, "code_execution", cancellationToken);
        }

        public async Task<ExecutionDto> ExecuteVersionAsync(string versionId, VersionExecutionRequestDto dto, CancellationToken cancellationToken = default)
        {
            var versionObjectId = ParseObjectId(versionId);
            var version = await _unitOfWork.Versions.GetByIdAsync(versionObjectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            var program = await _unitOfWork.Programs.GetByIdAsync(version.ProgramId, cancellationToken);
            if (program == null)
            {
                throw new KeyNotFoundException($"Program for version {versionId} not found.");
            }

            return await CreateAndStartExecutionAsync(program, version, dto.Parameters, dto.Environment,
                dto.ResourceLimits, "code_execution", cancellationToken);
        }

        public async Task<ExecutionDto> ExecuteWithParametersAsync(string programId, ExecutionParametersDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Get specified version or current version
            var version = await GetExecutionVersionAsync(program, dto.VersionId, cancellationToken);

            return await CreateAndStartExecutionAsync(program, version, dto.Parameters, dto.Environment,
                dto.ResourceLimits, "code_execution", cancellationToken);
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

            if (execution.Status != "running")
            {
                throw new InvalidOperationException($"Execution {id} is not running (status: {execution.Status}).");
            }

            // TODO: Implement actual execution stopping logic (kill container, stop process, etc.)
            _logger.LogInformation("Stopping execution {ExecutionId}", id);

            // Update status
            var success = await _unitOfWork.Executions.UpdateStatusAsync(objectId, "stopped", cancellationToken);

            if (success)
            {
                // Complete execution with stop status
                await _unitOfWork.Executions.CompleteExecutionAsync(objectId, -1, "Execution stopped by user",
                    new List<string>(), "Execution was manually stopped", cancellationToken);
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
                throw new InvalidOperationException($"Execution {id} is not running.");
            }

            // TODO: Implement pause logic (send SIGSTOP to container, etc.)
            _logger.LogInformation("Pausing execution {ExecutionId}", id);

            return await _unitOfWork.Executions.UpdateStatusAsync(objectId, "paused", cancellationToken);
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
                throw new InvalidOperationException($"Execution {id} is not paused.");
            }

            // TODO: Implement resume logic (send SIGCONT to container, etc.)
            _logger.LogInformation("Resuming execution {ExecutionId}", id);

            return await _unitOfWork.Executions.UpdateStatusAsync(objectId, "running", cancellationToken);
        }

        public async Task<ExecutionStatusDto> GetExecutionStatusAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            // TODO: Get real-time status from execution environment
            var progress = CalculateExecutionProgress(execution);

            return new ExecutionStatusDto
            {
                Id = id,
                Status = execution.Status,
                StartedAt = execution.StartedAt,
                CompletedAt = execution.CompletedAt,
                Progress = progress,
                CurrentStep = GetCurrentExecutionStep(execution),
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

            // TODO: Stream live output from execution environment
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

            // TODO: Read execution logs from storage
            var logs = new List<string>();

            // For now, return some sample logs
            if (!string.IsNullOrEmpty(execution.Results.Output))
            {
                logs.AddRange(execution.Results.Output.Split('\n').TakeLast(lines));
            }

            return logs;
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

            // TODO: Scan execution output directory for files
            var outputFiles = new List<ExecutionOutputFileDto>();

            foreach (var fileName in execution.Results.OutputFiles)
            {
                outputFiles.Add(new ExecutionOutputFileDto
                {
                    FileName = Path.GetFileName(fileName),
                    Path = fileName,
                    Size = 0, // Would get from file system
                    ContentType = GetContentType(fileName),
                    CreatedAt = execution.CompletedAt ?? execution.StartedAt,
                    DownloadUrl = $"/api/executions/{id}/results/{fileName}"
                });
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

            if (!execution.Results.OutputFiles.Contains(fileName))
            {
                throw new KeyNotFoundException($"Output file '{fileName}' not found for execution {id}.");
            }

            // TODO: Read file content from storage
            return new ExecutionOutputFileContentDto
            {
                FileName = fileName,
                ContentType = GetContentType(fileName),
                Content = Array.Empty<byte>(), // Would read from storage
                Size = 0, // Would get from file
                CreatedAt = execution.CompletedAt ?? execution.StartedAt
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

            // TODO: Create zip archive of all output files and copy to download path
            _logger.LogInformation("Downloading results for execution {ExecutionId} to {DownloadPath}", id, downloadPath);

            return true;
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

            // Get the current version for deployment
            var version = await GetExecutionVersionAsync(program, null, cancellationToken);

            return await CreateAndStartExecutionAsync(program, version, new { }, dto.Environment,
                null, "web_app_deploy", cancellationToken);
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
                throw new InvalidOperationException($"Execution {executionId} is not a web application deployment.");
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
                throw new InvalidOperationException($"Execution {executionId} is not a web application deployment.");
            }

            // TODO: Check actual web app health
            return new WebAppStatusDto
            {
                Status = execution.Status == "completed" ? "active" : "inactive",
                Url = execution.Results.WebAppUrl ?? string.Empty,
                IsHealthy = execution.Status == "completed",
                LastHealthCheck = DateTime.UtcNow,
                ResponseTime = 100, // Would be measured
                ErrorMessage = execution.Results.Error,
                Metrics = new Dictionary<string, object>()
            };
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
                throw new InvalidOperationException($"Execution {executionId} is not a web application deployment.");
            }

            // TODO: Implement web app restart logic
            _logger.LogInformation("Restarting web application for execution {ExecutionId}", executionId);

            return true;
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
                throw new InvalidOperationException($"Execution {executionId} is not a web application deployment.");
            }

            // TODO: Implement web app stop logic
            _logger.LogInformation("Stopping web application for execution {ExecutionId}", executionId);

            return await _unitOfWork.Executions.UpdateStatusAsync(objectId, "stopped", cancellationToken);
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
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {id} not found.");
            }

            return await _unitOfWork.Executions.UpdateResourceUsageAsync(objectId, dto.CpuTime, dto.MemoryUsed, dto.DiskUsed, cancellationToken);
        }

        public async Task<List<ExecutionResourceTrendDto>> GetResourceTrendsAsync(string programId, int days = 7, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var executions = await _unitOfWork.Executions.GetByProgramIdAsync(programObjectId, cancellationToken);

            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var recentExecutions = executions.Where(e => e.StartedAt >= cutoffDate);

            var trends = new List<ExecutionResourceTrendDto>();

            // Group by day and calculate averages
            var dailyExecutions = recentExecutions
                .GroupBy(e => e.StartedAt.Date)
                .OrderBy(g => g.Key);

            foreach (var group in dailyExecutions)
            {
                trends.Add(new ExecutionResourceTrendDto
                {
                    Timestamp = group.Key,
                    CpuUsage = group.Average(e => e.ResourceUsage.CpuTime),
                    MemoryUsage = (long)Math.Round(group.Average(e => e.ResourceUsage.MemoryUsed)),
                    DiskUsage = (long)Math.Round(group.Average(e => e.ResourceUsage.DiskUsed)),
                    ActiveExecutions = group.Count(e => e.Status == "running")
                });
            }

            return trends;
        }

        public async Task<ExecutionResourceLimitsDto> GetResourceLimitsAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Return default limits for the program language
            return _defaultResourceLimits.GetValueOrDefault(program.Language, _defaultResourceLimits["default"]);
        }

        public async Task<bool> UpdateResourceLimitsAsync(string programId, ExecutionResourceLimitsUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Store custom resource limits in program metadata
            _logger.LogInformation("Updating resource limits for program {ProgramId}", programId);

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

            return new ExecutionStatsDto
            {
                TotalExecutions = executionsList.Count,
                SuccessfulExecutions = executionsList.Count(e => e.Status == "completed" && e.Results.ExitCode == 0),
                FailedExecutions = executionsList.Count(e => e.Status == "failed" || e.Results.ExitCode != 0),
                RunningExecutions = executionsList.Count(e => e.Status == "running"),
                AverageExecutionTime = CalculateAverageExecutionTime(executionsList),
                SuccessRate = CalculateSuccessRate(executionsList),
                TotalCpuTime = (long)executionsList.Sum(e => e.ResourceUsage.CpuTime),
                TotalMemoryUsed = executionsList.Sum(e => e.ResourceUsage.MemoryUsed),
                ExecutionsByStatus = executionsList.GroupBy(e => e.Status).ToDictionary(g => g.Key, g => g.Count()),
                ExecutionsByType = executionsList.GroupBy(e => e.ExecutionType).ToDictionary(g => g.Key, g => g.Count())
            };
        }

        public async Task<List<ExecutionTrendDto>> GetExecutionTrendsAsync(string? programId = null, int days = 30, CancellationToken cancellationToken = default)
        {
            var allExecutions = await _unitOfWork.Executions.GetAllAsync(cancellationToken);
            var executions = allExecutions.AsQueryable();

            if (!string.IsNullOrEmpty(programId))
            {
                var programObjectId = ParseObjectId(programId);
                executions = executions.Where(e => e.ProgramId == programObjectId);
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var recentExecutions = executions.Where(e => e.StartedAt >= cutoffDate);

            var trends = new List<ExecutionTrendDto>();

            for (var i = 0; i < days; i++)
            {
                var date = DateTime.UtcNow.AddDays(-i).Date;
                var dayExecutions = recentExecutions.Where(e => e.StartedAt.Date == date);

                trends.Add(new ExecutionTrendDto
                {
                    Date = date,
                    ExecutionCount = dayExecutions.Count(),
                    SuccessfulCount = dayExecutions.Count(e => e.Status == "completed" && e.Results.ExitCode == 0),
                    FailedCount = dayExecutions.Count(e => e.Status == "failed" || e.Results.ExitCode != 0),
                    AverageExecutionTime = CalculateAverageExecutionTime(dayExecutions.ToList()),
                    TotalResourceUsage = (long)Math.Round(dayExecutions.Sum(e => e.ResourceUsage.CpuTime + e.ResourceUsage.MemoryUsed / 1024.0 / 1024.0))
                });
            }

            return trends.OrderBy(t => t.Date).ToList();
        }

        public async Task<List<ExecutionPerformanceDto>> GetExecutionPerformanceAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var executions = await _unitOfWork.Executions.GetByProgramIdAsync(programObjectId, cancellationToken);
            var executionsList = executions.ToList();

            var performance = new List<ExecutionPerformanceDto>
            {
                new ExecutionPerformanceDto
                {
                    ProgramId = programId,
                    ProgramName = "Program Name", // Would fetch from program
                    ExecutionCount = executionsList.Count,
                    SuccessRate = CalculateSuccessRate(executionsList),
                    AverageExecutionTime = CalculateAverageExecutionTime(executionsList),
                    AverageResourceUsage = CalculateAverageResourceUsage(executionsList),
                    LastExecution = executionsList.OrderByDescending(e => e.StartedAt).FirstOrDefault()?.StartedAt ?? DateTime.MinValue
                }
            };

            return performance;
        }

        public async Task<ExecutionSummaryDto> GetUserExecutionSummaryAsync(string userId, CancellationToken cancellationToken = default)
        {
            var executions = await _unitOfWork.Executions.GetByUserIdAsync(userId, cancellationToken);
            var executionsList = executions.ToList();

            var summary = new ExecutionSummaryDto
            {
                UserId = userId,
                TotalExecutions = executionsList.Count,
                SuccessfulExecutions = executionsList.Count(e => e.Status == "completed" && e.Results.ExitCode == 0),
                FailedExecutions = executionsList.Count(e => e.Status == "failed" || e.Results.ExitCode != 0),
                TotalCpuTime = executionsList.Sum(e => e.ResourceUsage.CpuTime),
                TotalMemoryUsed = executionsList.Sum(e => e.ResourceUsage.MemoryUsed),
                LastExecution = executionsList.OrderByDescending(e => e.StartedAt).FirstOrDefault()?.StartedAt,
                ProgramPerformance = new List<ExecutionPerformanceDto>()
            };

            // Group by program for performance breakdown
            var programGroups = executionsList.GroupBy(e => e.ProgramId);
            foreach (var group in programGroups)
            {
                summary.ProgramPerformance.Add(new ExecutionPerformanceDto
                {
                    ProgramId = group.Key.ToString(),
                    ProgramName = "Program Name", // Would fetch from program
                    ExecutionCount = group.Count(),
                    SuccessRate = CalculateSuccessRate(group.ToList()),
                    AverageExecutionTime = CalculateAverageExecutionTime(group.ToList()),
                    AverageResourceUsage = CalculateAverageResourceUsage(group.ToList()),
                    LastExecution = group.OrderByDescending(e => e.StartedAt).FirstOrDefault()?.StartedAt ?? DateTime.MinValue
                });
            }

            return summary;
        }

        #endregion

        #region Execution Configuration and Environment

        public async Task<ExecutionEnvironmentDto> GetExecutionEnvironmentAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            return new ExecutionEnvironmentDto
            {
                ProgramId = programId,
                Environment = GetDefaultEnvironmentVariables(program),
                ResourceLimits = await GetResourceLimitsAsync(programId, cancellationToken),
                Configuration = new Dictionary<string, object>(),
                LastUpdated = DateTime.UtcNow
            };
        }

        public async Task<bool> UpdateExecutionEnvironmentAsync(string programId, ExecutionEnvironmentUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Store environment configuration in program metadata
            _logger.LogInformation("Updating execution environment for program {ProgramId}", programId);

            return true;
        }

        public async Task<List<ExecutionTemplateDto>> GetExecutionTemplatesAsync(string? language = null, CancellationToken cancellationToken = default)
        {
            var templates = new List<ExecutionTemplateDto>();

            // Create templates for different languages
            var languages = string.IsNullOrEmpty(language) ? new[] { "python", "csharp", "javascript", "java" } : new[] { language };

            foreach (var lang in languages)
            {
                templates.Add(new ExecutionTemplateDto
                {
                    Id = $"{lang}-template",
                    Name = $"{lang.ToUpperInvariant()} Execution Template",
                    Description = $"Default execution template for {lang} programs",
                    Language = lang,
                    ParameterSchema = GetParameterSchemaForLanguage(lang),
                    DefaultEnvironment = GetDefaultEnvironmentVariables(lang),
                    DefaultResourceLimits = _defaultResourceLimits.GetValueOrDefault(lang, _defaultResourceLimits["default"])
                });
            }

            return templates;
        }

        public async Task<ExecutionValidationResult> ValidateExecutionRequestAsync(ProgramExecutionRequestDto dto, CancellationToken cancellationToken = default)
        {
            var result = new ExecutionValidationResult { IsValid = true };

            // Validate resource limits
            if (dto.ResourceLimits != null)
            {
                if (dto.ResourceLimits.MaxMemoryMb > 8192)
                {
                    result.Errors.Add("Memory limit cannot exceed 8GB");
                    result.IsValid = false;
                }

                if (dto.ResourceLimits.MaxExecutionTimeMinutes > 120)
                {
                    result.Errors.Add("Execution time limit cannot exceed 2 hours");
                    result.IsValid = false;
                }

                if (dto.ResourceLimits.MaxCpuPercentage > 100)
                {
                    result.Errors.Add("CPU percentage cannot exceed 100%");
                    result.IsValid = false;
                }
            }

            // Validate timeout
            if (dto.TimeoutMinutes > 120)
            {
                result.Errors.Add("Timeout cannot exceed 2 hours");
                result.IsValid = false;
            }

            return result;
        }

        #endregion

        #region Execution Queue and Scheduling

        public async Task<ExecutionQueueStatusDto> GetExecutionQueueStatusAsync(CancellationToken cancellationToken = default)
        {
            var runningExecutions = await _unitOfWork.Executions.GetRunningExecutionsAsync(cancellationToken);
            var queuedExecutions = new List<Execution>(); // TODO: Implement queued status

            return new ExecutionQueueStatusDto
            {
                QueueLength = queuedExecutions.Count,
                RunningExecutions = runningExecutions.Count(),
                MaxConcurrentExecutions = 10, // Would come from configuration
                AverageWaitTime = 0, // Would be calculated
                QueuedExecutions = _mapper.Map<List<ExecutionListDto>>(queuedExecutions)
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

            // TODO: Implement scheduling logic
            _logger.LogInformation("Scheduling execution for program {ProgramId} at {ScheduledTime}", programId, dto.ScheduledTime);

            // For now, create execution immediately
            var version = await GetExecutionVersionAsync(program, null, cancellationToken);
            return await CreateAndStartExecutionAsync(program, version, dto.Parameters, dto.Environment,
                dto.ResourceLimits, "code_execution", cancellationToken);
        }

        public async Task<bool> CancelScheduledExecutionAsync(string executionId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(executionId);
            var execution = await _unitOfWork.Executions.GetByIdAsync(objectId, cancellationToken);

            if (execution == null)
            {
                throw new KeyNotFoundException($"Execution with ID {executionId} not found.");
            }

            // TODO: Cancel scheduled execution
            return await _unitOfWork.Executions.UpdateStatusAsync(objectId, "cancelled", cancellationToken);
        }

        public async Task<List<ExecutionListDto>> GetScheduledExecutionsAsync(CancellationToken cancellationToken = default)
        {
            // TODO: Implement scheduled execution retrieval
            return new List<ExecutionListDto>();
        }

        #endregion

        #region Execution Cleanup and Maintenance

        public async Task<int> CleanupOldExecutionsAsync(int daysToKeep = 30, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting cleanup of executions older than {DaysToKeep} days", daysToKeep);

            var cleanedCount = await _unitOfWork.Executions.CleanupOldExecutionsAsync(daysToKeep, cancellationToken);

            _logger.LogInformation("Cleaned up {CleanedCount} old executions", cleanedCount);

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

            // TODO: Move execution files to archive storage
            _logger.LogInformation("Archiving execution {ExecutionId}", id);

            return true;
        }

        public async Task<List<ExecutionCleanupReportDto>> GetCleanupReportAsync(CancellationToken cancellationToken = default)
        {
            // TODO: Generate cleanup reports
            return new List<ExecutionCleanupReportDto>
            {
                new ExecutionCleanupReportDto
                {
                    CleanupDate = DateTime.UtcNow.AddDays(-1),
                    ExecutionsRemoved = 10,
                    SpaceFreed = 1024 * 1024 * 100, // 100MB
                    DaysRetained = 30,
                    RemovedByStatus = new Dictionary<string, int> { ["completed"] = 8, ["failed"] = 2 }
                }
            };
        }

        #endregion

        #region Execution Security and Validation

        public async Task<bool> ValidateExecutionPermissionsAsync(string programId, string userId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                return false;
            }

            // Check if user has execute permissions for the program
            // TODO: Implement proper permission checking
            return true;
        }

        public async Task<ExecutionSecurityScanResult> RunSecurityScanAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Implement security scanning
            return new ExecutionSecurityScanResult
            {
                IsSecure = true,
                SecurityIssues = new List<string>(),
                SecurityWarnings = new List<string>(),
                RiskLevel = 1,
                ScanDate = DateTime.UtcNow
            };
        }

        public async Task<bool> IsExecutionAllowedAsync(string programId, string userId, CancellationToken cancellationToken = default)
        {
            // Check permissions and resource quotas
            var hasPermissions = await ValidateExecutionPermissionsAsync(programId, userId, cancellationToken);

            if (!hasPermissions)
            {
                return false;
            }

            // Check if user has reached execution limits
            var userExecutions = await _unitOfWork.Executions.GetByUserIdAsync(userId, cancellationToken);
            var runningExecutions = userExecutions.Count(e => e.Status == "running");

            return runningExecutions < 5; // Max 5 concurrent executions per user
        }

        #endregion

        #region Private Helper Methods

        private async Task<Core.Models.Collaboration.Version> GetExecutionVersionAsync(Program program, string? versionId, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(versionId))
            {
                var versionObjectId = ParseObjectId(versionId);
                var version = await _unitOfWork.Versions.GetByIdAsync(versionObjectId, cancellationToken);
                if (version == null)
                {
                    throw new KeyNotFoundException($"Version {versionId} not found.");
                }
                return version;
            }

            // Get current version or latest
            if (!string.IsNullOrEmpty(program.CurrentVersion))
            {
                var currentVersionObjectId = ParseObjectId(program.CurrentVersion);
                var currentVersion = await _unitOfWork.Versions.GetByIdAsync(currentVersionObjectId, cancellationToken);
                if (currentVersion != null)
                {
                    return currentVersion;
                }
            }

            // Fall back to latest version
            var latestVersion = await _unitOfWork.Versions.GetLatestVersionForProgramAsync(program._ID, cancellationToken);
            if (latestVersion == null)
            {
                throw new InvalidOperationException($"No versions found for program {program._ID}.");
            }

            return latestVersion;
        }

        private async Task<ExecutionDto> CreateAndStartExecutionAsync(Program program, Core.Models.Collaboration.Version version,
            object parameters, Dictionary<string, string> environment, ExecutionResourceLimitsDto? resourceLimits,
            string executionType, CancellationToken cancellationToken)
        {
            var execution = new Execution
            {
                ProgramId = program._ID,
                VersionId = version._ID,
                UserId = "current-user-id", // Would come from auth context
                ExecutionType = executionType,
                StartedAt = DateTime.UtcNow,
                Status = "running",
                Parameters = parameters,
                Results = new ExecutionResults(),
                ResourceUsage = new ResourceUsage()
            };

            var createdExecution = await _unitOfWork.Executions.CreateAsync(execution, cancellationToken);

            // TODO: Start actual execution (container, process, web app deployment)
            await StartExecutionAsync(createdExecution, program, version, environment, resourceLimits, cancellationToken);

            return _mapper.Map<ExecutionDto>(createdExecution);
        }

        private async Task StartExecutionAsync(Execution execution, Program program, Core.Models.Collaboration.Version version,
            Dictionary<string, string> environment, ExecutionResourceLimitsDto? resourceLimits, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting execution {ExecutionId} for program {ProgramId}", execution._ID, program._ID);

            try
            {
                // TODO: Implement actual execution logic based on program type
                switch (program.Language.ToLower())
                {
                    case "python":
                        await StartPythonExecutionAsync(execution, program, version, environment, resourceLimits, cancellationToken);
                        break;
                    case "csharp":
                        await StartCSharpExecutionAsync(execution, program, version, environment, resourceLimits, cancellationToken);
                        break;
                    case "angular":
                    case "react":
                    case "vue":
                        await StartWebAppExecutionAsync(execution, program, version, environment, resourceLimits, cancellationToken);
                        break;
                    default:
                        await StartGenericExecutionAsync(execution, program, version, environment, resourceLimits, cancellationToken);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start execution {ExecutionId}", execution._ID);
                await _unitOfWork.Executions.UpdateStatusAsync(execution._ID, "failed", cancellationToken);
                await _unitOfWork.Executions.CompleteExecutionAsync(execution._ID, -1, string.Empty,
                    new List<string>(), ex.Message, cancellationToken);
            }
        }

        private async Task StartPythonExecutionAsync(Execution execution, Program program, Core.Models.Collaboration.Version version,
            Dictionary<string, string> environment, ExecutionResourceLimitsDto? resourceLimits, CancellationToken cancellationToken)
        {
            // TODO: Start Python execution in container
            _logger.LogInformation("Starting Python execution for {ProgramId}", program._ID);

            // Simulate execution completion for now
            await Task.Delay(1000, cancellationToken);
            await _unitOfWork.Executions.CompleteExecutionAsync(execution._ID, 0, "Python execution completed",
                new List<string> { "output.txt", "results.json" }, null, cancellationToken);
        }

        private async Task StartCSharpExecutionAsync(Execution execution, Program program, Core.Models.Collaboration.Version version,
            Dictionary<string, string> environment, ExecutionResourceLimitsDto? resourceLimits, CancellationToken cancellationToken)
        {
            // TODO: Start C# execution
            _logger.LogInformation("Starting C# execution for {ProgramId}", program._ID);

            await Task.Delay(1000, cancellationToken);
            await _unitOfWork.Executions.CompleteExecutionAsync(execution._ID, 0, "C# execution completed",
                new List<string> { "output.exe", "report.pdf" }, null, cancellationToken);
        }

        private async Task StartWebAppExecutionAsync(Execution execution, Program program, Core.Models.Collaboration.Version version,
            Dictionary<string, string> environment, ExecutionResourceLimitsDto? resourceLimits, CancellationToken cancellationToken)
        {
            // TODO: Deploy web application
            _logger.LogInformation("Starting web app deployment for {ProgramId}", program._ID);

            var webAppUrl = $"https://app-{execution._ID}.platform.com";

            await Task.Delay(2000, cancellationToken);

            // Update execution with web app URL
            execution.Results.WebAppUrl = webAppUrl;
            await _unitOfWork.Executions.UpdateAsync(execution._ID, execution, cancellationToken);

            await _unitOfWork.Executions.CompleteExecutionAsync(execution._ID, 0, "Web app deployed successfully",
                new List<string>(), null, cancellationToken);
        }

        private async Task StartGenericExecutionAsync(Execution execution, Program program, Core.Models.Collaboration.Version version,
            Dictionary<string, string> environment, ExecutionResourceLimitsDto? resourceLimits, CancellationToken cancellationToken)
        {
            // TODO: Start generic execution
            _logger.LogInformation("Starting generic execution for {ProgramId}", program._ID);

            await Task.Delay(1500, cancellationToken);
            await _unitOfWork.Executions.CompleteExecutionAsync(execution._ID, 0, "Execution completed",
                new List<string>(), null, cancellationToken);
        }

        private async Task EnrichExecutionDetailAsync(ExecutionDetailDto dto, Execution execution, CancellationToken cancellationToken)
        {
            // Enrich with program and user names
            var program = await _unitOfWork.Programs.GetByIdAsync(execution.ProgramId, cancellationToken);
            dto.ProgramName = program?.Name ?? "Unknown Program";
            dto.UserName = "User Name"; // Would fetch from user service

            // Enrich with version info
            var version = await _unitOfWork.Versions.GetByIdAsync(execution.VersionId, cancellationToken);
            dto.VersionNumber = version?.VersionNumber;

            // Enrich with output files
            dto.OutputFiles = await GetExecutionOutputFilesAsync(dto.Id, cancellationToken);

            // Enrich with recent logs
            dto.RecentLogs = await GetExecutionLogsAsync(dto.Id, 20, cancellationToken);

            // Enrich with environment
            dto.Environment = new ExecutionEnvironmentDto
            {
                ProgramId = execution.ProgramId.ToString(),
                Environment = new Dictionary<string, string>(),
                ResourceLimits = new ExecutionResourceLimitsDto(),
                Configuration = new Dictionary<string, object>(),
                LastUpdated = execution.StartedAt
            };

            // Enrich with web app info if applicable
            if (execution.ExecutionType == "web_app_deploy")
            {
                dto.WebAppUrl = execution.Results.WebAppUrl;
                dto.WebAppStatus = await GetWebApplicationStatusAsync(dto.Id, cancellationToken);
            }
        }

        private async Task EnrichExecutionListAsync(List<ExecutionListDto> dtos, CancellationToken cancellationToken)
        {
            foreach (var dto in dtos)
            {
                // TODO: Add program name, user name resolution, etc.
                dto.ProgramName = "Program Name"; // Would fetch from program service
                dto.UserName = "User Name"; // Would fetch from user service
                dto.HasError = !string.IsNullOrEmpty(dto.ResourceUsage.ToString()); // Simplified

                if (dto.ExitCode.HasValue && dto.ExitCode != 0)
                {
                    dto.HasError = true;
                }

                // Calculate duration if completed
                // This would be calculated from actual start/end times
                dto.Duration = 60.0; // Placeholder duration in seconds

                await Task.CompletedTask; // Placeholder
            }
        }

        private async Task CleanupExecutionResourcesAsync(ObjectId executionId, CancellationToken cancellationToken)
        {
            // TODO: Clean up execution files, containers, etc.
            _logger.LogInformation("Cleaning up resources for execution {ExecutionId}", executionId);
            await Task.CompletedTask;
        }

        private static Dictionary<string, ExecutionResourceLimitsDto> InitializeDefaultResourceLimits()
        {
            return new Dictionary<string, ExecutionResourceLimitsDto>
            {
                ["python"] = new ExecutionResourceLimitsDto
                {
                    MaxCpuPercentage = 80,
                    MaxMemoryMb = 1024,
                    MaxDiskMb = 2048,
                    MaxExecutionTimeMinutes = 30,
                    MaxConcurrentExecutions = 3
                },
                ["csharp"] = new ExecutionResourceLimitsDto
                {
                    MaxCpuPercentage = 90,
                    MaxMemoryMb = 2048,
                    MaxDiskMb = 4096,
                    MaxExecutionTimeMinutes = 60,
                    MaxConcurrentExecutions = 5
                },
                ["javascript"] = new ExecutionResourceLimitsDto
                {
                    MaxCpuPercentage = 70,
                    MaxMemoryMb = 512,
                    MaxDiskMb = 1024,
                    MaxExecutionTimeMinutes = 15,
                    MaxConcurrentExecutions = 5
                },
                ["default"] = new ExecutionResourceLimitsDto
                {
                    MaxCpuPercentage = 80,
                    MaxMemoryMb = 1024,
                    MaxDiskMb = 2048,
                    MaxExecutionTimeMinutes = 30,
                    MaxConcurrentExecutions = 3
                }
            };
        }

        private static Dictionary<string, string> GetDefaultEnvironmentVariables(Program program)
        {
            return GetDefaultEnvironmentVariables(program.Language);
        }

        private static Dictionary<string, string> GetDefaultEnvironmentVariables(string language)
        {
            var env = new Dictionary<string, string>
            {
                ["OUTPUTS"] = "/app/outputs",
                ["WORKSPACE"] = "/app",
                ["TEMP"] = "/tmp",
                ["EXECUTION_ID"] = "{{EXECUTION_ID}}",
                ["PROGRAM_ID"] = "{{PROGRAM_ID}}"
            };

            switch (language.ToLower())
            {
                case "python":
                    env["PYTHONPATH"] = "/app";
                    env["PYTHON_UNBUFFERED"] = "1";
                    break;
                case "csharp":
                    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
                    env["ASPNETCORE_ENVIRONMENT"] = "Production";
                    break;
                case "javascript":
                case "node":
                    env["NODE_ENV"] = "production";
                    env["NODE_PATH"] = "/app/node_modules";
                    break;
            }

            return env;
        }

        private static object GetParameterSchemaForLanguage(string language)
        {
            return new
            {
                type = "object",
                properties = new
                {
                    inputFile = new { type = "string", description = "Input file path" },
                    outputDir = new { type = "string", description = "Output directory path" },
                    parameters = new { type = "object", description = "Program-specific parameters" }
                }
            };
        }

        private static string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".pdf" => "application/pdf",
                ".xlsx" or ".xls" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".docx" or ".doc" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".csv" => "text/csv",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }

        private static double? CalculateExecutionProgress(Execution execution)
        {
            if (execution.Status == "completed" || execution.Status == "failed")
                return 100.0;

            if (execution.Status == "running")
            {
                // TODO: Calculate based on execution time and estimated duration
                var elapsed = DateTime.UtcNow - execution.StartedAt;
                var estimatedDuration = TimeSpan.FromMinutes(5); // Placeholder
                return Math.Min(elapsed.TotalMinutes / estimatedDuration.TotalMinutes * 100, 90);
            }

            return null;
        }

        private static string? GetCurrentExecutionStep(Execution execution)
        {
            return execution.Status switch
            {
                "running" => "Executing program",
                "completed" => "Completed",
                "failed" => "Failed",
                "stopped" => "Stopped",
                _ => null
            };
        }

        private static string? GetStatusMessage(Execution execution)
        {
            return execution.Status switch
            {
                "running" => "Execution in progress",
                "completed" => "Execution completed successfully",
                "failed" => execution.Results.Error ?? "Execution failed",
                "stopped" => "Execution was stopped",
                _ => null
            };
        }

        private static double CalculateAverageExecutionTime(List<Execution> executions)
        {
            var completedExecutions = executions.Where(e => e.CompletedAt.HasValue).ToList();

            if (!completedExecutions.Any())
                return 0;

            var totalSeconds = completedExecutions.Sum(e => (e.CompletedAt!.Value - e.StartedAt).TotalSeconds);
            return totalSeconds / completedExecutions.Count;
        }

        private static double CalculateSuccessRate(List<Execution> executions)
        {
            if (!executions.Any())
                return 0;

            var successfulExecutions = executions.Count(e => e.Status == "completed" && e.Results.ExitCode == 0);
            return (double)successfulExecutions / executions.Count * 100;
        }

        private static double CalculateAverageResourceUsage(List<Execution> executions)
        {
            if (!executions.Any())
                return 0;

            return executions.Average(e => e.ResourceUsage.CpuTime + e.ResourceUsage.MemoryUsed / 1024.0 / 1024.0);
        }

        #endregion
    }
}