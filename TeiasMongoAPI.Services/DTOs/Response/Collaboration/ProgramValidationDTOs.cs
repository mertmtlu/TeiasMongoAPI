using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Response.Collaboration
{
    public class ProjectValidationResultDto
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
        public ProjectSecurityScanDto? SecurityScan { get; set; }
        public ProjectComplexityDto? Complexity { get; set; }
    }

    public class ProjectStructureAnalysisDto
    {
        public string Language { get; set; } = string.Empty;
        public string ProjectType { get; set; } = string.Empty;
        public List<string> EntryPoints { get; set; } = new();
        public List<string> ConfigFiles { get; set; } = new();
        public List<string> SourceFiles { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public bool HasBuildFile { get; set; }
        public string? MainEntryPoint { get; set; }
        public List<ProjectFileDto> Files { get; set; } = new();
        public ProjectComplexityDto Complexity { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ProjectFileDto
    {
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Extension { get; set; } = string.Empty;
        public bool IsEntryPoint { get; set; }
        public int LineCount { get; set; }
    }

    public class ProjectComplexityDto
    {
        public int TotalFiles { get; set; }
        public int TotalLines { get; set; }
        public int Dependencies { get; set; }
        public string ComplexityLevel { get; set; } = "Simple";
        public double ComplexityScore { get; set; }
    }

    public class ProjectSecurityScanDto
    {
        public bool HasSecurityIssues { get; set; }
        public List<SecurityIssueDto> Issues { get; set; } = new();
        public int RiskLevel { get; set; }
    }

    public class SecurityIssueDto
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public int Line { get; set; }
        public string Severity { get; set; } = string.Empty;
    }
}
