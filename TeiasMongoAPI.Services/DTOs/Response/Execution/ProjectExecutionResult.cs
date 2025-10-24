namespace TeiasMongoAPI.Services.DTOs.Response.Execution
{
    public class ProjectExecutionResult
    {
        public string ExecutionId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorOutput { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? Duration { get; set; }
        public ProjectResourceUsage ResourceUsage { get; set; } = new();
        public List<string> OutputFiles { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public ProjectBuildResult? BuildResult { get; set; }
        public List<ProjectExecutionWarning> Warnings { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class ProjectStructureAnalysis
    {
        public string Language { get; set; } = string.Empty;
        public string ProjectType { get; set; } = string.Empty;
        public List<string> EntryPoints { get; set; } = new();
        public List<string> ConfigFiles { get; set; } = new();
        public List<string> SourceFiles { get; set; } = new();
        public List<string> BinaryFiles { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool HasBuildFile { get; set; }
        public string? MainEntryPoint { get; set; }
        public List<ProjectFile> Files { get; set; } = new();
        public ProjectComplexity Complexity { get; set; } = new();
    }

    public class ProjectBuildResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorOutput { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public List<string> GeneratedFiles { get; set; } = new();
        public List<ProjectBuildWarning> Warnings { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public class ProjectValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
        public ProjectSecurityScan? SecurityScan { get; set; }
        public ProjectComplexity? Complexity { get; set; }
    }

    public class ProjectResourceUsage
    {
        public double CpuTimeSeconds { get; set; }
        public long PeakMemoryBytes { get; set; }
        public long DiskSpaceUsedBytes { get; set; }
        public int ProcessCount { get; set; }
        public long OutputSizeBytes { get; set; }
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }

    public class ProjectFile
    {
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Extension { get; set; } = string.Empty;
        public bool IsEntryPoint { get; set; }
        public int LineCount { get; set; }
    }

    public class ProjectComplexity
    {
        public int TotalFiles { get; set; }
        public int TotalLines { get; set; }
        public int Dependencies { get; set; }
        public string ComplexityLevel { get; set; } = "Simple";
        public double ComplexityScore { get; set; }
    }

    public class ProjectSecurityScan
    {
        public bool HasSecurityIssues { get; set; }
        public List<SecurityIssue> Issues { get; set; } = new();
        public List<string> SuspiciousPatterns { get; set; } = new();
        public int RiskLevel { get; set; }
    }

    public class SecurityIssue
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public int Line { get; set; }
        public string Severity { get; set; } = string.Empty;
    }

    public class ProjectExecutionWarning
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? File { get; set; }
        public int? Line { get; set; }
    }

    public class ProjectBuildWarning
    {
        public string Message { get; set; } = string.Empty;
        public string? File { get; set; }
        public int? Line { get; set; }
        public string Severity { get; set; } = "Warning";
    }
}