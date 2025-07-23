using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Collaboration
{
    public class UiComponentCreateDto
    {
        [Required]
        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public required string Type { get; set; } // "input_form", "visualization", "composite", "web_component"

        // Note: ProgramId and VersionId are now passed as method parameters, not in DTO
        // This ensures they're always provided and validated at the service level

        public string Configuration { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
    }

    public class UiComponentUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }

        // Note: Cannot change ProgramId or VersionId after creation
        // Components belong to a specific version and cannot be moved

        public string? Configuration { get; set; }
        public string? Schema { get; set; }
        public List<string>? Tags { get; set; }
    }

    public class UiComponentSearchDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? Creator { get; set; }
        public string? Status { get; set; }

        // Version-specific filtering
        public string? ProgramId { get; set; }
        public string? VersionId { get; set; }

        public List<string>? Tags { get; set; }
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
    }

    public class UiComponentBundleUploadDto
    {
        [Required]
        public required List<UiComponentAssetUploadDto> Assets { get; set; }

        public Dictionary<string, string> Dependencies { get; set; } = new();
        public string BundleType { get; set; } = string.Empty; // "angular_element", "react_component", "vue_component"
    }

    public class UiComponentAssetUploadDto
    {
        [Required]
        public required string Path { get; set; }

        [Required]
        public required byte[] Content { get; set; }

        [Required]
        [MaxLength(50)]
        public required string ContentType { get; set; }

        [MaxLength(20)]
        public string AssetType { get; set; } = "file"; // "js", "css", "html", "image", "file"
    }

    public class UiComponentConfigUpdateDto
    {
        [Required]
        public required string Configuration { get; set; }
    }

    public class UiComponentSchemaUpdateDto
    {
        [Required]
        public required string Schema { get; set; }
    }

    public class UiComponentMappingDto
    {
        [Required]
        public required string ComponentId { get; set; }

        [Required]
        [MaxLength(100)]
        public required string MappingName { get; set; }

        public object MappingConfiguration { get; set; } = new();
        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    public class UiComponentCompatibilitySearchDto
    {
        [Required]
        public required string ProgramType { get; set; }

        public string? ProgramLanguage { get; set; }
        public List<string> RequiredFeatures { get; set; } = new();
        public List<string> CompatibleTypes { get; set; } = new();
    }

    public class UiComponentCopyRequestDto
    {
        [Required]
        public required string TargetProgramId { get; set; }

        [Required]
        public required string TargetVersionId { get; set; }

        public string? NewName { get; set; } // If null, keeps original name
        public bool CopyAssets { get; set; } = true;
        public bool OverwriteIfExists { get; set; } = false;
    }

    public class UiComponentBulkCopyRequestDto
    {
        [Required]
        public required string FromProgramId { get; set; }

        [Required]
        public required string FromVersionId { get; set; }

        [Required]
        public required string ToProgramId { get; set; }

        [Required]
        public required string ToVersionId { get; set; }

        public List<string>? ComponentNames { get; set; } // If null, copies all components
        public bool OverwriteIfExists { get; set; } = false;
        public bool CopyAssets { get; set; } = true;
    }
}