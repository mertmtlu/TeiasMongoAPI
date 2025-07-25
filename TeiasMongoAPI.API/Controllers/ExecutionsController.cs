﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ExecutionsController : BaseController
    {
        private readonly IExecutionService _executionService;

        public ExecutionsController(
            IExecutionService executionService,
            ILogger<ExecutionsController> logger)
            : base(logger)
        {
            _executionService = executionService;
        }

        #region Basic CRUD Operations

        /// <summary>
        /// Get all executions with pagination
        /// </summary>
        [HttpGet]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ExecutionListDto>>>> GetAll(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetAllAsync(pagination, cancellationToken);
            }, "Get all executions");
        }

        /// <summary>
        /// Get execution by ID
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<ExecutionDetailDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetByIdAsync(id, cancellationToken);
            }, $"Get execution {id}");
        }

        /// <summary>
        /// Delete execution
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        [AuditLog("DeleteExecution")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.DeleteAsync(id, cancellationToken);
            }, $"Delete execution {id}");
        }

        #endregion

        #region Execution Filtering and History

        /// <summary>
        /// Advanced execution search
        /// </summary>
        [HttpPost("search")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ExecutionListDto>>>> Search(
            [FromBody] ExecutionSearchDto searchDto,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _executionService.SearchAsync(searchDto, pagination, cancellationToken);
            }, "Search executions");
        }

        /// <summary>
        /// Get executions by program
        /// </summary>
        [HttpGet("programs/{programId}")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ExecutionListDto>>>> GetByProgram(
            string programId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetByProgramIdAsync(programId, pagination, cancellationToken);
            }, $"Get executions by program {programId}");
        }

        /// <summary>
        /// Get executions by version
        /// </summary>
        [HttpGet("versions/{versionId}")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ExecutionListDto>>>> GetByVersion(
            string versionId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(versionId, "versionId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetByVersionIdAsync(versionId, pagination, cancellationToken);
            }, $"Get executions by version {versionId}");
        }

        /// <summary>
        /// Get executions by user
        /// </summary>
        [HttpGet("users/{userId}")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ExecutionListDto>>>> GetByUser(
            string userId,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(userId, "userId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetByUserIdAsync(userId, pagination, cancellationToken);
            }, $"Get executions by user {userId}");
        }

        /// <summary>
        /// Get executions by status
        /// </summary>
        [HttpGet("status/{status}")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ExecutionListDto>>>> GetByStatus(
            string status,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetByStatusAsync(status, pagination, cancellationToken);
            }, $"Get executions by status {status}");
        }

        /// <summary>
        /// Get running executions
        /// </summary>
        [HttpGet("running")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ExecutionListDto>>>> GetRunningExecutions(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetRunningExecutionsAsync(pagination, cancellationToken);
            }, "Get running executions");
        }

        /// <summary>
        /// Get completed executions
        /// </summary>
        [HttpGet("completed")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ExecutionListDto>>>> GetCompletedExecutions(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetCompletedExecutionsAsync(pagination, cancellationToken);
            }, "Get completed executions");
        }

        /// <summary>
        /// Get failed executions
        /// </summary>
        [HttpGet("failed")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<PagedResponse<ExecutionListDto>>>> GetFailedExecutions(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetFailedExecutionsAsync(pagination, cancellationToken);
            }, "Get failed executions");
        }

        /// <summary>
        /// Get recent executions
        /// </summary>
        [HttpGet("recent")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<List<ExecutionListDto>>>> GetRecentExecutions(
            [FromQuery] int count = 10,
            CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return ValidationError<List<ExecutionListDto>>("Count must be greater than 0");
            }

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetRecentExecutionsAsync(count, cancellationToken);
            }, $"Get recent executions (count: {count})");
        }

        #endregion

        #region Program Execution Operations (Core Workflow)

        /// <summary>
        /// Execute program using current version
        /// </summary>
        [HttpPost("run/programs/{programId}")]
        [RequirePermission(UserPermissions.CreateExecutions)]
        [AuditLog("ExecuteProgram")]
        public async Task<ActionResult<ApiResponse<ExecutionDto>>> ExecuteProgram(
            string programId,
            [FromBody] ProgramExecutionRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ExecutionDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.ExecuteProgramAsync(programId, CurrentUserId, dto, cancellationToken);
            }, $"Execute program {programId}");
        }

        /// <summary>
        /// Execute specific version
        /// </summary>
        [HttpPost("run/versions/{versionId}")]
        [RequirePermission(UserPermissions.CreateExecutions)]
        [AuditLog("ExecuteVersion")]
        public async Task<ActionResult<ApiResponse<ExecutionDto>>> ExecuteVersion(
            string versionId,
            [FromBody] VersionExecutionRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(versionId, "versionId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ExecutionDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.ExecuteVersionAsync(versionId, dto, CurrentUserId, cancellationToken);
            }, $"Execute version {versionId}");
        }

        /// <summary>
        /// Execute with advanced parameters
        /// </summary>
        [HttpPost("run/advanced")]
        [RequirePermission(UserPermissions.CreateExecutions)]
        [AuditLog("ExecuteWithParameters")]
        public async Task<ActionResult<ApiResponse<ExecutionDto>>> ExecuteWithParameters(
            [FromBody] ExecutionParametersDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<ExecutionDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.ExecuteWithParametersAsync(dto.ProgramId, CurrentUserId, dto, cancellationToken);
            }, "Execute with advanced parameters");
        }

        #endregion

        #region Execution Control and Monitoring

        /// <summary>
        /// Stop execution
        /// </summary>
        [HttpPost("{id}/stop")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        [AuditLog("StopExecution")]
        public async Task<ActionResult<ApiResponse<bool>>> StopExecution(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.StopExecutionAsync(id, cancellationToken);
            }, $"Stop execution {id}");
        }

        /// <summary>
        /// Pause execution
        /// </summary>
        [HttpPost("{id}/pause")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        [AuditLog("PauseExecution")]
        public async Task<ActionResult<ApiResponse<bool>>> PauseExecution(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.PauseExecutionAsync(id, cancellationToken);
            }, $"Pause execution {id}");
        }

        /// <summary>
        /// Resume execution
        /// </summary>
        [HttpPost("{id}/resume")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        [AuditLog("ResumeExecution")]
        public async Task<ActionResult<ApiResponse<bool>>> ResumeExecution(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.ResumeExecutionAsync(id, cancellationToken);
            }, $"Resume execution {id}");
        }

        /// <summary>
        /// Get execution status
        /// </summary>
        [HttpGet("{id}/status")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<ExecutionStatusDto>>> GetExecutionStatus(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetExecutionStatusAsync(id, cancellationToken);
            }, $"Get execution status {id}");
        }

        /// <summary>
        /// Get execution output stream
        /// </summary>
        [HttpGet("{id}/output")]
        [RequirePermission(UserPermissions.ViewExecutionResults)]
        public async Task<ActionResult<ApiResponse<string>>> GetExecutionOutputStream(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetExecutionOutputStreamAsync(id, cancellationToken);
            }, $"Get execution output stream {id}");
        }

        /// <summary>
        /// Get execution logs
        /// </summary>
        [HttpGet("{id}/logs")]
        [RequirePermission(UserPermissions.ViewExecutionResults)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetExecutionLogs(
            string id,
            [FromQuery] int lines = 100,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (lines <= 0)
            {
                return ValidationError<List<string>>("Lines must be greater than 0");
            }

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetExecutionLogsAsync(id, lines, cancellationToken);
            }, $"Get execution logs {id}");
        }

        #endregion

        #region Execution Results Management

        /// <summary>
        /// Get execution result
        /// </summary>
        [HttpGet("{id}/result")]
        [RequirePermission(UserPermissions.ViewExecutionResults)]
        public async Task<ActionResult<ApiResponse<ExecutionResultDto>>> GetExecutionResult(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetExecutionResultAsync(id, cancellationToken);
            }, $"Get execution result {id}");
        }



        #endregion

        #region Web Application Execution

        /// <summary>
        /// Deploy web application
        /// </summary>
        [HttpPost("programs/{programId}/deploy-webapp")]
        [RequirePermission(UserPermissions.CreateExecutions)]
        [AuditLog("DeployWebApplication")]
        public async Task<ActionResult<ApiResponse<ExecutionDto>>> DeployWebApplication(
            string programId,
            [FromBody] WebAppDeploymentRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ExecutionDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.DeployWebApplicationAsync(programId, dto, cancellationToken);
            }, $"Deploy web application {programId}");
        }

        /// <summary>
        /// Get web application URL
        /// </summary>
        [HttpGet("{id}/webapp-url")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<string>>> GetWebApplicationUrl(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetWebApplicationUrlAsync(id, cancellationToken);
            }, $"Get web application URL {id}");
        }

        /// <summary>
        /// Get web application status
        /// </summary>
        [HttpGet("{id}/webapp-status")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<WebAppStatusDto>>> GetWebApplicationStatus(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetWebApplicationStatusAsync(id, cancellationToken);
            }, $"Get web application status {id}");
        }

        /// <summary>
        /// Restart web application
        /// </summary>
        [HttpPost("{id}/restart-webapp")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        [AuditLog("RestartWebApplication")]
        public async Task<ActionResult<ApiResponse<bool>>> RestartWebApplication(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.RestartWebApplicationAsync(id, cancellationToken);
            }, $"Restart web application {id}");
        }

        /// <summary>
        /// Stop web application
        /// </summary>
        [HttpPost("{id}/stop-webapp")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        [AuditLog("StopWebApplication")]
        public async Task<ActionResult<ApiResponse<bool>>> StopWebApplication(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.StopWebApplicationAsync(id, cancellationToken);
            }, $"Stop web application {id}");
        }

        #endregion

        #region Resource Management and Monitoring

        /// <summary>
        /// Get resource usage for execution
        /// </summary>
        [HttpGet("{id}/resources")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<ExecutionResourceUsageDto>>> GetResourceUsage(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetResourceUsageAsync(id, cancellationToken);
            }, $"Get resource usage {id}");
        }

        /// <summary>
        /// Update resource usage for execution
        /// </summary>
        [HttpPut("{id}/resources")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateResourceUsage(
            string id,
            [FromBody] ExecutionResourceUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.UpdateResourceUsageAsync(id, dto, cancellationToken);
            }, $"Update resource usage {id}");
        }

        /// <summary>
        /// Get resource trends for program
        /// </summary>
        [HttpGet("programs/{programId}/resource-trends")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<List<ExecutionResourceTrendDto>>>> GetResourceTrends(
            string programId,
            [FromQuery] int days = 7,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            if (days <= 0)
            {
                return ValidationError<List<ExecutionResourceTrendDto>>("Days must be greater than 0");
            }

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetResourceTrendsAsync(programId, days, cancellationToken);
            }, $"Get resource trends for program {programId}");
        }

        /// <summary>
        /// Get resource limits for program
        /// </summary>
        [HttpGet("programs/{programId}/resource-limits")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<ExecutionResourceLimitsDto>>> GetResourceLimits(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetResourceLimitsAsync(programId, cancellationToken);
            }, $"Get resource limits for program {programId}");
        }

        /// <summary>
        /// Update resource limits for program
        /// </summary>
        [HttpPut("programs/{programId}/resource-limits")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        [AuditLog("UpdateResourceLimits")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateResourceLimits(
            string programId,
            [FromBody] ExecutionResourceLimitsUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.UpdateResourceLimitsAsync(programId, dto, cancellationToken);
            }, $"Update resource limits for program {programId}");
        }

        #endregion

        #region Execution Statistics and Analytics

        /// <summary>
        /// Get execution statistics
        /// </summary>
        [HttpGet("statistics")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<ExecutionStatsDto>>> GetExecutionStats(
            [FromQuery] ExecutionStatsFilterDto? filter = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetExecutionStatsAsync(filter, cancellationToken);
            }, "Get execution statistics");
        }

        /// <summary>
        /// Get execution trends
        /// </summary>
        [HttpGet("trends")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<List<ExecutionTrendDto>>>> GetExecutionTrends(
            [FromQuery] string? programId = null,
            [FromQuery] int days = 30,
            CancellationToken cancellationToken = default)
        {
            if (days <= 0)
            {
                return ValidationError<List<ExecutionTrendDto>>("Days must be greater than 0");
            }

            // Validate programId if provided
            if (!string.IsNullOrEmpty(programId))
            {
                var objectIdResult = ParseObjectId(programId, "programId");
                if (objectIdResult.Result != null) return objectIdResult.Result!;
            }

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetExecutionTrendsAsync(programId, days, cancellationToken);
            }, "Get execution trends");
        }

        /// <summary>
        /// Get execution performance for program
        /// </summary>
        [HttpGet("programs/{programId}/performance")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<List<ExecutionPerformanceDto>>>> GetExecutionPerformance(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetExecutionPerformanceAsync(programId, cancellationToken);
            }, $"Get execution performance for program {programId}");
        }

        /// <summary>
        /// Get user execution summary
        /// </summary>
        [HttpGet("users/{userId}/summary")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<ExecutionSummaryDto>>> GetUserExecutionSummary(
            string userId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(userId, "userId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetUserExecutionSummaryAsync(userId, cancellationToken);
            }, $"Get execution summary for user {userId}");
        }

        #endregion

        #region Execution Configuration and Environment

        /// <summary>
        /// Get execution environment for program
        /// </summary>
        [HttpGet("programs/{programId}/environment")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<ExecutionEnvironmentDto>>> GetExecutionEnvironment(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetExecutionEnvironmentAsync(programId, cancellationToken);
            }, $"Get execution environment for program {programId}");
        }

        /// <summary>
        /// Update execution environment for program
        /// </summary>
        [HttpPut("programs/{programId}/environment")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        [AuditLog("UpdateExecutionEnvironment")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateExecutionEnvironment(
            string programId,
            [FromBody] ExecutionEnvironmentUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<bool>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.UpdateExecutionEnvironmentAsync(programId, dto, cancellationToken);
            }, $"Update execution environment for program {programId}");
        }

        /// <summary>
        /// Get execution templates
        /// </summary>
        [HttpGet("templates")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<List<ExecutionTemplateDto>>>> GetExecutionTemplates(
            [FromQuery] string? language = null,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetExecutionTemplatesAsync(language, cancellationToken);
            }, "Get execution templates");
        }

        /// <summary>
        /// Validate execution request
        /// </summary>
        [HttpPost("validate")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<ExecutionValidationResult>>> ValidateExecutionRequest(
            [FromBody] ProgramExecutionRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<ExecutionValidationResult>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.ValidateExecutionRequestAsync(dto, cancellationToken);
            }, "Validate execution request");
        }

        #endregion

        #region Execution Queue and Scheduling

        /// <summary>
        /// Get execution queue status
        /// </summary>
        [HttpGet("queue/status")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<ExecutionQueueStatusDto>>> GetExecutionQueueStatus(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetExecutionQueueStatusAsync(cancellationToken);
            }, "Get execution queue status");
        }

        /// <summary>
        /// Schedule execution for program
        /// </summary>
        [HttpPost("programs/{programId}/schedule")]
        [RequirePermission(UserPermissions.CreateExecutions)]
        [AuditLog("ScheduleExecution")]
        public async Task<ActionResult<ApiResponse<ExecutionDto>>> ScheduleExecution(
            string programId,
            [FromBody] ExecutionScheduleRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<ExecutionDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.ScheduleExecutionAsync(programId, dto, cancellationToken);
            }, $"Schedule execution for program {programId}");
        }

        /// <summary>
        /// Cancel scheduled execution
        /// </summary>
        [HttpPost("{id}/cancel-scheduled")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        [AuditLog("CancelScheduledExecution")]
        public async Task<ActionResult<ApiResponse<bool>>> CancelScheduledExecution(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.CancelScheduledExecutionAsync(id, cancellationToken);
            }, $"Cancel scheduled execution {id}");
        }

        /// <summary>
        /// Get scheduled executions
        /// </summary>
        [HttpGet("scheduled")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<List<ExecutionListDto>>>> GetScheduledExecutions(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetScheduledExecutionsAsync(cancellationToken);
            }, "Get scheduled executions");
        }

        #endregion

        #region Project Analysis and Validation

        /// <summary>
        /// Get supported programming languages
        /// </summary>
        [HttpGet("supported-languages")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetSupportedLanguages(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetSupportedLanguagesAsync(cancellationToken);
            }, "Get supported languages");
        }

        /// <summary>
        /// Analyze project structure
        /// </summary>
        [HttpPost("programs/{programId}/analyze")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<ProjectStructureAnalysisDto>>> AnalyzeProject(
            string programId,
            [FromQuery] string? versionId = null,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate versionId if provided
            if (!string.IsNullOrEmpty(versionId))
            {
                var versionObjectIdResult = ParseObjectId(versionId, "versionId");
                if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;
            }

            return await ExecuteAsync(async () =>
            {
                return await _executionService.AnalyzeProjectStructureAsync(programId, versionId, cancellationToken);
            }, $"Analyze project {programId}");
        }

        /// <summary>
        /// Validate project for execution
        /// </summary>
        [HttpPost("programs/{programId}/validate")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<ProjectValidationResultDto>>> ValidateProject(
            string programId,
            [FromQuery] string? versionId = null,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate versionId if provided
            if (!string.IsNullOrEmpty(versionId))
            {
                var versionObjectIdResult = ParseObjectId(versionId, "versionId");
                if (versionObjectIdResult.Result != null) return versionObjectIdResult.Result!;
            }

            return await ExecuteAsync(async () =>
            {
                return await _executionService.ValidateProjectAsync(programId, versionId, cancellationToken);
            }, $"Validate project {programId}");
        }

        #endregion

        #region Execution Cleanup and Maintenance

        /// <summary>
        /// Cleanup old executions
        /// </summary>
        [HttpPost("cleanup")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        [AuditLog("CleanupOldExecutions")]
        public async Task<ActionResult<ApiResponse<int>>> CleanupOldExecutions(
            [FromQuery] int daysToKeep = 30,
            CancellationToken cancellationToken = default)
        {
            if (daysToKeep <= 0)
            {
                return ValidationError<int>("Days to keep must be greater than 0");
            }

            return await ExecuteAsync(async () =>
            {
                return await _executionService.CleanupOldExecutionsAsync(daysToKeep, cancellationToken);
            }, $"Cleanup old executions (keep {daysToKeep} days)");
        }

        /// <summary>
        /// Archive execution
        /// </summary>
        [HttpPost("{id}/archive")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        [AuditLog("ArchiveExecution")]
        public async Task<ActionResult<ApiResponse<bool>>> ArchiveExecution(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.ArchiveExecutionAsync(id, cancellationToken);
            }, $"Archive execution {id}");
        }

        /// <summary>
        /// Get cleanup report
        /// </summary>
        [HttpGet("cleanup-report")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<List<ExecutionCleanupReportDto>>>> GetCleanupReport(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _executionService.GetCleanupReportAsync(cancellationToken);
            }, "Get cleanup report");
        }

        #endregion

        #region Execution Security and Validation

        /// <summary>
        /// Run security scan on program
        /// </summary>
        [HttpPost("programs/{programId}/security-scan")]
        [RequirePermission(UserPermissions.ManageExecutions)]
        public async Task<ActionResult<ApiResponse<ExecutionSecurityScanResult>>> RunSecurityScan(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.RunSecurityScanAsync(programId, cancellationToken);
            }, $"Run security scan for program {programId}");
        }

        /// <summary>
        /// Check if execution is allowed for user on program
        /// </summary>
        [HttpGet("programs/{programId}/users/{userId}/access-check")]
        [RequirePermission(UserPermissions.ViewExecutions)]
        public async Task<ActionResult<ApiResponse<bool>>> IsExecutionAllowed(
            string programId,
            string userId,
            CancellationToken cancellationToken = default)
        {
            var programObjectIdResult = ParseObjectId(programId, "programId");
            if (programObjectIdResult.Result != null) return programObjectIdResult.Result!;

            var userObjectIdResult = ParseObjectId(userId, "userId");
            if (userObjectIdResult.Result != null) return userObjectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _executionService.IsExecutionAllowedAsync(programId, userId, cancellationToken);
            }, $"Check execution access for user {userId} on program {programId}");
        }

        #endregion
    }
}