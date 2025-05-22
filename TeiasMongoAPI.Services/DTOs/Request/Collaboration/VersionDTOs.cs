using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Collaboration
{
    public class VersionCreateDto
    {
        [Required]
        public required string ProgramId { get; set; }

        [Required]
        [MaxLength(200)]
        public required string CommitMessage { get; set; }

        public List<VersionFileCreateDto> Files { get; set; } = new();
    }

    public class VersionUpdateDto
    {
        [MaxLength(200)]
        public string? CommitMessage { get; set; }

        public string? ReviewComments { get; set; }
    }

    public class VersionSearchDto
    {
        public string? ProgramId { get; set; }
        public string? CreatedBy { get; set; }
        public string? Reviewer { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
        public DateTime? ReviewedFrom { get; set; }
        public DateTime? ReviewedTo { get; set; }
        public int? VersionNumberFrom { get; set; }
        public int? VersionNumberTo { get; set; }
    }

    public class VersionStatusUpdateDto
    {
        [Required]
        [MaxLength(20)]
        public required string Status { get; set; } // "pending", "approved", "rejected"

        [MaxLength(500)]
        public string? Comments { get; set; }
    }

    public class VersionReviewSubmissionDto
    {
        [Required]
        [MaxLength(20)]
        public required string Status { get; set; } // "approved", "rejected"

        [Required]
        [MaxLength(1000)]
        public required string Comments { get; set; }
    }

    public class VersionFileCreateDto
    {
        [Required]
        public required string Path { get; set; }

        [Required]
        public required byte[] Content { get; set; }

        [MaxLength(50)]
        public string ContentType { get; set; } = "application/octet-stream";

        [MaxLength(50)]
        public string FileType { get; set; } = "source"; // "source", "asset", "config", "build_artifact"
    }

    public class VersionFileUpdateDto
    {
        [Required]
        public required byte[] Content { get; set; }

        [MaxLength(50)]
        public string? ContentType { get; set; }

        [MaxLength(50)]
        public string? FileType { get; set; }
    }

    public class VersionDeploymentRequestDto
    {
        public Dictionary<string, object> DeploymentConfiguration { get; set; } = new();
        public List<string> TargetEnvironments { get; set; } = new();
        public bool SetAsCurrent { get; set; } = false;
    }

    public class VersionCommitDto
    {
        [Required]
        [MaxLength(200)]
        public required string CommitMessage { get; set; }

        [Required]
        public required List<VersionFileChangeDto> Changes { get; set; }
    }

    public class VersionFileChangeDto
    {
        [Required]
        public required string Path { get; set; }

        [Required]
        [MaxLength(20)]
        public required string Action { get; set; } // "add", "modify", "delete"

        public byte[]? Content { get; set; } // null for delete
        public string? ContentType { get; set; }
    }

    public class VersionCommitValidationDto
    {
        [Required]
        public required List<VersionFileChangeDto> Changes { get; set; }
    }
}