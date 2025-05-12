using AutoMapper;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Building;
using TeiasMongoAPI.Services.DTOs.Response.Building;

public class BuildingMappingProfile : Profile
{
    public BuildingMappingProfile()
    {
        // Request to Domain
        CreateMap<BuildingCreateDto, Building>()
            .ForMember(dest => dest.TmID, opt => opt.MapFrom(src => ObjectId.Parse(src.TmId)));
        CreateMap<BuildingUpdateDto, Building>()
            .ForMember(dest => dest.TmID, opt => opt.MapFrom(src => src.TmId != null ? ObjectId.Parse(src.TmId) : ObjectId.Empty))
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // Domain to Response
        CreateMap<Building, BuildingDto>()
            .ForMember(dest => dest.TmId, opt => opt.MapFrom(src => src.TmID.ToString()))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()));
        CreateMap<Building, BuildingSummaryDto>()
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
            .ForMember(dest => dest.BlockCount, opt => opt.MapFrom(src => src.Blocks.Count));
        CreateMap<Building, BuildingListDto>()
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
            .ForMember(dest => dest.BlockCount, opt => opt.MapFrom(src => src.Blocks.Count));
        CreateMap<Building, BuildingDetailDto>()
            .ForMember(dest => dest.TmId, opt => opt.MapFrom(src => src.TmID.ToString()))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
            .ForMember(dest => dest.BlockCount, opt => opt.MapFrom(src => src.Blocks.Count));
    }
}