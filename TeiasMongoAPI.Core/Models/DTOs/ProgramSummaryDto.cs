using System;

namespace TeiasMongoAPI.Core.Models.DTOs
{
    public class ProgramSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public VersionInfoDto? CurrentVersion { get; set; }
        
        public int VersionCount { get; set; }
        public bool HasVersions => VersionCount > 0;
        public int ComponentCount { get; set; }
        public bool HasComponents => ComponentCount > 0;
        public string? NewestComponentType { get; set; }
    }

    public class VersionInfoDto
    {
        public string Id { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
    }
}