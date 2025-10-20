using AutoMapper;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.RemoteApp;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.RemoteApp;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;
using TeiasMongoAPI.Services.Specifications;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class RemoteAppService : BaseService, IRemoteAppService
    {
        public RemoteAppService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<RemoteAppService> logger)
            : base(unitOfWork, mapper, logger)
        {
        }

        public async Task<PagedResponse<RemoteAppListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var spec = new AllRemoteAppsSpecification(pagination);
            var (remoteApps, totalCount) = await _unitOfWork.RemoteApps.FindWithSpecificationAsync(spec, cancellationToken);
            var dtos = _mapper.Map<List<RemoteAppListDto>>(remoteApps);

            return new PagedResponse<RemoteAppListDto>(dtos, pagination.PageNumber, pagination.PageSize, (int)totalCount);
        }

        public async Task<RemoteAppDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(objectId, cancellationToken);
            if (remoteApp == null)
                throw new KeyNotFoundException($"Remote app with ID {id} not found");

            var dto = _mapper.Map<RemoteAppDetailDto>(remoteApp);

            // Populate permission details
            var permissions = new List<RemoteAppPermissionDto>();

            // Add user permissions
            foreach (var userPerm in remoteApp.Permissions.Users)
            {
                try
                {
                    if (ObjectId.TryParse(userPerm.UserId, out var userObjectId))
                    {
                        var user = await _unitOfWork.Users.GetByIdAsync(userObjectId, cancellationToken);
                        if (user != null)
                        {
                            permissions.Add(new RemoteAppPermissionDto
                            {
                                Type = "user",
                                Id = userPerm.UserId,
                                Name = user.FullName,
                                AccessLevel = userPerm.AccessLevel
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get user {UserId} for remote app permissions", userPerm.UserId);
                }
            }

            // Add group permissions
            foreach (var groupPerm in remoteApp.Permissions.Groups)
            {
                permissions.Add(new RemoteAppPermissionDto
                {
                    Type = "group",
                    Id = groupPerm.GroupId,
                    Name = $"Group {groupPerm.GroupId}",
                    AccessLevel = groupPerm.AccessLevel
                });
            }

            dto.Permissions = permissions;

            // Populate creator name
            if (ObjectId.TryParse(remoteApp.Creator, out var creatorObjectId))
            {
                var creator = await _unitOfWork.Users.GetByIdAsync(creatorObjectId, cancellationToken);
                if (creator != null)
                {
                    dto.CreatorName = creator.FullName;
                }
            }

            return dto;
        }

        public async Task<RemoteAppDto> CreateAsync(RemoteAppCreateDto dto, string creatorId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(creatorId, out var creatorObjectId))
                throw new ArgumentException("Invalid creator ID format", nameof(creatorId));

            // Validate name uniqueness
            if (!await _unitOfWork.RemoteApps.IsNameUniqueAsync(dto.Name, cancellationToken: cancellationToken))
                throw new InvalidOperationException($"Remote app with name '{dto.Name}' already exists");

            var remoteApp = _mapper.Map<RemoteApp>(dto);
            remoteApp.Creator = creatorId;
            remoteApp.CreatedAt = DateTime.UtcNow;
            remoteApp.DefaultUsername = dto.DefaultUsername;
            remoteApp.DefaultPassword = dto.DefaultPassword;
            remoteApp.SsoUrl = dto.SsoUrl;

            var createdRemoteApp = await _unitOfWork.RemoteApps.CreateAsync(remoteApp, cancellationToken);
            return _mapper.Map<RemoteAppDto>(createdRemoteApp);
        }

        public async Task<RemoteAppDto> UpdateAsync(string id, RemoteAppUpdateDto dto, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            var existingRemoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(objectId, cancellationToken);
            if (existingRemoteApp == null)
                throw new KeyNotFoundException($"Remote app with ID {id} not found");

            // Validate name uniqueness if name is being updated
            if (!string.IsNullOrEmpty(dto.Name) && dto.Name != existingRemoteApp.Name)
            {
                if (!await _unitOfWork.RemoteApps.IsNameUniqueAsync(dto.Name, objectId, cancellationToken))
                    throw new InvalidOperationException($"Remote app with name '{dto.Name}' already exists");
            }

            // Update fields
            if (!string.IsNullOrEmpty(dto.Name))
                existingRemoteApp.Name = dto.Name;
            
            if (dto.Description != null)
                existingRemoteApp.Description = dto.Description;
            
            if (!string.IsNullOrEmpty(dto.Url))
                existingRemoteApp.Url = dto.Url;
            
            if (dto.IsPublic.HasValue)
                existingRemoteApp.IsPublic = dto.IsPublic.Value;
            
            if (dto.DefaultUsername != null)
                existingRemoteApp.DefaultUsername = dto.DefaultUsername;
            
            if (dto.DefaultPassword != null)
                existingRemoteApp.DefaultPassword = dto.DefaultPassword;
            
            if (dto.SsoUrl != null)
                existingRemoteApp.SsoUrl = dto.SsoUrl;

            existingRemoteApp.ModifiedAt = DateTime.UtcNow;

            await _unitOfWork.RemoteApps.UpdateAsync(objectId, existingRemoteApp, cancellationToken);
            return _mapper.Map<RemoteAppDto>(existingRemoteApp);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            return await _unitOfWork.RemoteApps.DeleteAsync(objectId, cancellationToken);
        }

        public async Task<PagedResponse<RemoteAppListDto>> GetByCreatorAsync(string creatorId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var spec = new RemoteAppsByCreatorSpecification(creatorId, pagination);
            var (remoteApps, totalCount) = await _unitOfWork.RemoteApps.FindWithSpecificationAsync(spec, cancellationToken);
            var dtos = _mapper.Map<List<RemoteAppListDto>>(remoteApps);

            return new PagedResponse<RemoteAppListDto>(dtos, pagination.PageNumber, pagination.PageSize, (int)totalCount);
        }

        public async Task<PagedResponse<RemoteAppListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var spec = new RemoteAppsByStatusSpecification(status, pagination);
            var (remoteApps, totalCount) = await _unitOfWork.RemoteApps.FindWithSpecificationAsync(spec, cancellationToken);
            var dtos = _mapper.Map<List<RemoteAppListDto>>(remoteApps);

            return new PagedResponse<RemoteAppListDto>(dtos, pagination.PageNumber, pagination.PageSize, (int)totalCount);
        }

        public async Task<PagedResponse<RemoteAppListDto>> GetUserAccessibleAppsAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(userId, out var userObjectId))
                throw new ArgumentException("Invalid user ID format", nameof(userId));

            // Get user's group memberships
            var user = await _unitOfWork.Users.GetByIdAsync(userObjectId, cancellationToken);
            var userGroupIds = user?.Groups;

            var spec = new RemoteAppsUserAccessibleSpecification(userId, userGroupIds, pagination);
            var (remoteApps, totalCount) = await _unitOfWork.RemoteApps.FindWithSpecificationAsync(spec, cancellationToken);
            var dtos = _mapper.Map<List<RemoteAppListDto>>(remoteApps);

            return new PagedResponse<RemoteAppListDto>(dtos, pagination.PageNumber, pagination.PageSize, (int)totalCount);
        }

        public async Task<PagedResponse<RemoteAppListDto>> GetPublicAppsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var spec = new RemoteAppsPublicSpecification(pagination);
            var (remoteApps, totalCount) = await _unitOfWork.RemoteApps.FindWithSpecificationAsync(spec, cancellationToken);
            var dtos = _mapper.Map<List<RemoteAppListDto>>(remoteApps);

            return new PagedResponse<RemoteAppListDto>(dtos, pagination.PageNumber, pagination.PageSize, (int)totalCount);
        }

        #region Permission Management

        public async Task<List<RemoteAppPermissionDto>> GetRemoteAppPermissionsAsync(string id, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(objectId, cancellationToken);
            if (remoteApp == null)
                throw new KeyNotFoundException($"Remote app with ID {id} not found");

            var permissions = new List<RemoteAppPermissionDto>();

            // Add user permissions
            foreach (var userPerm in remoteApp.Permissions.Users)
            {
                try
                {
                    if (ObjectId.TryParse(userPerm.UserId, out var userObjectId))
                    {
                        var user = await _unitOfWork.Users.GetByIdAsync(userObjectId, cancellationToken);
                        if (user != null)
                        {
                            permissions.Add(new RemoteAppPermissionDto
                            {
                                Type = "user",
                                Id = userPerm.UserId,
                                Name = user.FullName,
                                AccessLevel = userPerm.AccessLevel
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get user {UserId} for remote app permissions", userPerm.UserId);
                }
            }

            // Add group permissions
            foreach (var groupPerm in remoteApp.Permissions.Groups)
            {
                permissions.Add(new RemoteAppPermissionDto
                {
                    Type = "group",
                    Id = groupPerm.GroupId,
                    Name = $"Group {groupPerm.GroupId}",
                    AccessLevel = groupPerm.AccessLevel
                });
            }

            return permissions;
        }

        public async Task<RemoteAppDto> AddUserPermissionAsync(string id, RemoteAppUserPermissionDto dto, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(objectId, cancellationToken);
            if (remoteApp == null)
                throw new KeyNotFoundException($"Remote app with ID {id} not found");

            // Verify user exists
            if (!ObjectId.TryParse(dto.UserId, out var userObjectId))
                throw new ArgumentException("Invalid user ID format");

            if (!await _unitOfWork.Users.ExistsAsync(userObjectId, cancellationToken))
                throw new KeyNotFoundException($"User with ID {dto.UserId} not found");

            // Check if permission already exists
            var existingPerm = remoteApp.Permissions.Users.FirstOrDefault(up => up.UserId == dto.UserId);
            if (existingPerm != null)
                throw new InvalidOperationException($"User {dto.UserId} already has permissions for this remote app");

            // Add permission
            remoteApp.Permissions.Users.Add(new RemoteAppUserPermission
            {
                UserId = dto.UserId,
                AccessLevel = dto.AccessLevel
            });
            remoteApp.ModifiedAt = DateTime.UtcNow;

            await _unitOfWork.RemoteApps.UpdateAsync(objectId, remoteApp, cancellationToken);

            _logger.LogInformation("Added user permission for user {UserId} on remote app {RemoteAppId} with access level {AccessLevel}",
                dto.UserId, id, dto.AccessLevel);

            return _mapper.Map<RemoteAppDto>(remoteApp);
        }

        public async Task<RemoteAppDto> UpdateUserPermissionAsync(string id, RemoteAppUserPermissionDto dto, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(objectId, cancellationToken);
            if (remoteApp == null)
                throw new KeyNotFoundException($"Remote app with ID {id} not found");

            // Find and update permission
            var existingPerm = remoteApp.Permissions.Users.FirstOrDefault(up => up.UserId == dto.UserId);
            if (existingPerm == null)
                throw new KeyNotFoundException($"No permission found for user {dto.UserId}");

            existingPerm.AccessLevel = dto.AccessLevel;
            remoteApp.ModifiedAt = DateTime.UtcNow;

            await _unitOfWork.RemoteApps.UpdateAsync(objectId, remoteApp, cancellationToken);

            _logger.LogInformation("Updated user permission for user {UserId} on remote app {RemoteAppId} to access level {AccessLevel}",
                dto.UserId, id, dto.AccessLevel);

            return _mapper.Map<RemoteAppDto>(remoteApp);
        }

        public async Task<bool> RemoveUserPermissionAsync(string id, string userId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(objectId, cancellationToken);
            if (remoteApp == null)
                throw new KeyNotFoundException($"Remote app with ID {id} not found");

            // Find and remove permission
            var existingPerm = remoteApp.Permissions.Users.FirstOrDefault(up => up.UserId == userId);
            if (existingPerm == null)
                return false;

            remoteApp.Permissions.Users.Remove(existingPerm);
            remoteApp.ModifiedAt = DateTime.UtcNow;

            await _unitOfWork.RemoteApps.UpdateAsync(objectId, remoteApp, cancellationToken);

            _logger.LogInformation("Removed user permission for user {UserId} from remote app {RemoteAppId}", userId, id);

            return true;
        }

        public async Task<RemoteAppDto> AddGroupPermissionAsync(string id, RemoteAppGroupPermissionDto dto, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(objectId, cancellationToken);
            if (remoteApp == null)
                throw new KeyNotFoundException($"Remote app with ID {id} not found");

            // Check if permission already exists
            var existingPerm = remoteApp.Permissions.Groups.FirstOrDefault(gp => gp.GroupId == dto.GroupId);
            if (existingPerm != null)
                throw new InvalidOperationException($"Group {dto.GroupId} already has permissions for this remote app");

            // Add permission
            remoteApp.Permissions.Groups.Add(new RemoteAppGroupPermission
            {
                GroupId = dto.GroupId,
                AccessLevel = dto.AccessLevel
            });
            remoteApp.ModifiedAt = DateTime.UtcNow;

            await _unitOfWork.RemoteApps.UpdateAsync(objectId, remoteApp, cancellationToken);

            _logger.LogInformation("Added group permission for group {GroupId} on remote app {RemoteAppId} with access level {AccessLevel}",
                dto.GroupId, id, dto.AccessLevel);

            return _mapper.Map<RemoteAppDto>(remoteApp);
        }

        public async Task<RemoteAppDto> UpdateGroupPermissionAsync(string id, RemoteAppGroupPermissionDto dto, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(objectId, cancellationToken);
            if (remoteApp == null)
                throw new KeyNotFoundException($"Remote app with ID {id} not found");

            // Find and update permission
            var existingPerm = remoteApp.Permissions.Groups.FirstOrDefault(gp => gp.GroupId == dto.GroupId);
            if (existingPerm == null)
                throw new KeyNotFoundException($"No permission found for group {dto.GroupId}");

            existingPerm.AccessLevel = dto.AccessLevel;
            remoteApp.ModifiedAt = DateTime.UtcNow;

            await _unitOfWork.RemoteApps.UpdateAsync(objectId, remoteApp, cancellationToken);

            _logger.LogInformation("Updated group permission for group {GroupId} on remote app {RemoteAppId} to access level {AccessLevel}",
                dto.GroupId, id, dto.AccessLevel);

            return _mapper.Map<RemoteAppDto>(remoteApp);
        }

        public async Task<bool> RemoveGroupPermissionAsync(string id, string groupId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(objectId, cancellationToken);
            if (remoteApp == null)
                throw new KeyNotFoundException($"Remote app with ID {id} not found");

            // Find and remove permission
            var existingPerm = remoteApp.Permissions.Groups.FirstOrDefault(gp => gp.GroupId == groupId);
            if (existingPerm == null)
                return false;

            remoteApp.Permissions.Groups.Remove(existingPerm);
            remoteApp.ModifiedAt = DateTime.UtcNow;

            await _unitOfWork.RemoteApps.UpdateAsync(objectId, remoteApp, cancellationToken);

            _logger.LogInformation("Removed group permission for group {GroupId} from remote app {RemoteAppId}", groupId, id);

            return true;
        }

        #endregion

        public async Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(objectId, cancellationToken);
            if (remoteApp == null)
                throw new KeyNotFoundException($"Remote app with ID {id} not found");

            remoteApp.Status = status;
            remoteApp.ModifiedAt = DateTime.UtcNow;

            await _unitOfWork.RemoteApps.UpdateAsync(objectId, remoteApp, cancellationToken);
            return true;
        }

        public async Task<bool> ValidateNameUniqueAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default)
        {
            ObjectId? excludeObjectId = null;
            if (!string.IsNullOrEmpty(excludeId))
            {
                if (!ObjectId.TryParse(excludeId, out var objectId))
                    throw new ArgumentException("Invalid exclude ID format", nameof(excludeId));
                excludeObjectId = objectId;
            }

            return await _unitOfWork.RemoteApps.IsNameUniqueAsync(name, excludeObjectId, cancellationToken);
        }

        public async Task<string> GetLaunchUrlAsync(string id, string userId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            if (!ObjectId.TryParse(userId, out var userObjectId))
                throw new ArgumentException("Invalid user ID format", nameof(userId));

            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(objectId, cancellationToken);
            if (remoteApp == null)
                throw new KeyNotFoundException($"Remote app with ID {id} not found");

            // Check if user has access (public apps, user permissions, or creator)
            bool hasAccess = remoteApp.IsPublic ||
                           remoteApp.Creator == userId ||
                           remoteApp.Permissions.Users.Any(up => up.UserId == userId);

            // Also check group permissions
            if (!hasAccess)
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userObjectId, cancellationToken);
                if (user != null && user.Groups != null && user.Groups.Any())
                {
                    var userGroupIdStrings = user.Groups.Select(g => g.ToString()).ToList();
                    hasAccess = remoteApp.Permissions.Groups.Any(gp => userGroupIdStrings.Contains(gp.GroupId));
                }
            }

            if (!hasAccess)
                throw new UnauthorizedAccessException("User does not have access to this remote app");

            // If SSO credentials are configured, construct SSO URL
            if (!string.IsNullOrEmpty(remoteApp.SsoUrl) && 
                !string.IsNullOrEmpty(remoteApp.DefaultUsername) && 
                !string.IsNullOrEmpty(remoteApp.DefaultPassword))
            {
                return $"{remoteApp.SsoUrl}?username={remoteApp.DefaultUsername}&password={remoteApp.DefaultPassword}";
            }

            // Otherwise return the base URL
            return remoteApp.Url;
        }

    }
}