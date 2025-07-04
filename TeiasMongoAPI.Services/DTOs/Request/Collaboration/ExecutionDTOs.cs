using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Collaboration
{
    public class ProgramExecutionRequestDto
    {
        public object Parameters { get; set; } = new object();
        public Dictionary<string, string> Environment { get; set; } = new();
        public ExecutionResourceLimitsDto? ResourceLimits { get; set; }
        public bool SaveResults { get; set; } = true;
        public int TimeoutMinutes { get; set; } = 30;
    }

    public class VersionExecutionRequestDto
    {
        public object Parameters { get; set; } = new object();
        public Dictionary<string, string> Environment { get; set; } = new();
        public ExecutionResourceLimitsDto? ResourceLimits { get; set; }
        public bool SaveResults { get; set; } = true;
        public int TimeoutMinutes { get; set; } = 30;
    }

    public class ExecutionParametersDto
    {
        [Required]
        public required string ProgramId { get; set; }

        public string? VersionId { get; set; }
        public object Parameters { get; set; } = new object();
        public Dictionary<string, string> Environment { get; set; } = new();
        public ExecutionResourceLimitsDto? ResourceLimits { get; set; }
        public bool SaveResults { get; set; } = true;
        public int TimeoutMinutes { get; set; } = 30;
        public string? ExecutionName { get; set; }
    }

    public class ExecutionSearchDto
    {
        public string? ProgramId { get; set; }
        public string? VersionId { get; set; }
        public string? UserId { get; set; }
        public string? Status { get; set; }
        public string? ExecutionType { get; set; }
        public DateTime? StartedFrom { get; set; }
        public DateTime? StartedTo { get; set; }
        public DateTime? CompletedFrom { get; set; }
        public DateTime? CompletedTo { get; set; }
        public int? ExitCodeFrom { get; set; }
        public int? ExitCodeTo { get; set; }
        public bool? HasErrors { get; set; }
    }

    public class WebAppDeploymentRequestDto
    {
        public Dictionary<string, object> Configuration { get; set; } = new();
        public Dictionary<string, string> Environment { get; set; } = new();
        public List<string> Features { get; set; } = new();
        public bool AutoStart { get; set; } = true;
        public int? Port { get; set; }
        public string? DomainName { get; set; }
    }

    public class ExecutionResourceUpdateDto
    {
        public double CpuTime { get; set; }
        public long MemoryUsed { get; set; }
        public long DiskUsed { get; set; }
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }

    public class ExecutionResourceLimitsDto
    {
        public int MaxCpuPercentage { get; set; } = 80;
        public long MaxMemoryMb { get; set; } = 1024;
        public long MaxDiskMb { get; set; } = 2048;
        public int MaxExecutionTimeMinutes { get; set; } = 60;
        public int MaxConcurrentExecutions { get; set; } = 5;
    }

    public class ExecutionResourceLimitsUpdateDto
    {
        public int? MaxCpuPercentage { get; set; }
        public long? MaxMemoryMb { get; set; }
        public long? MaxDiskMb { get; set; }
        public int? MaxExecutionTimeMinutes { get; set; }
        public int? MaxConcurrentExecutions { get; set; }
    }

    public class ExecutionStatsFilterDto
    {
        public string? ProgramId { get; set; }
        public string? UserId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<string>? Statuses { get; set; }
        public string? ExecutionType { get; set; }
    }

    public class ExecutionEnvironmentUpdateDto
    {
        public Dictionary<string, string> Environment { get; set; } = new();
        public ExecutionResourceLimitsDto? ResourceLimits { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    public class ExecutionScheduleRequestDto
    {
        public DateTime ScheduledTime { get; set; }
        public object Parameters { get; set; } = new object();
        public Dictionary<string, string> Environment { get; set; } = new();
        public ExecutionResourceLimitsDto? ResourceLimits { get; set; }
        public bool SaveResults { get; set; } = true;
        public string? Description { get; set; }
    }

}