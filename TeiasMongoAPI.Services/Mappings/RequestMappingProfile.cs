﻿using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;

namespace TeiasMongoAPI.Services.Mappings
{
    public class RequestMappingProfile : Profile
    {
        public RequestMappingProfile()
        {
            // Request to Domain
            CreateMap<RequestCreateDto, Request>()
                .ForMember(dest => dest._ID, opt => opt.Ignore()) // Generated by MongoDB
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.ProgramId) ? ObjectId.Parse(src.ProgramId) : ObjectId.Empty))
                .ForMember(dest => dest.RequestedBy, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.RequestedBy) ? src.RequestedBy : string.Empty)) // Will be overridden in service with current user
                .ForMember(dest => dest.RequestedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.AssignedTo, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "open"))
                .ForMember(dest => dest.Responses, opt => opt.MapFrom(src => new List<RequestResponse>()))
                .ForMember(dest => dest.RelatedEntityId, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.RelatedEntityId) ? ObjectId.Parse(src.RelatedEntityId) : (ObjectId?)null))
                .ForMember(dest => dest.RelatedEntityType, opt => opt.MapFrom(src => src.RelatedEntityType ?? string.Empty));

            CreateMap<RequestUpdateDto, Request>()
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.ProgramId) ? ObjectId.Parse(src.ProgramId) : ObjectId.Empty))
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<RequestResponseCreateDto, RequestResponse>()
                .ForMember(dest => dest.RespondedBy, opt => opt.Ignore()) // Set from current user in service
                .ForMember(dest => dest.RespondedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

            // Domain to Response
            CreateMap<Request, RequestDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId != ObjectId.Empty ? src.ProgramId.ToString() : null))
                .ForMember(dest => dest.RelatedEntityId, opt => opt.MapFrom(src => src.RelatedEntityId.HasValue ? src.RelatedEntityId.Value.ToString() : null));

            CreateMap<Request, RequestListDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId != ObjectId.Empty ? src.ProgramId.ToString() : null))
                .ForMember(dest => dest.ProgramName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.RequestedByName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.AssignedToName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.ResponseCount, opt => opt.MapFrom(src => src.Responses.Count))
                .ForMember(dest => dest.LastResponseAt, opt => opt.MapFrom(src => src.Responses.Any() ? src.Responses.Max(r => r.RespondedAt) : (DateTime?)null))
                .ForMember(dest => dest.RelatedEntityType, opt => opt.MapFrom(src => src.RelatedEntityType));

            CreateMap<Request, RequestDetailDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ProgramId, opt => opt.MapFrom(src => src.ProgramId != ObjectId.Empty ? src.ProgramId.ToString() : null))
                .ForMember(dest => dest.RelatedEntityId, opt => opt.MapFrom(src => src.RelatedEntityId.HasValue ? src.RelatedEntityId.Value.ToString() : null))
                .ForMember(dest => dest.RequestedByName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.AssignedToName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.ProgramName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.Responses, opt => opt.MapFrom(src => src.Responses))
                .ForMember(dest => dest.RelatedEntity, opt => opt.Ignore()) // Populated in service
                .ForMember(dest => dest.Timeline, opt => opt.MapFrom(src => CreateTimeline(src)))
                .ForMember(dest => dest.Subscribers, opt => opt.Ignore()); // Populated in service

            CreateMap<RequestResponse, RequestResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Generated in service
                .ForMember(dest => dest.RequestId, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.RespondedByName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.IsInternal, opt => opt.MapFrom(src => false)) // Default value, can be overridden
                .ForMember(dest => dest.Attachments, opt => opt.MapFrom(src => new List<string>())); // Populated in service

            // Status and assignment updates
            CreateMap<RequestStatusUpdateDto, Request>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.Type, opt => opt.Ignore())
                .ForMember(dest => dest.Title, opt => opt.Ignore())
                .ForMember(dest => dest.Description, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.RequestedBy, opt => opt.Ignore())
                .ForMember(dest => dest.RequestedAt, opt => opt.Ignore())
                .ForMember(dest => dest.AssignedTo, opt => opt.Ignore())
                .ForMember(dest => dest.Priority, opt => opt.Ignore())
                .ForMember(dest => dest.Responses, opt => opt.Ignore())
                .ForMember(dest => dest.Metadata, opt => opt.Ignore())
                .ForMember(dest => dest.RelatedEntityId, opt => opt.Ignore())
                .ForMember(dest => dest.RelatedEntityType, opt => opt.Ignore());

            CreateMap<RequestAssignmentDto, Request>()
                .ForMember(dest => dest.AssignedTo, opt => opt.MapFrom(src => src.AssignedTo))
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.Type, opt => opt.Ignore())
                .ForMember(dest => dest.Title, opt => opt.Ignore())
                .ForMember(dest => dest.Description, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.RequestedBy, opt => opt.Ignore())
                .ForMember(dest => dest.RequestedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.Priority, opt => opt.Ignore())
                .ForMember(dest => dest.Responses, opt => opt.Ignore())
                .ForMember(dest => dest.Metadata, opt => opt.Ignore())
                .ForMember(dest => dest.RelatedEntityId, opt => opt.Ignore())
                .ForMember(dest => dest.RelatedEntityType, opt => opt.Ignore());

            CreateMap<RequestPriorityUpdateDto, Request>()
                .ForMember(dest => dest.Priority, opt => opt.MapFrom(src => src.Priority))
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.Type, opt => opt.Ignore())
                .ForMember(dest => dest.Title, opt => opt.Ignore())
                .ForMember(dest => dest.Description, opt => opt.Ignore())
                .ForMember(dest => dest.ProgramId, opt => opt.Ignore())
                .ForMember(dest => dest.RequestedBy, opt => opt.Ignore())
                .ForMember(dest => dest.RequestedAt, opt => opt.Ignore())
                .ForMember(dest => dest.AssignedTo, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.Responses, opt => opt.Ignore())
                .ForMember(dest => dest.Metadata, opt => opt.Ignore())
                .ForMember(dest => dest.RelatedEntityId, opt => opt.Ignore())
                .ForMember(dest => dest.RelatedEntityType, opt => opt.Ignore());

            // Statistics mappings
            CreateMap<List<Request>, RequestStatsDto>()
                .ConvertUsing(src => new RequestStatsDto
                {
                    TotalRequests = src.Count,
                    OpenRequests = src.Count(r => r.Status == "open"),
                    InProgressRequests = src.Count(r => r.Status == "in_progress"),
                    CompletedRequests = src.Count(r => r.Status == "completed"),
                    RejectedRequests = src.Count(r => r.Status == "rejected"),
                    UnassignedRequests = src.Count(r => string.IsNullOrEmpty(r.AssignedTo)),
                    AverageResolutionTime = CalculateAverageResolutionTime(src),
                    RequestsByType = src.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count()),
                    RequestsByPriority = src.GroupBy(r => r.Priority).ToDictionary(g => g.Key, g => g.Count())
                });

            // Template mappings
            CreateMap<RequestTemplateCreateDto, RequestTemplateDto>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Generated in service
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore()) // Set from current user in service
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UsageCount, opt => opt.MapFrom(src => 0));

            CreateMap<RequestFromTemplateDto, RequestCreateDto>()
                .ConvertUsing((src, dest, context) => new RequestCreateDto
                {
                    Type = GetFieldValue<string>(src.FieldValues, "Type") ?? "general",
                    Title = GetFieldValue<string>(src.FieldValues, "Title") ?? "Request from template",
                    Description = GetFieldValue<string>(src.FieldValues, "Description") ?? "",
                    ProgramId = src.ProgramId,
                    RelatedEntityId = src.RelatedEntityId,
                    RelatedEntityType = src.RelatedEntityType,
                    Priority = GetFieldValue<string>(src.FieldValues, "Priority") ?? "normal",
                    Metadata = src.FieldValues
                });

            // Validation mappings
            CreateMap<Request, RequestValidationResult>()
                .ConvertUsing(src => new RequestValidationResult
                {
                    IsValid = ValidateRequest(src),
                    Errors = GetRequestValidationErrors(src),
                    Warnings = GetRequestValidationWarnings(src),
                    Suggestions = GetRequestValidationSuggestions(src)
                });

            // Performance and trending mappings
            CreateMap<IGrouping<string, Request>, RequestPerformanceDto>()
                .ConvertUsing(group => new RequestPerformanceDto
                {
                    UserId = group.Key,
                    UserName = string.Empty, // Resolved in service
                    AssignedCount = group.Count(),
                    CompletedCount = group.Count(r => r.Status == "completed"),
                    CompletionRate = group.Count() > 0 ? (double)group.Count(r => r.Status == "completed") / group.Count() * 100 : 0,
                    AverageResolutionTime = CalculateAverageResolutionTime(group.ToList()),
                    Rating = 0.0 // This would come from rating data
                });

            // Infrastructure linking
            CreateMap<RequestInfrastructureLinkDto, RequestRelatedEntityDto>()
                .ForMember(dest => dest.EntityType, opt => opt.MapFrom(src => src.EntityType))
                .ForMember(dest => dest.EntityId, opt => opt.MapFrom(src => src.EntityId))
                .ForMember(dest => dest.EntityName, opt => opt.Ignore()) // Resolved in service
                .ForMember(dest => dest.LinkDescription, opt => opt.MapFrom(src => src.LinkDescription));
        }

        private RequestTimelineDto CreateTimeline(Request request)
        {
            var timeline = new RequestTimelineDto
            {
                CreatedAt = request.RequestedAt,
                AssignedAt = null, // Would need to track this separately
                FirstResponseAt = request.Responses.Any() ? request.Responses.Min(r => r.RespondedAt) : null,
                CompletedAt = request.Status == "completed" ? (DateTime?)DateTime.UtcNow : null, // Would need to track this
                Events = new List<RequestTimelineEventDto>()
            };

            // Add creation event
            timeline.Events.Add(new RequestTimelineEventDto
            {
                Timestamp = request.RequestedAt,
                EventType = "created",
                Description = "Request created",
                UserId = request.RequestedBy,
                UserName = string.Empty // Resolved in service
            });

            // Add response events
            foreach (var response in request.Responses.OrderBy(r => r.RespondedAt))
            {
                timeline.Events.Add(new RequestTimelineEventDto
                {
                    Timestamp = response.RespondedAt,
                    EventType = "response",
                    Description = "Response added",
                    UserId = response.RespondedBy,
                    UserName = string.Empty // Resolved in service
                });
            }

            // Calculate resolution time if completed
            if (timeline.CompletedAt.HasValue)
            {
                timeline.ResolutionTime = timeline.CompletedAt.Value - timeline.CreatedAt;
            }

            return timeline;
        }

        private double CalculateAverageResolutionTime(List<Request> requests)
        {
            var completedRequests = requests.Where(r => r.Status == "completed").ToList();
            if (!completedRequests.Any()) return 0;

            // This is a simplified calculation - in reality you'd track completion timestamps
            var totalHours = completedRequests.Sum(r => (DateTime.UtcNow - r.RequestedAt).TotalHours);
            return totalHours / completedRequests.Count;
        }

        private T? GetFieldValue<T>(Dictionary<string, object> fieldValues, string fieldName)
        {
            if (fieldValues.TryGetValue(fieldName, out var value))
            {
                try
                {
                    return (T)value;
                }
                catch
                {
                    return default(T);
                }
            }
            return default(T);
        }

        private bool ValidateRequest(Request request)
        {
            return !string.IsNullOrEmpty(request.Type) &&
                   !string.IsNullOrEmpty(request.Title) &&
                   !string.IsNullOrEmpty(request.Description) &&
                   !string.IsNullOrEmpty(request.RequestedBy);
        }

        private List<string> GetRequestValidationErrors(Request request)
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(request.Type))
                errors.Add("Request type is required");

            if (string.IsNullOrEmpty(request.Title))
                errors.Add("Request title is required");

            if (string.IsNullOrEmpty(request.Description))
                errors.Add("Request description is required");

            if (string.IsNullOrEmpty(request.RequestedBy))
                errors.Add("Requester information is required");

            return errors;
        }

        private List<string> GetRequestValidationWarnings(Request request)
        {
            var warnings = new List<string>();

            if (request.Priority == "high" && string.IsNullOrEmpty(request.AssignedTo))
                warnings.Add("High priority requests should be assigned immediately");

            if (request.Responses.Count == 0 && (DateTime.UtcNow - request.RequestedAt).TotalHours > 24)
                warnings.Add("Request has been open for more than 24 hours without response");

            return warnings;
        }

        private List<RequestValidationSuggestionDto> GetRequestValidationSuggestions(Request request)
        {
            var suggestions = new List<RequestValidationSuggestionDto>();

            if (string.IsNullOrEmpty(request.AssignedTo))
            {
                suggestions.Add(new RequestValidationSuggestionDto
                {
                    Field = "AssignedTo",
                    Message = "Consider assigning this request to improve response time",
                    SuggestedValue = null
                });
            }

            return suggestions;
        }
    }
}