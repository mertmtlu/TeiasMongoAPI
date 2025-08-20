using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Services.DTOs.Request.Icon;
using TeiasMongoAPI.Services.DTOs.Response.Icon;

namespace TeiasMongoAPI.Services.Mappings
{
    public class IconMappingProfile : Profile
    {
        public IconMappingProfile()
        {
            CreateMap<IconCreateDto, Icon>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.EntityId, opt => opt.MapFrom(src => ObjectId.Parse(src.EntityId)))
                .ForMember(dest => dest.Size, opt => opt.MapFrom(src => CalculateBase64Size(src.IconData)))
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.ModifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata));

            CreateMap<Icon, IconResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.EntityId, opt => opt.MapFrom(src => src.EntityId.ToString()))
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata));

            CreateMap<IconUpdateDto, Icon>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.EntityType, opt => opt.Ignore())
                .ForMember(dest => dest.EntityId, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ModifiedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Size, opt => opt.MapFrom((src, dest) => 
                    !string.IsNullOrEmpty(src.IconData) ? CalculateBase64Size(src.IconData) : dest.Size))
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        }

        private static int CalculateBase64Size(string base64String)
        {
            if (string.IsNullOrEmpty(base64String))
                return 0;

            var base64Length = base64String.Length;
            var padding = 0;
            
            if (base64String.EndsWith("=="))
                padding = 2;
            else if (base64String.EndsWith("="))
                padding = 1;

            return (base64Length * 3 / 4) - padding;
        }
    }
}