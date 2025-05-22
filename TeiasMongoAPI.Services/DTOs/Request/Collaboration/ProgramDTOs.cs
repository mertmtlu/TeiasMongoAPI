using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Services.DTOs.Request.Collaboration
{
    public class ProgramCreateDto
    {
        [Required]
        [MaxLength(100)]
        public required string Name { get; set; }

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public required string Type { get; set; }

        [Required]
        [MaxLength(30)]
        public required string Language { get; set; }

        [MaxLength(200)]
        public string MainFile { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public required string UiType { get; set; }

        public object UiConfiguration { get; set; } = new object();
        public object Metadata { get; set; } = new object();
        public AppDeploymentInfo? DeploymentInfo { get; set; }
    }

    public class ProgramUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }

        [MaxLength(30)]
        public string? Language { get; set; }

        [MaxLength(200)]
        public string? MainFile { get; set; }

        [MaxLength(50)]
        public string? UiType { get; set; }

        public object? UiConfiguration { get; set; }
        public object? Metadata { get; set; }
        public AppDeploymentInfo? DeploymentInfo { get; set; }
    }

    public class ProgramSearchDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Type { get; set; }
        public string? Language { get; set; }
        public string? UiType { get; set; }
        public string? Creator { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
        public List<string>? Tags { get; set; }
        public AppDeploymentType? DeploymentType { get; set; }
    }

    public class ProgramUserPermissionDto
    {
        [Required]
        public required string UserId { get; set; }

        [Required]
        [MaxLength(20)]
        public required string AccessLevel { get; set; } // "read", "write", "admin"
    }

    public class ProgramGroupPermissionDto
    {
        [Required]
        public required string GroupId { get; set; }

        [Required]
        [MaxLength(20)]
        public required string AccessLevel { get; set; }
    }

    public class ProgramFileUploadDto
    {
        [Required]
        public required string Path { get; set; }

        [Required]
        public required byte[] Content { get; set; }

        [MaxLength(50)]
        public string ContentType { get; set; } = "application/octet-stream";

        [MaxLength(100)]
        public string? Description { get; set; }
    }

    public class ProgramFileUpdateDto
    {
        [Required]
        public required byte[] Content { get; set; }

        [MaxLength(50)]
        public string? ContentType { get; set; }

        [MaxLength(100)]
        public string? Description { get; set; }
    }

    public class ProgramDeploymentRequestDto
    {
        [Required]
        public required AppDeploymentType DeploymentType { get; set; }

        public Dictionary<string, object> Configuration { get; set; } = new();
        public List<ProgramFileUploadDto> Files { get; set; } = new();
        public List<string> SupportedFeatures { get; set; } = new();
    }

    public class ProgramDeploymentConfigDto
    {
        public Dictionary<string, object> Configuration { get; set; } = new();
        public List<string> SupportedFeatures { get; set; } = new();
    }
}