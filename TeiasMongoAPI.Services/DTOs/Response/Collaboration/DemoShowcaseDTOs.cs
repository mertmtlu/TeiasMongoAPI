using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Response.Collaboration
{
    // Root response for GET /api/demoshowcase - Public nested structure
    public class PublicDemoShowcaseResponse
    {
        public List<TabGroupDto> Tabs { get; set; } = new();
    }

    public class TabGroupDto
    {
        public required string TabName { get; set; }
        public List<PrimaryGroupDto> PrimaryGroups { get; set; } = new();
    }

    public class PrimaryGroupDto
    {
        public required string PrimaryGroupName { get; set; }
        public List<SecondaryGroupDto> SecondaryGroups { get; set; } = new();
    }

    public class SecondaryGroupDto
    {
        public required string SecondaryGroupName { get; set; }
        public List<DemoShowcaseItemDto> Items { get; set; } = new();
        public List<TertiaryGroupDto> TertiaryGroups { get; set; } = new();
    }

    public class TertiaryGroupDto
    {
        public required string TertiaryGroupName { get; set; }
        public List<DemoShowcaseItemDto> Items { get; set; } = new();
    }

    public class DemoShowcaseItemDto
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public string? IconUrl { get; set; }
        public required string AppId { get; set; }
        public required string AppType { get; set; }
        public required string VideoPath { get; set; }
        public DateTime CreatedAt { get; set; }

        // NEW FIELDS
        public required string CreatorFullName { get; set; }
        public bool HasPublicUiComponent { get; set; }
    }

    // Legacy Public DTO for /demo page (deprecated - kept for compatibility)
    public class DemoShowcasePublicDto
    {
        public required string Id { get; set; }
        public required string Group { get; set; }
        public required string VideoPath { get; set; }

        // From associated app
        public required string AppType { get; set; }
        public required string AppId { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public required string Creator { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Admin DTO for management
    public class DemoShowcaseDto
    {
        public required string Id { get; set; }
        public required string AssociatedAppId { get; set; }
        public required string AppType { get; set; }
        public required string Tab { get; set; }
        public required string PrimaryGroup { get; set; }
        public required string SecondaryGroup { get; set; }
        public string? TertiaryGroup { get; set; }
        public required string VideoPath { get; set; }
    }

    // UI Component response DTO
    public class UiComponentResponseDto
    {
        public required string Id { get; set; }
        public required string ProgramId { get; set; }
        public object? Schema { get; set; }
        public object? Configuration { get; set; }
    }

    // Execution response DTO
    public class ExecutionResponseDto
    {
        public required string ExecutionId { get; set; }
        public required string Status { get; set; }
        public object? Result { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // Public execution monitoring DTOs
    public class PublicExecutionDetailDto
    {
        public required string ExecutionId { get; set; }
        public required string Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public object? Parameters { get; set; }
        public int? ExitCode { get; set; }
        public string? ErrorMessage { get; set; }
        public double? Duration { get; set; }
    }

    public class PublicExecutionLogsDto
    {
        public required string ExecutionId { get; set; }
        public List<string> Logs { get; set; } = new();
        public int TotalLines { get; set; }
    }

    public class PublicExecutionFilesDto
    {
        public required string ExecutionId { get; set; }
        public List<string> Files { get; set; } = new();
        public int TotalFiles { get; set; }
    }

    // Video upload response
    public class VideoUploadResponseDto
    {
        public required string VideoPath { get; set; }
        public long FileSize { get; set; }
    }

    // Available apps for dropdown
    public class AvailableAppsDto
    {
        public List<AppOptionDto> Programs { get; set; } = new();
        public List<AppOptionDto> Workflows { get; set; } = new();
        public List<AppOptionDto> RemoteApps { get; set; } = new();
    }

    public class AppOptionDto
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
    }

    // Remote app launch response
    public class RemoteAppLaunchResponseDto
    {
        public required string RedirectUrl { get; set; }
        public bool RequiresSso { get; set; }
    }

    // Extended execution details with resource usage and results
    public class PublicExecutionDetailExtendedDto
    {
        public required string ExecutionId { get; set; }
        public required string Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public object? Parameters { get; set; }
        public string? ErrorMessage { get; set; }
        public double? Duration { get; set; }
        public ExecutionResourceUsageExtendedDto? ResourceUsage { get; set; }
        public ExecutionResultExtendedDto? Result { get; set; }
    }

    public class ExecutionResourceUsageExtendedDto
    {
        public double MaxMemoryUsedMb { get; set; }
        public double MaxCpuPercent { get; set; }
        public double ExecutionTimeMinutes { get; set; }
    }

    public class ExecutionResultExtendedDto
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorOutput { get; set; } = string.Empty;
    }

    // Execution stop response
    public class ExecutionStopResponseDto
    {
        public bool Success { get; set; }
    }

    // File download response for ZIP
    public class FileDownloadResponseDto
    {
        public required Stream FileStream { get; set; }
        public required string FileName { get; set; }
    }
}
