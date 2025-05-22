namespace TeiasMongoAPI.Services.DTOs.Response.Collaboration
{
    public class VersionDto
    {
        public string Id { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string CommitMessage { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Reviewer { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewComments { get; set; }
    }

    public class VersionListDto
    {
        public string Id { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string CommitMessage { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Reviewer { get; set; }
        public string? ReviewerName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public int FileCount { get; set; }
        public bool IsCurrent { get; set; }
    }

    public class VersionDetailDto : VersionDto
    {
        public string ProgramName { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public string? ReviewerName { get; set; }
        public List<VersionFileDto> Files { get; set; } = new();
        public VersionStatsDto Stats { get; set; } = new();
        public VersionDeploymentInfoDto? DeploymentInfo { get; set; }
    }

    public class VersionFileDto
    {
        public string Path { get; set; } = string.Empty;
        public string StorageKey { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
        public string FileType { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }

    public class VersionFileDetailDto : VersionFileDto
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public DateTime LastModified { get; set; }
    }

    public class VersionReviewDto
    {
        public string Id { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Comments { get; set; } = string.Empty;
        public string ReviewedBy { get; set; } = string.Empty;
        public string ReviewedByName { get; set; } = string.Empty;
        public DateTime ReviewedAt { get; set; }
    }

    public class VersionDiffDto
    {
        public string FromVersionId { get; set; } = string.Empty;
        public string ToVersionId { get; set; } = string.Empty;
        public int FromVersionNumber { get; set; }
        public int ToVersionNumber { get; set; }
        public List<VersionFileChangeSummaryDto> Changes { get; set; } = new();
        public VersionDiffStatsDto Stats { get; set; } = new();
    }

    public class VersionFileChangeSummaryDto
    {
        public string Path { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // "added", "modified", "deleted"
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
        public long SizeBefore { get; set; }
        public long SizeAfter { get; set; }
    }

    public class VersionDiffStatsDto
    {
        public int FilesChanged { get; set; }
        public int FilesAdded { get; set; }
        public int FilesDeleted { get; set; }
        public int TotalLinesAdded { get; set; }
        public int TotalLinesRemoved { get; set; }
    }

    public class VersionChangeDto
    {
        public string Path { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ImpactLevel { get; set; } // 1-5 scale
    }

    public class VersionDeploymentDto
    {
        public string Id { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime DeployedAt { get; set; }
        public string DeployedBy { get; set; } = string.Empty;
        public List<string> TargetEnvironments { get; set; } = new();
        public Dictionary<string, object> Configuration { get; set; } = new();
    }

    public class VersionDeploymentInfoDto
    {
        public bool IsDeployed { get; set; }
        public DateTime? LastDeployment { get; set; }
        public string? DeploymentStatus { get; set; }
        public List<string> Environments { get; set; } = new();
    }

    public class VersionStatsDto
    {
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public Dictionary<string, int> FileTypeCount { get; set; } = new();
        public int ExecutionCount { get; set; }
        public bool IsCurrentVersion { get; set; }
    }

    public class VersionActivityDto
    {
        public DateTime Date { get; set; }
        public string Activity { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}