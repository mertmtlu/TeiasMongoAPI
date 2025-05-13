using TeiasMongoAPI.Services.DTOs.Request.Auth;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Search;
using TeiasMongoAPI.Services.DTOs.Request.User;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.User;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IUserService
    {
        Task<UserDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<UserListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<UserListDto>> SearchAsync(UserSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<UserDto> CreateAsync(UserRegisterDto dto, CancellationToken cancellationToken = default);
        Task<UserDto> UpdateAsync(string id, UserUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<UserDto> UpdateRolesAsync(string id, UserRoleUpdateDto dto, CancellationToken cancellationToken = default);
        Task<UserDto> UpdatePermissionsAsync(string id, UserPermissionUpdateDto dto, CancellationToken cancellationToken = default);
        Task<UserDto> AssignRegionsAsync(string id, UserRegionAssignmentDto dto, CancellationToken cancellationToken = default);
        Task<UserDto> AssignTMsAsync(string id, UserTMAssignmentDto dto, CancellationToken cancellationToken = default);
        Task<UserDto> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<UserDto> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
        Task<bool> ActivateAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> DeactivateAsync(string id, CancellationToken cancellationToken = default);
        Task<UserProfileDto> GetProfileAsync(string id, CancellationToken cancellationToken = default);
    }
}