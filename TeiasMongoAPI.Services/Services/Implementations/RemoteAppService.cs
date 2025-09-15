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

            // Populate assigned user details
            if (remoteApp.AssignedUsers.Any())
            {
                var users = await _unitOfWork.Users.GetByIdsAsync(remoteApp.AssignedUsers, cancellationToken);
                dto.AssignedUsers = users.Select(u => new RemoteAppAssignedUserDto
                {
                    UserId = u._ID.ToString(),
                    Username = u.Username,
                    FullName = u.FullName,
                    Email = u.Email
                }).ToList();
            }

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

            // Convert assigned user IDs to ObjectIds
            if (dto.AssignedUserIds.Any())
            {
                var userObjectIds = new List<ObjectId>();
                foreach (var userId in dto.AssignedUserIds)
                {
                    if (ObjectId.TryParse(userId, out var userObjectId))
                    {
                        // Verify user exists
                        var user = await _unitOfWork.Users.GetByIdAsync(userObjectId, cancellationToken);
                        if (user != null)
                        {
                            userObjectIds.Add(userObjectId);
                        }
                    }
                }
                remoteApp.AssignedUsers = userObjectIds;
            }

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

            // Update assigned users if provided
            if (dto.AssignedUserIds != null)
            {
                var userObjectIds = new List<ObjectId>();
                foreach (var userId in dto.AssignedUserIds)
                {
                    if (ObjectId.TryParse(userId, out var userObjectId))
                    {
                        // Verify user exists
                        var user = await _unitOfWork.Users.GetByIdAsync(userObjectId, cancellationToken);
                        if (user != null)
                        {
                            userObjectIds.Add(userObjectId);
                        }
                    }
                }
                existingRemoteApp.AssignedUsers = userObjectIds;
            }

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

            var spec = new RemoteAppsUserAccessibleSpecification(userObjectId, pagination);
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

        public async Task<bool> AssignUserAsync(string remoteAppId, string userId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(remoteAppId, out var remoteAppObjectId))
                throw new ArgumentException("Invalid remote app ID format", nameof(remoteAppId));

            if (!ObjectId.TryParse(userId, out var userObjectId))
                throw new ArgumentException("Invalid user ID format", nameof(userId));

            // Verify user exists
            var user = await _unitOfWork.Users.GetByIdAsync(userObjectId, cancellationToken);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found");

            return await _unitOfWork.RemoteApps.AddUserAssignmentAsync(remoteAppObjectId, userObjectId, cancellationToken);
        }

        public async Task<bool> UnassignUserAsync(string remoteAppId, string userId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(remoteAppId, out var remoteAppObjectId))
                throw new ArgumentException("Invalid remote app ID format", nameof(remoteAppId));

            if (!ObjectId.TryParse(userId, out var userObjectId))
                throw new ArgumentException("Invalid user ID format", nameof(userId));

            return await _unitOfWork.RemoteApps.RemoveUserAssignmentAsync(remoteAppObjectId, userObjectId, cancellationToken);
        }

        public async Task<bool> IsUserAssignedAsync(string remoteAppId, string userId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(remoteAppId, out var remoteAppObjectId))
                throw new ArgumentException("Invalid remote app ID format", nameof(remoteAppId));

            if (!ObjectId.TryParse(userId, out var userObjectId))
                throw new ArgumentException("Invalid user ID format", nameof(userId));

            return await _unitOfWork.RemoteApps.IsUserAssignedAsync(remoteAppObjectId, userObjectId, cancellationToken);
        }

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

            // Check if user has access (public apps or assigned users)
            if (!remoteApp.IsPublic && !remoteApp.AssignedUsers.Contains(userObjectId))
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