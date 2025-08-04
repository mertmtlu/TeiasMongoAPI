using AutoMapper;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.UIWorkflow;

namespace TeiasMongoAPI.Services.Mappings
{
    public class UIInteractionMappingProfile : Profile
    {
        public UIInteractionMappingProfile()
        {
            CreateMap<UIInteraction, UIInteractionSessionApiResponse>()
                .ForMember(dest => dest.SessionId, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.WorkflowId, opt => opt.MapFrom(src => src.WorkflowExecutionId.ToString())) // This will need to be resolved from execution
                .ForMember(dest => dest.ExecutionId, opt => opt.MapFrom(src => src.WorkflowExecutionId.ToString()))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.InteractionType, opt => opt.MapFrom(src => src.InteractionType.ToString()))
                .ForMember(dest => dest.TimeoutAt, opt => opt.MapFrom(src => src.Timeout.HasValue ? src.CreatedAt.Add(src.Timeout.Value) : (DateTime?)null))
                .ForMember(dest => dest.ContextData, opt => opt.MapFrom(src => new Dictionary<string, object>()));

            CreateMap<UIInteraction, UIInteractionSession>()
                .ForMember(dest => dest.SessionId, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.WorkflowId, opt => opt.MapFrom(src => src.WorkflowExecutionId.ToString())) // This will need to be resolved from execution
                .ForMember(dest => dest.ExecutionId, opt => opt.MapFrom(src => src.WorkflowExecutionId.ToString()))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.InteractionType, opt => opt.MapFrom(src => src.InteractionType.ToString()))
                .ForMember(dest => dest.TimeoutAt, opt => opt.MapFrom(src => src.Timeout.HasValue ? src.CreatedAt.Add(src.Timeout.Value) : (DateTime?)null))
                .ForMember(dest => dest.ContextData, opt => opt.MapFrom(src => new Dictionary<string, object>()));
        }
    }
}