using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Region;
using TeiasMongoAPI.Services.DTOs.Response.Region;

namespace TeiasMongoAPI.Services.Mappings
{
    public class RegionMappingProfile : Profile
    {
        public RegionMappingProfile()
        {
            // Request to Domain
            CreateMap<RegionCreateDto, Region>()
                .ForMember(dest => dest.ClientID, opt => opt.MapFrom(src =>
                    string.IsNullOrEmpty(src.ClientId) ? ObjectId.Empty : ObjectId.Parse(src.ClientId)));

            CreateMap<RegionUpdateDto, Region>()
                .ForMember(dest => dest.ClientID, opt => opt.MapFrom(src =>
                    string.IsNullOrEmpty(src.ClientId) ? ObjectId.Empty : ObjectId.Parse(src.ClientId)))
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // Domain to Response
            CreateMap<Region, RegionResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ClientId, opt => opt.MapFrom(src => src.ClientID.ToString()))
                .ForMember(dest => dest.RegionId, opt => opt.MapFrom(src => src.Id)); // Numeric ID

            CreateMap<Region, RegionSummaryResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.RegionId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.CityCount, opt => opt.MapFrom(src => src.Cities.Count));

            CreateMap<Region, RegionListResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.RegionId, opt => opt.MapFrom(src => src.Id));

            CreateMap<Region, RegionDetailResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
                .ForMember(dest => dest.ClientId, opt => opt.MapFrom(src => src.ClientID.ToString()))
                .ForMember(dest => dest.RegionId, opt => opt.MapFrom(src => src.Id));
        }
    }
}