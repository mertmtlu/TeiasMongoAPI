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

        public bool IsGlobal { get; set; } = false;

        public string? ProgramId { get; set; }

        public object Configuration { get; set; } = new object();
        public object Schema { get; set; } = new object();
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

        public bool? IsGlobal { get; set; }
        public string? ProgramId { get; set; }
        public object? Configuration { get; set; }
        public object? Schema { get; set; }
        public List<string>? Tags { get; set; }
    }

    public class UiComponentSearchDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? Creator { get; set; }
        public string? Status { get; set; }
        public bool? IsGlobal { get; set; }
        public string? ProgramId { get; set; }
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
        public required object Configuration { get; set; }
    }

    public class UiComponentSchemaUpdateDto
    {
        [Required]
        public required object Schema { get; set; }
    }

    public class UiComponentMappingDto
    {
        [Required]
        public required string ComponentId { get; set; }

        [Required]
        [MaxLength(100)]
        public required string MappingName { get; set; }

        public object MappingConfiguration { get; set; } = new object();
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
}