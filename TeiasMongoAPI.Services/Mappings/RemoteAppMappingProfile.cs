using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.RemoteApp;
using TeiasMongoAPI.Services.DTOs.Response.RemoteApp;
using TeiasMongoAPI.Services.Mappings;

namespace TeiasMongoAPI.Services.Mappings
{
    public class RemoteAppMappingProfile : Profile
    {
        public RemoteAppMappingProfile()
        {
            // Request DTOs to Entity mappings
            CreateMap<RemoteAppCreateDto, RemoteApp>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.Permissions, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "active"))
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ModifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => new object()));

            CreateMap<RemoteAppUpdateDto, RemoteApp>()
                .ForMember(dest => dest._ID, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.Permissions, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ModifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Metadata, opt => opt.Ignore())
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));

            // Entity to Response DTOs mappings
            CreateMap<RemoteApp, RemoteAppDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()));

            CreateMap<RemoteApp, RemoteAppListDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()));

            CreateMap<RemoteApp, RemoteAppDetailDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.CreatorName, opt => opt.Ignore())
                .ForMember(dest => dest.Permissions, opt => opt.Ignore());

            // ObjectId to string and vice versa converters
            CreateMap<ObjectId, string>().ConvertUsing(id => id.ToString());
            CreateMap<string, ObjectId>().ConvertUsing<StringToObjectIdConverter>();
        }
    }

}