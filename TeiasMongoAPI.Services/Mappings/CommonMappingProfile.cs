using AutoMapper;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Core.Models.TMRelatedProperties;

namespace TeiasMongoAPI.Services.Mappings
{
    public class CommonMappingProfile : Profile
    {
        public CommonMappingProfile()
        {
            // Location mappings
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Common.LocationDto, Location>();
            CreateMap<Location, TeiasMongoAPI.Services.DTOs.Response.Common.LocationDto>();

            // Address mappings - to TM model (using fully qualified names)
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Common.AddressDto, TM>()
                .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.City ?? string.Empty))
                .ForMember(dest => dest.County, opt => opt.MapFrom(src => src.County ?? string.Empty))
                .ForMember(dest => dest.District, opt => opt.MapFrom(src => src.District ?? string.Empty))
                .ForMember(dest => dest.Street, opt => opt.MapFrom(src => src.Street ?? string.Empty));

            // Address mappings - to AlternativeTM model (using fully qualified names)
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Common.AddressDto, AlternativeTM>()
                .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.City ?? string.Empty))
                .ForMember(dest => dest.County, opt => opt.MapFrom(src => src.County ?? string.Empty))
                .ForMember(dest => dest.District, opt => opt.MapFrom(src => src.District ?? string.Empty))
                .ForMember(dest => dest.Street, opt => opt.MapFrom(src => src.Street ?? string.Empty));

            // Earthquake level mappings (using fully qualified names)
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Common.EarthquakeLevelDto, EarthquakeLevel>();
            CreateMap<EarthquakeLevel, TeiasMongoAPI.Services.DTOs.Response.TM.EarthquakeLevelDto>();

            // Soil mappings (using fully qualified names)
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Common.SoilDto, Soil>()
                .ForMember(dest => dest.SoilClassDataSource, opt => opt.MapFrom(src => src.SoilClassDataSource ?? string.Empty))
                .ForMember(dest => dest.GeotechnicalReport, opt => opt.MapFrom(src => src.GeotechnicalReport ?? string.Empty))
                .ForMember(dest => dest.Results, opt => opt.MapFrom(src => src.Results ?? string.Empty))
                .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.Notes ?? string.Empty))
                .ForMember(dest => dest.NewSoilClassDataReport, opt => opt.MapFrom(src => src.NewSoilClassDataReport ?? string.Empty))
                .ForMember(dest => dest.NewLiquefactionRiskDataReport, opt => opt.MapFrom(src => src.NewLiquefactionRiskDataReport ?? string.Empty))
                .ForMember(dest => dest.GeotechnicalReportMTV, opt => opt.MapFrom(src => src.GeotechnicalReportMTV ?? string.Empty))
                .ForMember(dest => dest.LiquefactionRiskGeotechnicalReport, opt => opt.MapFrom(src => src.LiquefactionRiskGeotechnicalReport ?? string.Empty))
                .ForMember(dest => dest.StructureType, opt => opt.MapFrom(src => src.StructureType ?? string.Empty))
                .ForMember(dest => dest.VASS, opt => opt.MapFrom(src => src.VASS ?? string.Empty));

            CreateMap<Soil, TeiasMongoAPI.Services.DTOs.Response.TM.SoilDto>()
                .ForMember(dest => dest.SoilClassTDY2007, opt => opt.MapFrom(src => src.SoilClassTDY2007.ToString()))
                .ForMember(dest => dest.SoilClassTBDY2018, opt => opt.MapFrom(src => src.SoilClassTBDY2018.ToString()))
                .ForMember(dest => dest.FinalDecisionOnOldData, opt => opt.MapFrom(src => src.FinalDecisionOnOldData.ToString()))
                .ForMember(dest => dest.FinalSoilClassification, opt => opt.MapFrom(src => src.FinalSoilClassification.ToString()));
        }
    }
}