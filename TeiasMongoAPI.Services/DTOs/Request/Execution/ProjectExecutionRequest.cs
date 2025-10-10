using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Services.DTOs.Response.Execution;

namespace TeiasMongoAPI.Services.DTOs.Request.Execution
{
    public class ProjectExecutionRequest
    {
        [Required]
        public required string ProgramId { get; set; }

        public string? VersionId { get; set; }

        [Required]
        public required string UserId { get; set; }

        public object Parameters { get; set; } = new object();

        public Dictionary<string, string> Environment { get; set; } = new();

        public ProjectResourceLimits? ResourceLimits { get; set; }

        public ProjectBuildArgs? BuildArgs { get; set; }

        public bool SaveResults { get; set; } = true;

        public string? ExecutionName { get; set; }

        public bool CleanupOnCompletion { get; set; } = true;

        public List<string> AllowedFileExtensions { get; set; } = new();
    }

    public class ProjectResourceLimits
    {
        public int MaxCpuPercentage { get; set; } = 80;
        public long MaxMemoryMb { get; set; } = 1024;
        public long MaxDiskMb { get; set; } = 2048;
        public int MaxProcesses { get; set; } = 10;
        public long MaxOutputSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB
    }

    public class ProjectBuildArgs
    {
        public string Configuration { get; set; } = "Release";
        public List<string> AdditionalArgs { get; set; } = new();
        public Dictionary<string, string> BuildEnvironment { get; set; } = new();
        public bool SkipBuild { get; set; } = false;
        public bool RestoreDependencies { get; set; } = true;
        public int BuildTimeoutMinutes { get; set; } = 15;
        public string? PackageVolumeName { get; set; }
    }

    public class ProjectExecutionContext
    {
        public required string ExecutionId { get; set; }
        public required string ProjectDirectory { get; set; }
        public required string UserId { get; set; }
        public required object Parameters { get; set; }
        public required Dictionary<string, string> Environment { get; set; }
        public required ProjectResourceLimits ResourceLimits { get; set; }
        public required ProjectStructureAnalysis ProjectStructure { get; set; }
        public required CancellationToken CancellationToken { get; set; }
        public string? WorkingDirectory { get; set; }
        public Action<string>? OutputCallback { get; set; }
        public Action<string>? ErrorCallback { get; set; }
        public string? PackageVolumeName { get; set; }
    }
}