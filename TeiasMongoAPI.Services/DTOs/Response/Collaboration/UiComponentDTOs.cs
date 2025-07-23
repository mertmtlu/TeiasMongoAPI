
namespace TeiasMongoAPI.Services.DTOs.Response.Collaboration
{
    public class UiComponentDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Version-specific properties
        public string ProgramId { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;

        public Dictionary<string, object> Configuration { get; set; } = new();
        public Dictionary<string, object> Schema { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }

    public class UiComponentListDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Creator { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Version-specific properties
        public string ProgramId { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public int? VersionNumber { get; set; } // For display purposes

        public string Status { get; set; } = string.Empty;
        public int UsageCount { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class UiComponentDetailDto : UiComponentDto
    {
        public string CreatorName { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public int? VersionNumber { get; set; } // For display purposes
        public List<UiComponentAssetDto> Assets { get; set; } = new();
        public UiComponentBundleInfoDto? BundleInfo { get; set; }
        public UiComponentStatsDto Stats { get; set; } = new();
        public List<UiComponentUsageDto> Usage { get; set; } = new();
    }

    public class UiComponentAssetDto
    {
        public string Path { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    public class UiComponentBundleDto
    {
        public string Id { get; set; } = string.Empty;
        public string ComponentId { get; set; } = string.Empty;
        public string BundleType { get; set; } = string.Empty;
        public List<UiComponentAssetDto> Assets { get; set; } = new();
        public Dictionary<string, string> Dependencies { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public long TotalSize { get; set; }
    }

    public class UiComponentBundleInfoDto
    {
        public string BundleType { get; set; } = string.Empty;
        public List<string> AssetUrls { get; set; } = new();
        public Dictionary<string, string> Dependencies { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public long TotalSize { get; set; }
    }

    public class UiComponentConfigDto
    {
        public string ComponentId { get; set; } = string.Empty;
        public Dictionary<string, object> Configuration { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
    }

    public class UiComponentSchemaDto
    {
        public string ComponentId { get; set; } = string.Empty;
        public Dictionary<string, object> Schema { get; set; } = new();
        public DateTime LastUpdated { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public bool IsValid { get; set; }
    }

    public class UiComponentUsageDto
    {
        public string ProgramId { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public int? VersionNumber { get; set; }
        public string MappingName { get; set; } = string.Empty;
        public DateTime UsedSince { get; set; }
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ProgramComponentMappingDto
    {
        public string Id { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public string ComponentId { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public string MappingName { get; set; } = string.Empty;
        public Dictionary<string, object> MappingConfiguration { get; set; } = new();
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UiComponentRecommendationDto
    {
        public string ComponentId { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public string ComponentType { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public string VersionId { get; set; } = string.Empty;
        public string RecommendationReason { get; set; } = string.Empty;
        public double CompatibilityScore { get; set; }
        public int UsageCount { get; set; }
        public double Rating { get; set; }
    }

    public class UiComponentStatsDto
    {
        public int TotalUsage { get; set; }
        public int ActiveUsage { get; set; }
        public DateTime? LastUsed { get; set; }
        public double AverageRating { get; set; }
        public int RatingCount { get; set; }
        public long TotalDownloads { get; set; }
    }

    public class UiComponentValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<UiComponentValidationSuggestionDto> Suggestions { get; set; } = new();
    }

    public class UiComponentValidationSuggestionDto
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? SuggestedValue { get; set; }
    }

    public class UiComponentCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ComponentCount { get; set; }
        public List<string> SubCategories { get; set; } = new();
    }

    public class UiComponentCopyResultDto
    {
        public string ComponentId { get; set; } = string.Empty;
        public string ComponentName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public bool AssetsCopied { get; set; }
        public int AssetCount { get; set; }
    }

    public class UiComponentBulkCopyResultDto
    {
        public string FromProgramId { get; set; } = string.Empty;
        public string FromVersionId { get; set; } = string.Empty;
        public string ToProgramId { get; set; } = string.Empty;
        public string ToVersionId { get; set; } = string.Empty;
        public int TotalComponents { get; set; }
        public int SuccessfulCopies { get; set; }
        public int FailedCopies { get; set; }
        public List<UiComponentCopyResultDto> Results { get; set; } = new();
    }
}