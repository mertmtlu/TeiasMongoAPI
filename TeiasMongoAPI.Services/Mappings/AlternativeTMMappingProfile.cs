using AutoMapper;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.AlternativeTM;
using TeiasMongoAPI.Services.DTOs.Response.AlternativeTM;

public class AlternativeTMMappingProfile : Profile
{
    public AlternativeTMMappingProfile()
    {
        // Request to Domain
        CreateMap<AlternativeTMCreateDto, AlternativeTM>()
            .ForMember(dest => dest.TmID, opt => opt.MapFrom(src => ObjectId.Parse(src.TmId)))
            .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.Address.City ?? string.Empty))
            .ForMember(dest => dest.County, opt => opt.MapFrom(src => src.Address.County ?? string.Empty))
            .ForMember(dest => dest.District, opt => opt.MapFrom(src => src.Address.District ?? string.Empty))
            .ForMember(dest => dest.Street, opt => opt.MapFrom(src => src.Address.Street ?? string.Empty));
        CreateMap<AlternativeTMUpdateDto, AlternativeTM>()
            .ForMember(dest => dest.TmID, opt => opt.MapFrom(src => src.TmId != null ? ObjectId.Parse(src.TmId) : ObjectId.Empty))
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // Domain to Response
        CreateMap<AlternativeTM, AlternativeTMDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
            .ForMember(dest => dest.TmId, opt => opt.MapFrom(src => src.TmID.ToString()))
            .ForMember(dest => dest.Address, opt => opt.MapFrom(src => new AddressDto
            { City = src.City, County = src.County, District = src.District, Street = src.Street }));
        CreateMap<AlternativeTM, AlternativeTMSummaryDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()));
        CreateMap<AlternativeTM, AlternativeTMDetailDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
            .ForMember(dest => dest.TmId, opt => opt.MapFrom(src => src.TmID.ToString()));
    }
}