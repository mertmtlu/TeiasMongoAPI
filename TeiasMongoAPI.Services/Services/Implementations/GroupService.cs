using AutoMapper;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Group;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.Group;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;
using TeiasMongoAPI.Services.Specifications;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class GroupService : BaseService, IGroupService
    {
        public GroupService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<GroupService> logger)
            : base(unitOfWork, mapper, logger)
        {
        }

        public async Task<GroupDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                throw new ArgumentException("Invalid group ID format", nameof(id));
            }

            var group = await _unitOfWork.Groups.GetByIdAsync(objectId, cancellationToken);
            if (group == null)
            {
                throw new KeyNotFoundException($"Group with ID {id} not found");
            }

            var dto = _mapper.Map<GroupDto>(group);
            
            // Get creator name
            if (ObjectId.TryParse(group.CreatedBy, out var creatorId))
            {
                var creator = await _unitOfWork.Users.GetByIdAsync(creatorId, cancellationToken);
                dto.CreatedByName = creator?.FullName ?? "Unknown";
            }

            // Get member details
            dto.Members = await GetMemberDetailsAsync(group.Members, cancellationToken);
            dto.MemberCount = group.Members.Count;

            return dto;
        }

        public async Task<PagedResponse<GroupListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var spec = new GroupPaginationSpecification(pagination);
            var (groups, totalCount) = await _unitOfWork.Groups.FindWithSpecificationAsync(spec, cancellationToken);

            var dtos = _mapper.Map<List<GroupListDto>>(groups);
            
            // Populate creator names
            await PopulateCreatorNamesAsync(dtos, groups, cancellationToken);

            return new PagedResponse<GroupListDto>(dtos, pagination.PageNumber, pagination.PageSize, (int)totalCount);
        }

        public async Task<PagedResponse<GroupListDto>> SearchAsync(GroupSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var spec = new GroupSearchSpecification(searchDto, pagination);
            var (groups, totalCount) = await _unitOfWork.Groups.FindWithSpecificationAsync(spec, cancellationToken);

            var dtos = _mapper.Map<List<GroupListDto>>(groups);
            
            // Populate creator names
            await PopulateCreatorNamesAsync(dtos, groups, cancellationToken);

            return new PagedResponse<GroupListDto>(dtos, pagination.PageNumber, pagination.PageSize, (int)totalCount);
        }

        public async Task<IEnumerable<GroupListDto>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default)
        {
            var groups = await _unitOfWork.Groups.GetByCreatorAsync(creatorId, cancellationToken);
            return _mapper.Map<List<GroupListDto>>(groups);
        }

        public async Task<IEnumerable<GroupListDto>> GetUserGroupsAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(userId, out var userObjectId))
            {
                throw new ArgumentException("Invalid user ID format", nameof(userId));
            }

            var groups = await _unitOfWork.Groups.GetUserGroupsAsync(userObjectId, cancellationToken);
            return _mapper.Map<List<GroupListDto>>(groups);
        }

        public async Task<IEnumerable<GroupListDto>> GetActiveGroupsAsync(CancellationToken cancellationToken = default)
        {
            var groups = await _unitOfWork.Groups.GetActiveGroupsAsync(cancellationToken);
            return _mapper.Map<List<GroupListDto>>(groups);
        }

        public async Task<GroupDto> CreateAsync(GroupCreateDto dto, string? userId, CancellationToken cancellationToken = default)
        {
            if (!await _unitOfWork.Groups.IsNameUniqueAsync(dto.Name, null, cancellationToken))
            {
                throw new InvalidOperationException($"Group with name '{dto.Name}' already exists.");
            }

            var group = _mapper.Map<Group>(dto);
            group.CreatedBy = userId ?? "system";
            group.CreatedAt = DateTime.UtcNow;

            // Add initial members if provided
            if (dto.MemberIds.Any())
            {
                foreach (var memberId in dto.MemberIds)
                {
                    if (ObjectId.TryParse(memberId, out var memberObjectId))
                    {
                        var user = await _unitOfWork.Users.GetByIdAsync(memberObjectId, cancellationToken);
                        if (user != null)
                        {
                            group.Members.Add(memberObjectId);
                        }
                    }
                }
            }

            var createdGroup = await _unitOfWork.Groups.CreateAsync(group, cancellationToken);
            _logger.LogInformation("Created group {GroupId} with name {GroupName}", createdGroup._ID, createdGroup.Name);

            // Update user group memberships
            foreach (var memberId in group.Members)
            {
                await UpdateUserGroupMembershipAsync(memberId, createdGroup._ID, true, cancellationToken);
            }

            return await GetByIdAsync(createdGroup._ID.ToString(), cancellationToken);
        }

        public async Task<GroupDto> UpdateAsync(string id, GroupUpdateDto dto, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                throw new ArgumentException("Invalid group ID format", nameof(id));
            }

            var existingGroup = await _unitOfWork.Groups.GetByIdAsync(objectId, cancellationToken);
            if (existingGroup == null)
            {
                throw new KeyNotFoundException($"Group with ID {id} not found");
            }

            // Check name uniqueness if name is being changed
            if (!string.IsNullOrEmpty(dto.Name) && dto.Name != existingGroup.Name)
            {
                if (!await _unitOfWork.Groups.IsNameUniqueAsync(dto.Name, objectId, cancellationToken))
                {
                    throw new InvalidOperationException($"Group with name '{dto.Name}' already exists.");
                }
                existingGroup.Name = dto.Name;
            }

            if (!string.IsNullOrEmpty(dto.Description))
            {
                existingGroup.Description = dto.Description;
            }

            if (dto.IsActive.HasValue)
            {
                existingGroup.IsActive = dto.IsActive.Value;
            }

            if (dto.Metadata != null)
            {
                existingGroup.Metadata = dto.Metadata;
            }

            existingGroup.ModifiedAt = DateTime.UtcNow;

            await _unitOfWork.Groups.UpdateAsync(objectId, existingGroup, cancellationToken);
            _logger.LogInformation("Updated group {GroupId}", objectId);

            return await GetByIdAsync(id, cancellationToken);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                throw new ArgumentException("Invalid group ID format", nameof(id));
            }

            var group = await _unitOfWork.Groups.GetByIdAsync(objectId, cancellationToken);
            if (group == null)
            {
                return false;
            }

            // Remove group from all users
            foreach (var memberId in group.Members)
            {
                await UpdateUserGroupMembershipAsync(memberId, objectId, false, cancellationToken);
            }

            var result = await _unitOfWork.Groups.DeleteAsync(objectId, cancellationToken);
            if (result)
            {
                _logger.LogInformation("Deleted group {GroupId}", objectId);
            }

            return result;
        }

        public async Task<bool> UpdateStatusAsync(string id, bool isActive, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
            {
                throw new ArgumentException("Invalid group ID format", nameof(id));
            }

            var result = await _unitOfWork.Groups.UpdateStatusAsync(objectId, isActive, cancellationToken);
            if (result)
            {
                _logger.LogInformation("Updated group {GroupId} status to {Status}", objectId, isActive ? "active" : "inactive");
            }

            return result;
        }

        public async Task<bool> AddMemberAsync(string groupId, string userId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(groupId, out var groupObjectId))
            {
                throw new ArgumentException("Invalid group ID format", nameof(groupId));
            }

            if (!ObjectId.TryParse(userId, out var userObjectId))
            {
                throw new ArgumentException("Invalid user ID format", nameof(userId));
            }

            // Verify user exists
            var user = await _unitOfWork.Users.GetByIdAsync(userObjectId, cancellationToken);
            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {userId} not found");
            }

            var result = await _unitOfWork.Groups.AddMemberAsync(groupObjectId, userObjectId, cancellationToken);
            if (result)
            {
                await UpdateUserGroupMembershipAsync(userObjectId, groupObjectId, true, cancellationToken);
                _logger.LogInformation("Added user {UserId} to group {GroupId}", userId, groupId);
            }

            return result;
        }

        public async Task<bool> RemoveMemberAsync(string groupId, string userId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(groupId, out var groupObjectId))
            {
                throw new ArgumentException("Invalid group ID format", nameof(groupId));
            }

            if (!ObjectId.TryParse(userId, out var userObjectId))
            {
                throw new ArgumentException("Invalid user ID format", nameof(userId));
            }

            var result = await _unitOfWork.Groups.RemoveMemberAsync(groupObjectId, userObjectId, cancellationToken);
            if (result)
            {
                await UpdateUserGroupMembershipAsync(userObjectId, groupObjectId, false, cancellationToken);
                _logger.LogInformation("Removed user {UserId} from group {GroupId}", userId, groupId);
            }

            return result;
        }

        public async Task<bool> IsMemberAsync(string groupId, string userId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(groupId, out var groupObjectId))
            {
                return false;
            }

            if (!ObjectId.TryParse(userId, out var userObjectId))
            {
                return false;
            }

            return await _unitOfWork.Groups.IsMemberAsync(groupObjectId, userObjectId, cancellationToken);
        }

        public async Task<IEnumerable<GroupMemberDto>> GetMembersAsync(string groupId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(groupId, out var groupObjectId))
            {
                throw new ArgumentException("Invalid group ID format", nameof(groupId));
            }

            var memberIds = await _unitOfWork.Groups.GetGroupMembersAsync(groupObjectId, cancellationToken);
            return await GetMemberDetailsAsync(memberIds.ToList(), cancellationToken);
        }

        public async Task<bool> AddMembersAsync(string groupId, List<string> userIds, CancellationToken cancellationToken = default)
        {
            var allSuccessful = true;
            foreach (var userId in userIds)
            {
                try
                {
                    var result = await AddMemberAsync(groupId, userId, cancellationToken);
                    if (!result)
                    {
                        allSuccessful = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add user {UserId} to group {GroupId}", userId, groupId);
                    allSuccessful = false;
                }
            }

            return allSuccessful;
        }

        public async Task<bool> RemoveMembersAsync(string groupId, List<string> userIds, CancellationToken cancellationToken = default)
        {
            var allSuccessful = true;
            foreach (var userId in userIds)
            {
                try
                {
                    var result = await RemoveMemberAsync(groupId, userId, cancellationToken);
                    if (!result)
                    {
                        allSuccessful = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove user {UserId} from group {GroupId}", userId, groupId);
                    allSuccessful = false;
                }
            }

            return allSuccessful;
        }

        public async Task<bool> ValidateUserGroupAccessAsync(string groupId, string userId, CancellationToken cancellationToken = default)
        {
            return await IsMemberAsync(groupId, userId, cancellationToken);
        }

        private async Task<List<GroupMemberDto>> GetMemberDetailsAsync(List<ObjectId> memberIds, CancellationToken cancellationToken)
        {
            var members = new List<GroupMemberDto>();

            foreach (var memberId in memberIds)
            {
                var user = await _unitOfWork.Users.GetByIdAsync(memberId, cancellationToken);
                if (user != null)
                {
                    members.Add(new GroupMemberDto
                    {
                        UserId = user._ID.ToString(),
                        Username = user.Username,
                        FullName = user.FullName,
                        Email = user.Email,
                        JoinedAt = DateTime.UtcNow, // This would ideally come from membership tracking
                        IsActive = user.IsActive
                    });
                }
            }

            return members;
        }

        private async Task PopulateCreatorNamesAsync(List<GroupListDto> dtos, IEnumerable<Group> groups, CancellationToken cancellationToken)
        {
            var creatorIds = groups.Select(g => g.CreatedBy).Distinct().ToList();
            var creators = new Dictionary<string, string>();

            foreach (var creatorId in creatorIds)
            {
                if (ObjectId.TryParse(creatorId, out var objectId))
                {
                    var creator = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);
                    if (creator != null)
                    {
                        creators[creatorId] = creator.FullName;
                    }
                }
            }

            foreach (var dto in dtos)
            {
                var group = groups.FirstOrDefault(g => g._ID.ToString() == dto.Id);
                if (group != null && creators.ContainsKey(group.CreatedBy))
                {
                    dto.CreatedByName = creators[group.CreatedBy];
                }
                else
                {
                    dto.CreatedByName = "Unknown";
                }

                dto.MemberCount = group?.Members.Count ?? 0;
            }
        }

        private async Task UpdateUserGroupMembershipAsync(ObjectId userId, ObjectId groupId, bool isAdding, CancellationToken cancellationToken)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
            if (user != null)
            {
                if (isAdding && !user.Groups.Contains(groupId))
                {
                    user.Groups.Add(groupId);
                }
                else if (!isAdding)
                {
                    user.Groups.Remove(groupId);
                }

                user.ModifiedDate = DateTime.UtcNow;
                await _unitOfWork.Users.UpdateAsync(userId, user, cancellationToken);
            }
        }
    }
}