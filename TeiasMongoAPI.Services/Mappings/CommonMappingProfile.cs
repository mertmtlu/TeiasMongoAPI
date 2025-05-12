using AutoMapper;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Core.Models.KeyModels;

public class CommonMappingProfile : Profile
{
    public CommonMappingProfile()
    {
        // Location mappings
        CreateMap<LocationDto, Location>();
        CreateMap<Location, LocationDto>();
        CreateMap<Services.DTOs.Request.Common.LocationDto, Core.Models.Common.Location>();
        CreateMap<Core.Models.Common.Location, Services.DTOs.Response.Common.LocationDto>();

        // Address mappings
        CreateMap<Services.DTOs.Request.Common.AddressDto, TM>()
            .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.City ?? string.Empty))
            .ForMember(dest => dest.County, opt => opt.MapFrom(src => src.County ?? string.Empty))
            .ForMember(dest => dest.District, opt => opt.MapFrom(src => src.District ?? string.Empty))
            .ForMember(dest => dest.Street, opt => opt.MapFrom(src => src.Street ?? string.Empty));

        // Earthquake level mappings
        CreateMap<Services.DTOs.Request.Common.EarthquakeLevelDto, Core.Models.TMRelatedProperties.EarthquakeLevel>();
        CreateMap<Core.Models.TMRelatedProperties.EarthquakeLevel, Services.DTOs.Response.TM.EarthquakeLevelDto>();

        // Soil mappings
        CreateMap<Services.DTOs.Request.Common.SoilDto, Core.Models.TMRelatedProperties.Soil>();
        CreateMap<Core.Models.TMRelatedProperties.Soil, Services.DTOs.Response.TM.SoilDto>()
            .ForMember(dest => dest.SoilClassTDY2007, opt => opt.MapFrom(src => src.SoilClassTDY2007.ToString()))
            .ForMember(dest => dest.SoilClassTBDY2018, opt => opt.MapFrom(src => src.SoilClassTBDY2018.ToString()))
            .ForMember(dest => dest.FinalDecisionOnOldData, opt => opt.MapFrom(src => src.FinalDecisionOnOldData.ToString()))
            .ForMember(dest => dest.FinalSoilClassification, opt => opt.MapFrom(src => src.FinalSoilClassification.ToString()));
    }
}