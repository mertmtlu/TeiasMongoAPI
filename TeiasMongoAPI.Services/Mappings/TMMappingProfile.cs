using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.TM;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.TM;

namespace TeiasMongoAPI.Services.Mappings
{
    public class TMMappingProfile : Profile
    {
        public TMMappingProfile()
        {
            // Request to Domain
            CreateMap<TMCreateDto, TM>()
                .ForMember(dest => dest.RegionID, opt => opt.MapFrom(src =>
                    string.IsNullOrEmpty(src.RegionId) ? ObjectId.Empty : ObjectId.Parse(src.RegionId)))
                .ForMember(dest => dest.ProvisionalAcceptanceDate, opt => opt.MapFrom(src =>
                    src.ProvisionalAcceptanceDate.HasValue ? src.ProvisionalAcceptanceDate.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue))
                .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.Address != null ? src.Address.City ?? string.Empty : string.Empty))
                .ForMember(dest => dest.County, opt => opt.MapFrom(src => src.Address != null ? src.Address.County ?? string.Empty : string.Empty))
                .ForMember(dest => dest.District, opt => opt.MapFrom(src => src.Address != null ? src.Address.District ?? string.Empty : string.Empty))
                .ForMember(dest => dest.Street, opt => opt.MapFrom(src => src.Address != null ? src.Address.Street ?? string.Empty : string.Empty));

            CreateMap<TMUpdateDto, TM>()
                .ForMember(dest => dest.RegionID, opt => opt.MapFrom(src =>
                    string.IsNullOrEmpty(src.RegionId) ? ObjectId.Empty : ObjectId.Parse(src.RegionId)))
                .ForMember(dest => dest.ProvisionalAcceptanceDate, opt => opt.MapFrom(src =>
                    src.ProvisionalAcceptanceDate.HasValue ? src.ProvisionalAcceptanceDate.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue))
                .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.Address != null ? src.Address.City ?? string.Empty : string.Empty))
                .ForMember(dest => dest.County, opt => opt.MapFrom(src => src.Address != null ? src.Address.County ?? string.Empty : string.Empty))
                .ForMember(dest => dest.District, opt => opt.MapFrom(src => src.Address != null ? src.Address.District ?? string.Empty : string.Empty))
                .ForMember(dest => dest.Street, opt => opt.MapFrom(src => src.Address != null ? src.Address.Street ?? string.Empty : string.Empty))
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // Domain to Response
            CreateMap<TM, TMResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.RegionId, opt => opt.MapFrom(src => src.RegionID.ToString()))
                .ForMember(dest => dest.TMId, opt => opt.MapFrom(src => src.TmID))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State.ToString()))
                .ForMember(dest => dest.Address, opt => opt.MapFrom(src => new AddressResponseDto
                {
                    City = src.City,
                    County = src.County,
                    District = src.District,
                    Street = src.Street
                }));

            CreateMap<TM, TMSummaryResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.TMId, opt => opt.MapFrom(src => src.TmID))
                .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State.ToString()));

            CreateMap<TM, TMListResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.TMId, opt => opt.MapFrom(src => src.TmID))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State.ToString()));

            CreateMap<TM, TMDetailResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.RegionId, opt => opt.MapFrom(src => src.RegionID.ToString()))
                .ForMember(dest => dest.TMId, opt => opt.MapFrom(src => src.TmID))
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.State.ToString()));
        }
    }
}