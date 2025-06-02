using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.Collaboration
{
    public class RequestCreateDto
    {
        [Required]
        [MaxLength(50)]
        public required string Type { get; set; } // "feature", "ui", "review", "infrastructure_access"

        [Required]
        [MaxLength(200)]
        public required string Title { get; set; }

        [Required]
        [MaxLength(2000)]
        public required string Description { get; set; }

        public string? RequestedBy { get; set; }    

        public string? ProgramId { get; set; }
        public string? RelatedEntityId { get; set; }

        [MaxLength(50)]
        public string? RelatedEntityType { get; set; } // "program", "client", "region", "tm", "building"

        [MaxLength(20)]
        public string Priority { get; set; } = "normal"; // "low", "normal", "high"

        public object Metadata { get; set; } = new object();
    }

    public class RequestUpdateDto
    {
        public string? ProgramId { get; set; }

        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(20)]
        public string? Priority { get; set; }

        public object? Metadata { get; set; }
    }

    public class RequestSearchDto
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string? RequestedBy { get; set; }
        public string? AssignedTo { get; set; }
        public string? ProgramId { get; set; }
        public string? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }
        public DateTime? RequestedFrom { get; set; }
        public DateTime? RequestedTo { get; set; }
        public List<string>? Tags { get; set; }
    }

    public class RequestStatusUpdateDto
    {
        [Required]
        [MaxLength(20)]
        public required string Status { get; set; } // "open", "in_progress", "completed", "rejected"

        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class RequestAssignmentDto
    {
        [Required]
        public required string AssignedTo { get; set; }

        [MaxLength(500)]
        public string? AssignmentNotes { get; set; }
    }

    public class RequestPriorityUpdateDto
    {
        [Required]
        [MaxLength(20)]
        public required string Priority { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class RequestResponseCreateDto
    {
        [Required]
        [MaxLength(2000)]
        public required string Message { get; set; }

        public bool IsInternal { get; set; } = false;
        public List<string> Attachments { get; set; } = new();
    }

    public class RequestResponseUpdateDto
    {
        [Required]
        [MaxLength(2000)]
        public required string Message { get; set; }

        public bool? IsInternal { get; set; }
        public List<string>? Attachments { get; set; }
    }

    public class RequestCompletionDto
    {
        [Required]
        [MaxLength(1000)]
        public required string CompletionNotes { get; set; }

        public List<string> DeliverableLinks { get; set; } = new();
        public object? CompletionData { get; set; }
    }

    public class RequestRejectionDto
    {
        [Required]
        [MaxLength(1000)]
        public required string RejectionReason { get; set; }

        public List<string> AlternativeSuggestions { get; set; } = new();
    }

    public class RequestStatsFilterDto
    {
        public string? ProgramId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Type { get; set; }
        public string? AssignedTo { get; set; }
        public List<string>? Statuses { get; set; }
    }

    public class RequestTemplateCreateDto
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
        [MaxLength(200)]
        public required string TitleTemplate { get; set; }

        [Required]
        [MaxLength(2000)]
        public required string DescriptionTemplate { get; set; }

        public object FieldDefinitions { get; set; } = new object();
        public string Priority { get; set; } = "normal";
        public bool IsActive { get; set; } = true;
    }

    public class RequestFromTemplateDto
    {
        [Required]
        public required Dictionary<string, object> FieldValues { get; set; }

        public string? ProgramId { get; set; }
        public string? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }
    }

    public class RequestInfrastructureLinkDto
    {
        [Required]
        public required string EntityType { get; set; }

        [Required]
        public required string EntityId { get; set; }

        [MaxLength(500)]
        public string? LinkDescription { get; set; }
    }

    public class BulkRequestStatusUpdateDto
    {
        [Required]
        public required List<string> RequestIds { get; set; }

        [Required]
        [MaxLength(20)]
        public required string Status { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }
    }
}