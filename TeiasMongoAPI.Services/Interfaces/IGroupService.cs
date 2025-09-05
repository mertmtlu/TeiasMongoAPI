using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Services.DTOs.Request.Group;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Group;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IGroupService
    {
        Task<GroupDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<GroupListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<GroupListDto>> SearchAsync(GroupSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<IEnumerable<GroupListDto>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default);
        Task<IEnumerable<GroupListDto>> GetUserGroupsAsync(string userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<GroupListDto>> GetActiveGroupsAsync(CancellationToken cancellationToken = default);
        
        Task<GroupDto> CreateAsync(GroupCreateDto dto, string? userId, CancellationToken cancellationToken = default);
        Task<GroupDto> UpdateAsync(string id, GroupUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> UpdateStatusAsync(string id, bool isActive, CancellationToken cancellationToken = default);

        Task<bool> AddMemberAsync(string groupId, string userId, CancellationToken cancellationToken = default);
        Task<bool> RemoveMemberAsync(string groupId, string userId, CancellationToken cancellationToken = default);
        Task<bool> IsMemberAsync(string groupId, string userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<GroupMemberDto>> GetMembersAsync(string groupId, CancellationToken cancellationToken = default);
        Task<bool> AddMembersAsync(string groupId, List<string> userIds, CancellationToken cancellationToken = default);
        Task<bool> RemoveMembersAsync(string groupId, List<string> userIds, CancellationToken cancellationToken = default);

        Task<bool> ValidateUserGroupAccessAsync(string groupId, string userId, CancellationToken cancellationToken = default);
    }
}