namespace TeiasMongoAPI.Services.DTOs.Response.Collaboration
{
    public class RequestDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ProgramId { get; set; }
        public string? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public string? AssignedTo { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public object Metadata { get; set; } = new object();
    }

    public class RequestListDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ProgramId { get; set; }
        public string? ProgramName { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public string RequestedByName { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public string? AssignedTo { get; set; }
        public string? AssignedToName { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int ResponseCount { get; set; }
        public DateTime? LastResponseAt { get; set; }
        public string? RelatedEntityType { get; set; }
    }

    public class RequestDetailDto : RequestDto
    {
        public string RequestedByName { get; set; } = string.Empty;
        public string? AssignedToName { get; set; }
        public string? ProgramName { get; set; }
        public List<RequestResponseDto> Responses { get; set; } = new();
        public RequestRelatedEntityDto? RelatedEntity { get; set; }
        public RequestTimelineDto Timeline { get; set; } = new();
        public List<string> Subscribers { get; set; } = new();
    }

    public class RequestResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string RespondedBy { get; set; } = string.Empty;
        public string RespondedByName { get; set; } = string.Empty;
        public DateTime RespondedAt { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public List<string> Attachments { get; set; } = new();
    }

    public class RequestRelatedEntityDto
    {
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string? LinkDescription { get; set; }
    }

    public class RequestTimelineDto
    {
        public DateTime CreatedAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? FirstResponseAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? ResolutionTime { get; set; }
        public List<RequestTimelineEventDto> Events { get; set; } = new();
    }

    public class RequestTimelineEventDto
    {
        public DateTime Timestamp { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }

    public class RequestStatsDto
    {
        public int TotalRequests { get; set; }
        public int OpenRequests { get; set; }
        public int InProgressRequests { get; set; }
        public int CompletedRequests { get; set; }
        public int RejectedRequests { get; set; }
        public int UnassignedRequests { get; set; }
        public double AverageResolutionTime { get; set; }
        public Dictionary<string, int> RequestsByType { get; set; } = new();
        public Dictionary<string, int> RequestsByPriority { get; set; } = new();
    }

    public class RequestTrendDto
    {
        public DateTime Date { get; set; }
        public int CreatedCount { get; set; }
        public int CompletedCount { get; set; }
        public int TotalOpen { get; set; }
    }

    public class RequestMetricDto
    {
        public string Category { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class RequestPerformanceDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int AssignedCount { get; set; }
        public int CompletedCount { get; set; }
        public double CompletionRate { get; set; }
        public double AverageResolutionTime { get; set; }
        public double Rating { get; set; }
    }

    public class RequestTemplateDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string TitleTemplate { get; set; } = string.Empty;
        public string DescriptionTemplate { get; set; } = string.Empty;
        public object FieldDefinitions { get; set; } = new object();
        public string Priority { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int UsageCount { get; set; }
    }

    public class RequestValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<RequestValidationSuggestionDto> Suggestions { get; set; } = new();
    }

    public class RequestValidationSuggestionDto
    {
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? SuggestedValue { get; set; }
    }
}