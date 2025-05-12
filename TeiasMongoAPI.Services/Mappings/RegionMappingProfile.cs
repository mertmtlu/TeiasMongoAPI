using AutoMapper;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Region;
using TeiasMongoAPI.Services.DTOs.Response.Region;

public class RegionMappingProfile : Profile
{
    public RegionMappingProfile()
    {
        // Request to Domain
        CreateMap<RegionCreateDto, Region>()
            .ForMember(dest => dest.ClientID, opt => opt.MapFrom(src => ObjectId.Parse(src.ClientId)));
        CreateMap<RegionUpdateDto, Region>()
            .ForMember(dest => dest.ClientID, opt => opt.MapFrom(src => src.ClientId != null ? ObjectId.Parse(src.ClientId) : ObjectId.Empty))
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // Domain to Response
        CreateMap<Region, RegionDto>()
            .ForMember(dest => dest.ClientId, opt => opt.MapFrom(src => src.ClientID.ToString()))
            .ForMember(dest => dest.RegionId, opt => opt.MapFrom(src => src.Id));
        CreateMap<Region, RegionSummaryDto>()
            .ForMember(dest => dest.RegionId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.CityCount, opt => opt.MapFrom(src => src.Cities.Count));
        CreateMap<Region, RegionListDto>()
            .ForMember(dest => dest.RegionId, opt => opt.MapFrom(src => src.Id));
        CreateMap<Region, RegionDetailDto>()
            .ForMember(dest => dest.ClientId, opt => opt.MapFrom(src => src.ClientID.ToString()))
            .ForMember(dest => dest.RegionId, opt => opt.MapFrom(src => src.Id));
    }
}