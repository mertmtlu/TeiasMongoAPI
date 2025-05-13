using AutoMapper;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Auth;
using TeiasMongoAPI.Services.DTOs.Request.User;
using TeiasMongoAPI.Services.DTOs.Response.User;

namespace TeiasMongoAPI.Services.Mappings
{
    public class UserMappingProfile : Profile
    {
        public UserMappingProfile()
        {
            // Request to Domain
            CreateMap<UserRegisterDto, User>()
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore()) // Will be hashed in service
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.Username.ToLower()))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email.ToLower()))
                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName ?? string.Empty))
                .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName ?? string.Empty))
                .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => new List<string> { UserRoles.Viewer }))
                .ForMember(dest => dest.Permissions, opt => opt.Ignore()) // Will be set based on roles in service
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.IsEmailVerified, opt => opt.MapFrom(src => false))
                .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => DateTime.UtcNow));

            CreateMap<UserUpdateDto, User>()
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email != null ? src.Email.ToLower() : null))
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
}