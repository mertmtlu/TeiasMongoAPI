using AutoMapper;
using TeiasMongoAPI.Core.Models.Block;
using TeiasMongoAPI.Services.DTOs.Request.Block;
using TeiasMongoAPI.Services.DTOs.Response.Block;

namespace TeiasMongoAPI.Services.Mappings
{
    public class BlockMappingProfile : Profile
    {
        public BlockMappingProfile()
        {
            // Request to Domain
            CreateMap<ConcreteCreateDto, Concrete>();
            CreateMap<ConcreteUpdateDto, Concrete>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
            CreateMap<MasonryCreateDto, Masonry>();
            CreateMap<MasonryUpdateDto, Masonry>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // Domain to Response
            CreateMap<ABlock, BlockResponseDto>()
                .ForMember(dest => dest.ModelingType, opt => opt.MapFrom(src => src.ModelingType.ToString()))
                .IncludeAllDerived();

            CreateMap<Concrete, ConcreteBlockResponseDto>()
                .ForMember(dest => dest.ModelingType, opt => opt.MapFrom(src => src.ModelingType.ToString()));

            CreateMap<Masonry, MasonryBlockResponseDto>()
                .ForMember(dest => dest.ModelingType, opt => opt.MapFrom(src => src.ModelingType.ToString()));

            CreateMap<ABlock, BlockSummaryResponseDto>()
                .ForMember(dest => dest.ModelingType, opt => opt.MapFrom(src => src.ModelingType.ToString()))
                .ForMember(dest => dest.StoreyCount, opt => opt.MapFrom(src => src.StoreyHeight.Count));

            // MasonryUnitType mapping
            CreateMap<MasonryUnitType, MasonryUnitTypeResponseDto>();
        }
    }
}