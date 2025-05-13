using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.AlternativeTM;
using TeiasMongoAPI.Services.DTOs.Response.AlternativeTM;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Mappings
{
    public class AlternativeTMMappingProfile : Profile
    {
        public AlternativeTMMappingProfile()
        {
            // Request to Domain
            CreateMap<AlternativeTMCreateDto, AlternativeTM>()
                .ForMember(dest => dest.TmID, opt => opt.MapFrom(src =>
                    string.IsNullOrEmpty(src.TmId) ? ObjectId.Empty : ObjectId.Parse(src.TmId)))
                .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.Address != null ? src.Address.City ?? string.Empty : string.Empty))
                .ForMember(dest => dest.County, opt => opt.MapFrom(src => src.Address != null ? src.Address.County ?? string.Empty : string.Empty))
                .ForMember(dest => dest.District, opt => opt.MapFrom(src => src.Address != null ? src.Address.District ?? string.Empty : string.Empty))
                .ForMember(dest => dest.Street, opt => opt.MapFrom(src => src.Address != null ? src.Address.Street ?? string.Empty : string.Empty));

            CreateMap<AlternativeTMUpdateDto, AlternativeTM>()
                .ForMember(dest => dest.TmID, opt => opt.MapFrom(src =>
                    string.IsNullOrEmpty(src.TmId) ? ObjectId.Empty : ObjectId.Parse(src.TmId)))
                .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.Address != null ? src.Address.City ?? string.Empty : string.Empty))
                .ForMember(dest => dest.County, opt => opt.MapFrom(src => src.Address != null ? src.Address.County ?? string.Empty : string.Empty))
                .ForMember(dest => dest.District, opt => opt.MapFrom(src => src.Address != null ? src.Address.District ?? string.Empty : string.Empty))
                .ForMember(dest => dest.Street, opt => opt.MapFrom(src => src.Address != null ? src.Address.Street ?? string.Empty : string.Empty))
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // Domain to Response
            CreateMap<AlternativeTM, AlternativeTMResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.TmId, opt => opt.MapFrom(src => src.TmID.ToString()))
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => new AddressResponseDto
                {
                    City = src.City,
                    County = src.County,
                    District = src.District,
                    Street = src.Street
                }));

            CreateMap<AlternativeTM, AlternativeTMSummaryResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.OverallRiskScore, opt => opt.MapFrom(src => CalculateOverallRiskScore(src)));

            CreateMap<AlternativeTM, AlternativeTMDetailResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.TmId, opt => opt.MapFrom(src => src.TmID.ToString()))
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => new AddressResponseDto
                {
                    City = src.City,
                    County = src.County,
                    District = src.District,
                    Street = src.Street
                }));
        }

        private static double CalculateOverallRiskScore(AlternativeTM tm)
        {
            // Simplified calculation - implement actual business logic
            var scores = new[]
            {
                tm.FireHazard.Score,
                tm.SecurityHazard.Score,
                tm.NoiseHazard.Score,
                tm.AvalancheHazard.Score,
                tm.LandslideHazard.Score,
                tm.RockFallHazard.Score,
                tm.FloodHazard.Score,
                tm.TsunamiHazard.Score
            };
            return scores.Average();
        }
    }
}