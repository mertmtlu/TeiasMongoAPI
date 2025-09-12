using MongoDB.Bson;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IExecutionService
    {
        // Basic CRUD Operations
        Task<ExecutionDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<ExecutionListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ExecutionListDto>> SearchAsync(ExecutionSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

        // Execution Filtering and History
        Task<PagedResponse<ExecutionListDto>> GetByProgramIdAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ExecutionListDto>> GetByVersionIdAsync(string versionId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ExecutionListDto>> GetByUserIdAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ExecutionListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ExecutionListDto>> GetRunningExecutionsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ExecutionListDto>> GetCompletedExecutionsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<ExecutionListDto>> GetFailedExecutionsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<List<ExecutionListDto>> GetRecentExecutionsAsync(int count = 10, CancellationToken cancellationToken = default);

        // Program Execution Operations
        Task<ExecutionDto> ExecuteProgramAsync(string programId, ObjectId? currentUser, ProgramExecutionRequestDto dto, CancellationToken cancellationToken = default);
        Task<ExecutionDto> ExecuteVersionAsync(string versionId, VersionExecutionRequestDto dto, ObjectId? currentUserId, CancellationToken cancellationToken = default);
        Task<ExecutionDto> ExecuteWithParametersAsync(string programId, ObjectId? currentUser, ExecutionParametersDto dto, CancellationToken cancellationToken = default);

        // Execution Control and Monitoring
        Task<bool> StopExecutionAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> PauseExecutionAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> ResumeExecutionAsync(string id, CancellationToken cancellationToken = default);
        Task<ExecutionStatusDto> GetExecutionStatusAsync(string id, CancellationToken cancellationToken = default);
        Task<string> GetExecutionOutputStreamAsync(string id, CancellationToken cancellationToken = default);
        Task<List<string>> GetExecutionLogsAsync(string id, int lines = 100, CancellationToken cancellationToken = default);

        // Execution Results Management
        Task<ExecutionResultDto> GetExecutionResultAsync(string id, CancellationToken cancellationToken = default);

        // Web Application Execution
        Task<ExecutionDto> DeployWebApplicationAsync(string programId, WebAppDeploymentRequestDto dto, ObjectId? currentUserId = null, CancellationToken cancellationToken = default);
        Task<string> GetWebApplicationUrlAsync(string executionId, CancellationToken cancellationToken = default);
        Task<WebAppStatusDto> GetWebApplicationStatusAsync(string executionId, CancellationToken cancellationToken = default);
        Task<bool> RestartWebApplicationAsync(string executionId, CancellationToken cancellationToken = default);
        Task<bool> StopWebApplicationAsync(string executionId, CancellationToken cancellationToken = default);

        // Resource Management and Monitoring
        Task<ExecutionResourceUsageDto> GetResourceUsageAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> UpdateResourceUsageAsync(string id, ExecutionResourceUpdateDto dto, CancellationToken cancellationToken = default);
        Task<List<ExecutionResourceTrendDto>> GetResourceTrendsAsync(string programId, int days = 7, CancellationToken cancellationToken = default);
        Task<ExecutionResourceLimitsDto> GetResourceLimitsAsync(string programId, CancellationToken cancellationToken = default);
        Task<bool> UpdateResourceLimitsAsync(string programId, ExecutionResourceLimitsUpdateDto dto, CancellationToken cancellationToken = default);

        // Execution Statistics and Analytics
        Task<ExecutionStatsDto> GetExecutionStatsAsync(ExecutionStatsFilterDto? filter = null, CancellationToken cancellationToken = default);
        Task<List<ExecutionTrendDto>> GetExecutionTrendsAsync(string? programId = null, int days = 30, CancellationToken cancellationToken = default);
        Task<List<ExecutionPerformanceDto>> GetExecutionPerformanceAsync(string programId, CancellationToken cancellationToken = default);
        Task<ExecutionSummaryDto> GetUserExecutionSummaryAsync(string userId, CancellationToken cancellationToken = default);

        // Execution Configuration and Environment
        Task<ExecutionEnvironmentDto> GetExecutionEnvironmentAsync(string programId, CancellationToken cancellationToken = default);
        Task<bool> UpdateExecutionEnvironmentAsync(string programId, ExecutionEnvironmentUpdateDto dto, CancellationToken cancellationToken = default);
        Task<List<ExecutionTemplateDto>> GetExecutionTemplatesAsync(string? language = null, CancellationToken cancellationToken = default);
        Task<ExecutionValidationResult> ValidateExecutionRequestAsync(ProgramExecutionRequestDto dto, CancellationToken cancellationToken = default);

        // Execution Queue and Scheduling
        Task<ExecutionQueueStatusDto> GetExecutionQueueStatusAsync(CancellationToken cancellationToken = default);
        Task<ExecutionDto> ScheduleExecutionAsync(string programId, ExecutionScheduleRequestDto dto, ObjectId? currentUserId = null, CancellationToken cancellationToken = default);
        Task<bool> CancelScheduledExecutionAsync(string executionId, CancellationToken cancellationToken = default);
        Task<List<ExecutionListDto>> GetScheduledExecutionsAsync(CancellationToken cancellationToken = default);

        // Execution Cleanup and Maintenance
        Task<int> CleanupOldExecutionsAsync(int daysToKeep = 30, CancellationToken cancellationToken = default);
        Task<bool> ArchiveExecutionAsync(string id, CancellationToken cancellationToken = default);
        Task<List<ExecutionCleanupReportDto>> GetCleanupReportAsync(CancellationToken cancellationToken = default);

        // Execution Security and Validation
        Task<bool> ValidateExecutionPermissionsAsync(string programId, string userId, CancellationToken cancellationToken = default);
        Task<ExecutionSecurityScanResult> RunSecurityScanAsync(string programId, CancellationToken cancellationToken = default);
        Task<bool> IsExecutionAllowedAsync(string programId, string userId, CancellationToken cancellationToken = default);


        Task<List<string>> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default);
        Task<ProjectStructureAnalysisDto> AnalyzeProjectStructureAsync(string programId, string? versionId = null, CancellationToken cancellationToken = default);
        Task<ProjectValidationResultDto> ValidateProjectAsync(string programId, string? versionId = null, CancellationToken cancellationToken = default);
    }
}