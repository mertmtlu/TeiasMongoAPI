using AutoMapper;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.TM;
using TeiasMongoAPI.Services.DTOs.Response.TM;

public class TMMappingProfile : Profile
{
    public TMMappingProfile()
    {
        // Request to Domain
        CreateMap<TMCreateDto, TM>()
            .ForMember(dest => dest.RegionID, opt => opt.MapFrom(src => ObjectId.Parse(src.RegionId)))
            .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.Address.City ?? string.Empty))
            .ForMember(dest => dest.County, opt => opt.MapFrom(src => src.Address.County ?? string.Empty))
            .ForMember(dest => dest.District, opt => opt.MapFrom(src => src.Address.District ?? string.Empty))
            .ForMember(dest => dest.Street, opt => opt.MapFrom(src => src.Address.Street ?? string.Empty));
        CreateMap<TMUpdateDto, TM>()
            .ForMember(dest => dest.RegionID, opt => opt.MapFrom(src => src.RegionId != null ? ObjectId.Parse(src.RegionId) : ObjectId.Empty))
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // Domain to Response
        CreateMap<TM, TMDto>()
            .ForMember(dest => dest.RegionId, opt => opt.MapFrom(src => src.RegionID.ToString()))
            .ForMember(dest => dest.TMId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
            .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State.ToString()))
            .ForMember(dest => dest.Address, opt => opt.MapFrom(src => new AddressDto
            { City = src.City, County = src.County, District = src.District, Street = src.Street }));
        CreateMap<TM, TMSummaryDto>()
            .ForMember(dest => dest.TMId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State.ToString()));
        CreateMap<TM, TMListDto>()
            .ForMember(dest => dest.TMId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
            .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State.ToString()));
        CreateMap<TM, TMDetailDto>()
            .ForMember(dest => dest.TMId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
            .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State.ToString()));
    }
}