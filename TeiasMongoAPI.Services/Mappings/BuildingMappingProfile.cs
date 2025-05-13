using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Building;
using TeiasMongoAPI.Services.DTOs.Response.Building;

namespace TeiasMongoAPI.Services.Mappings
{
    public class BuildingMappingProfile : Profile
    {
        public BuildingMappingProfile()
        {
            // Request to Domain
            CreateMap<BuildingCreateDto, Building>()
                .ForMember(dest => dest.TmID, opt => opt.MapFrom(src =>
                    string.IsNullOrEmpty(src.TmId) ? ObjectId.Empty : ObjectId.Parse(src.TmId)))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty));

            CreateMap<BuildingUpdateDto, Building>()
                .ForMember(dest => dest.TmID, opt => opt.MapFrom(src =>
                    string.IsNullOrEmpty(src.TmId) ? ObjectId.Empty : ObjectId.Parse(src.TmId)))
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // Domain to Response
            CreateMap<Building, BuildingResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.TmId, opt => opt.MapFrom(src => src.TmID.ToString()))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()));

            CreateMap<Building, BuildingSummaryResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.BlockCount, opt => opt.MapFrom(src => src.Blocks.Count));

            CreateMap<Building, BuildingListResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.BlockCount, opt => opt.MapFrom(src => src.Blocks.Count));

            CreateMap<Building, BuildingDetailResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.TmId, opt => opt.MapFrom(src => src.TmID.ToString()))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.BlockCount, opt => opt.MapFrom(src => src.Blocks.Count));
        }
    }
}