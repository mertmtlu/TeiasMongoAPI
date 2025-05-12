using AutoMapper;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Auth;
using TeiasMongoAPI.Services.DTOs.Request.User;
using TeiasMongoAPI.Services.DTOs.Response.User;

public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        // Request to Domain
        CreateMap<UserRegisterDto, User>()
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore()) // Will be hashed in service
            .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => new List<string> { UserRoles.Viewer }));
        CreateMap<UserUpdateDto, User>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // Domain to Response
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()));
        CreateMap<User, UserListDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()));
        CreateMap<User, UserDetailDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()))
            .ForMember(dest => dest.AssignedRegionIds, opt => opt.MapFrom(src => src.AssignedRegions.Select(r => r.ToString())))
            .ForMember(dest => dest.AssignedTMIds, opt => opt.MapFrom(src => src.AssignedTMs.Select(t => t.ToString())));
        CreateMap<User, UserProfileDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src._ID.ToString()));
    }
}