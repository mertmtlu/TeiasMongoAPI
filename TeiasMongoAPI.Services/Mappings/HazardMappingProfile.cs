using AutoMapper;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Core.Models.Hazard;
using TeiasMongoAPI.Core.Models.Hazard.MongoAPI.Models.Hazards;
using TeiasMongoAPI.Core.Models.TMRelatedProperties;
using TeiasMongoAPI.Services.DTOs.Request.Hazard;
using TeiasMongoAPI.Services.DTOs.Response.Hazard;

namespace TeiasMongoAPI.Services.Mappings
{
    public class HazardMappingProfile : Profile
    {
        public HazardMappingProfile()
        {
            // Pollution (special case - not inheriting from AHazard)
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Hazard.PollutionDto, Pollution>();
            CreateMap<Pollution, TeiasMongoAPI.Services.DTOs.Response.Hazard.PollutionDto>()
                .ForMember(dest => dest.PollutantLevel, opt => opt.MapFrom(src => src.PollutantLevel.ToString()));

            // Fire Hazard
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Hazard.FireHazardDto, FireHazard>()
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertStringDictionaryToEnum<FireEliminationMethod>(src.EliminationCosts)));
            CreateMap<FireHazard, TeiasMongoAPI.Services.DTOs.Response.Hazard.FireHazardDto>()
                .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()))
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertEnumDictionaryToString(src.EliminationCosts)));

            // Security Hazard  
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Hazard.SecurityHazardDto, SecurityHazard>()
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertStringDictionaryToEnum<SecurityEliminationMethod>(src.EliminationCosts)));
            CreateMap<SecurityHazard, TeiasMongoAPI.Services.DTOs.Response.Hazard.SecurityHazardDto>()
                .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()))
                .ForMember(dest => dest.PerimeterFenceType, opt => opt.MapFrom(src => src.PerimeterFenceType.ToString()))
                .ForMember(dest => dest.WallCondition, opt => opt.MapFrom(src => src.WallCondition.ToString()))
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertEnumDictionaryToString(src.EliminationCosts)));

            // Noise Hazard
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Hazard.NoiseHazardDto, NoiseHazard>()
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertStringDictionaryToEnum<NoiseEliminationMethod>(src.EliminationCosts)));
            CreateMap<NoiseHazard, TeiasMongoAPI.Services.DTOs.Response.Hazard.NoiseHazardDto>()
                .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()))
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertEnumDictionaryToString(src.EliminationCosts)));

            // Avalanche Hazard
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Hazard.AvalancheHazardDto, AvalancheHazard>()
                .ForMember(dest => dest.FirstHillLocation, opt => opt.MapFrom(src => src.FirstHillLocation))
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertStringDictionaryToEnum<AvalancheEliminationMethod>(src.EliminationCosts)));
            CreateMap<AvalancheHazard, TeiasMongoAPI.Services.DTOs.Response.Hazard.AvalancheHazardDto>()
                .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()))
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertEnumDictionaryToString(src.EliminationCosts)));

            // Landslide Hazard
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Hazard.LandslideHazardDto, LandslideHazard>()
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertStringDictionaryToEnum<LandslideEliminationMethod>(src.EliminationCosts)));
            CreateMap<LandslideHazard, TeiasMongoAPI.Services.DTOs.Response.Hazard.LandslideHazardDto>()
                .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()))
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertEnumDictionaryToString(src.EliminationCosts)));

            // RockFall Hazard
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Hazard.RockFallHazardDto, RockFallHazard>()
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertStringDictionaryToEnum<RockFallEliminationMethod>(src.EliminationCosts)));
            CreateMap<RockFallHazard, TeiasMongoAPI.Services.DTOs.Response.Hazard.RockFallHazardDto>()
                .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()))
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertEnumDictionaryToString(src.EliminationCosts)));

            // Flood Hazard
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Hazard.FloodHazardDto, FloodHazard>()
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertStringDictionaryToEnum<FloodEliminationMethod>(src.EliminationCosts)));
            CreateMap<FloodHazard, TeiasMongoAPI.Services.DTOs.Response.Hazard.FloodHazardDto>()
                .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()))
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertEnumDictionaryToString(src.EliminationCosts)));

            // Tsunami Hazard
            CreateMap<TeiasMongoAPI.Services.DTOs.Request.Hazard.TsunamiHazardDto, TsunamiHazard>()
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertStringDictionaryToEnum<TsunamiEliminationMethod>(src.EliminationCosts)));
            CreateMap<TsunamiHazard, TeiasMongoAPI.Services.DTOs.Response.Hazard.TsunamiHazardDto>()
                .ForMember(dest => dest.Level, opt => opt.MapFrom(src => src.Level.ToString()))
                .ForMember(dest => dest.EliminationCosts, opt => opt.MapFrom(src =>
                    ConvertEnumDictionaryToString(src.EliminationCosts)));
        }

        // Fixed the generic constraint error
        private static Dictionary<T, int>? ConvertStringDictionaryToEnum<T>(Dictionary<string, int>? source) where T : struct, Enum
        {
            if (source == null) return null;

            var result = new Dictionary<T, int>();
            foreach (var kvp in source)
            {
                if (Enum.TryParse<T>(kvp.Key, out var enumValue))
                {
                    result[enumValue] = kvp.Value;
                }
            }
            return result;
        }

        private static Dictionary<string, int> ConvertEnumDictionaryToString<T>(Dictionary<T, int> source) where T : Enum
        {
            if (source == null) return new Dictionary<string, int>();

            return source.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
        }
    }
}