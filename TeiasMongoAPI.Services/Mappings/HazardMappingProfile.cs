using AutoMapper;
using TeiasMongoAPI.Core.Models.Hazard;
using TeiasMongoAPI.Core.Models.TMRelatedProperties;

public class HazardMappingProfile : Profile
{
    public HazardMappingProfile()
    {
        // Pollution
        CreateMap<PollutionDto, Pollution>();
        CreateMap<Pollution, Services.DTOs.Response.Hazard.PollutionDto>()
            .ForMember(dest => dest.PollutantLevel, opt => opt.MapFrom(src => src.PollutantLevel.ToString()));

        // Fire Hazard
        CreateMap<FireHazardDto, FireHazard>();
        CreateMap<FireHazard, Services.DTOs.Response.Hazard.FireHazardDto>()
            .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()));

        // Security Hazard  
        CreateMap<SecurityHazardDto, SecurityHazard>();
        CreateMap<SecurityHazard, Services.DTOs.Response.Hazard.SecurityHazardDto>()
            .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()))
            .ForMember(dest => dest.PerimeterFenceType, opt => opt.MapFrom(src => src.PerimeterFenceType.ToString()))
            .ForMember(dest => dest.WallCondition, opt => opt.MapFrom(src => src.WallCondition.ToString()));

        // Noise Hazard
        CreateMap<NoiseHazardDto, NoiseHazard>();
        CreateMap<NoiseHazard, Services.DTOs.Response.Hazard.NoiseHazardDto>()
            .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()));

        // Other hazards
        CreateMap<AvalancheHazardDto, AvalancheHazard>();
        CreateMap<AvalancheHazard, Services.DTOs.Response.Hazard.AvalancheHazardDto>()
            .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()));

        CreateMap<LandslideHazardDto, LandslideHazard>();
        CreateMap<LandslideHazard, Services.DTOs.Response.Hazard.LandslideHazardDto>()
            .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()));

        CreateMap<RockFallHazardDto, RockFallHazard>();
        CreateMap<RockFallHazard, Services.DTOs.Response.Hazard.RockFallHazardDto>()
            .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()));

        CreateMap<FloodHazardDto, FloodHazard>();
        CreateMap<FloodHazard, Services.DTOs.Response.Hazard.FloodHazardDto>()
            .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()));

        CreateMap<TsunamiHazardDto, TsunamiHazard>();
        CreateMap<TsunamiHazard, Services.DTOs.Response.Hazard.TsunamiHazardDto>()
            .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()));
    }
}