using AutoMapper;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Auth;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Search;
using TeiasMongoAPI.Services.DTOs.Request.User;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.User;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;
using TeiasMongoAPI.Services.Security;
using TeiasMongoAPI.Services.Specifications;
using Microsoft.Extensions.Logging;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class UserService : BaseService, IUserService
    {
        private readonly IPasswordHashingService _passwordHashingService;

        public UserService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IPasswordHashingService passwordHashingService,
            ILogger<UserService> logger)
            : base(unitOfWork, mapper, logger)
        {
            _passwordHashingService = passwordHashingService;
        }

        public async Task<bool> RevokeAllTokensAsync(string userId, string revokedByIp, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(userId);
            var user = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {userId} not found.");
            }

            _logger.LogInformation("Revoking all tokens for user {UserId} from IP {IP}", userId, revokedByIp);

            return await _unitOfWork.Users.RevokeAllUserRefreshTokensAsync(objectId, revokedByIp, cancellationToken);
        }

        public async Task<UserDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var user = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {id} not found.");
            }

            var dto = _mapper.Map<UserDetailDto>(user);

            // Get assigned clients
            var assignedClients = new List<DTOs.Response.Client.ClientSummaryResponseDto>();
            foreach (var clientId in user.AssignedClients)
            {
                var client = await _unitOfWork.Clients.GetByIdAsync(clientId, cancellationToken);
                if (client != null)
                {
                    assignedClients.Add(_mapper.Map<DTOs.Response.Client.ClientSummaryResponseDto>(client));
                }
            }
            dto.AssignedClients = assignedClients;

            return dto;
        }

        public async Task<PagedResponse<UserListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            // Use Specification Pattern for database-level pagination
            var spec = new AllUsersSpecification(pagination);
            var (users, totalCount) = await _unitOfWork.Users.FindWithSpecificationAsync(spec, cancellationToken);

            var dtos = _mapper.Map<List<UserListDto>>(users);

            return new PagedResponse<UserListDto>(dtos, pagination.PageNumber, pagination.PageSize, (int)totalCount);
        }

        public async Task<PagedResponse<UserListDto>> SearchAsync(UserSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            // Use Specification Pattern for database-level pagination and filtering
            var spec = new UserSearchSpecification(searchDto, pagination);
            var (users, totalCount) = await _unitOfWork.Users.FindWithSpecificationAsync(spec, cancellationToken);

            var dtos = _mapper.Map<List<UserListDto>>(users);

            return new PagedResponse<UserListDto>(dtos, pagination.PageNumber, pagination.PageSize, (int)totalCount);
        }

        public async Task<UserDto> CreateAsync(UserRegisterDto dto, CancellationToken cancellationToken = default)
        {
            // Validate password complexity
            if (!_passwordHashingService.IsPasswordComplex(dto.Password))
            {
                throw new InvalidOperationException(PasswordRequirements.GetPasswordPolicy());
            }

            // Check if user with same email exists
            if (await _unitOfWork.Users.EmailExistsAsync(dto.Email, cancellationToken))
            {
                throw new InvalidOperationException($"User with email '{dto.Email}' already exists.");
            }

            // Check if user with same username exists
            if (await _unitOfWork.Users.UsernameExistsAsync(dto.Username, cancellationToken))
            {
                throw new InvalidOperationException($"User with username '{dto.Username}' already exists.");
            }

            var user = _mapper.Map<User>(dto);
            user.PasswordHash = _passwordHashingService.HashPassword(dto.Password);
            // Removed: EmailVerificationToken generation

            // Ensure permissions are populated based on roles
            user.Permissions = RolePermissions.GetUserPermissions(user);

            var createdUser = await _unitOfWork.Users.CreateAsync(user, cancellationToken);

            return _mapper.Map<UserDto>(createdUser);
        }

        public async Task<UserDto> UpdateAsync(string id, UserUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var existingUser = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);

            if (existingUser == null)
            {
                throw new KeyNotFoundException($"User with ID {id} not found.");
            }

            // If updating email, check uniqueness
            if (!string.IsNullOrEmpty(dto.Email) && dto.Email != existingUser.Email)
            {
                if (await _unitOfWork.Users.EmailExistsAsync(dto.Email, cancellationToken))
                {
                    throw new InvalidOperationException($"User with email '{dto.Email}' already exists.");
                }
            }

            _mapper.Map(dto, existingUser);
            existingUser.ModifiedDate = DateTime.UtcNow;

            var success = await _unitOfWork.Users.UpdateAsync(objectId, existingUser, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update user with ID {id}.");
            }

            return _mapper.Map<UserDto>(existingUser);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var user = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {id} not found.");
            }

            // Don't allow deletion of last admin
            if (user.Roles.Contains(UserRoles.Admin))
            {
                var adminUsers = await _unitOfWork.Users.GetByRoleAsync(UserRoles.Admin, cancellationToken);
                if (adminUsers.Count() <= 1)
                {
                    throw new InvalidOperationException("Cannot delete the last admin user.");
                }
            }

            return await _unitOfWork.Users.DeleteAsync(objectId, cancellationToken);
        }

        public async Task<UserDto> UpdateRolesAsync(string id, UserRoleUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var user = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {id} not found.");
            }

            // Don't allow removing admin role from last admin
            if (user.Roles.Contains(UserRoles.Admin) && !dto.Roles.Contains(UserRoles.Admin))
            {
                var adminUsers = await _unitOfWork.Users.GetByRoleAsync(UserRoles.Admin, cancellationToken);
                if (adminUsers.Count() <= 1)
                {
                    throw new InvalidOperationException("Cannot remove admin role from the last admin user.");
                }
            }

            user.Roles = dto.Roles;
            // Update permissions based on new roles
            user.Permissions = RolePermissions.GetUserPermissions(user);
            user.ModifiedDate = DateTime.UtcNow;

            var success = await _unitOfWork.Users.UpdateAsync(objectId, user, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update roles for user with ID {id}.");
            }

            return _mapper.Map<UserDto>(user);
        }

        public async Task<UserDto> UpdatePermissionsAsync(string id, UserPermissionUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var user = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {id} not found.");
            }

            user.Permissions = dto.Permissions;
            user.ModifiedDate = DateTime.UtcNow;

            var success = await _unitOfWork.Users.UpdateAsync(objectId, user, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update permissions for user with ID {id}.");
            }

            return _mapper.Map<UserDto>(user);
        }

        // New method to assign clients
        public async Task<UserDto> AssignClientsAsync(string id, UserClientAssignmentDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var user = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {id} not found.");
            }

            // Validate all clients exist
            var clientObjectIds = new List<MongoDB.Bson.ObjectId>();
            foreach (var clientId in dto.ClientIds)
            {
                var clientObjectId = ParseObjectId(clientId);
                var client = await _unitOfWork.Clients.GetByIdAsync(clientObjectId, cancellationToken);
                if (client == null)
                {
                    throw new InvalidOperationException($"Client with ID {clientId} not found.");
                }
                clientObjectIds.Add(clientObjectId);
            }

            user.AssignedClients = clientObjectIds;
            user.ModifiedDate = DateTime.UtcNow;

            var success = await _unitOfWork.Users.UpdateAsync(objectId, user, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to assign clients to user with ID {id}.");
            }

            return _mapper.Map<UserDto>(user);
        }

        public async Task<UserDto> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(email, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with email '{email}' not found.");
            }

            return _mapper.Map<UserDto>(user);
        }

        public async Task<UserDto> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            var user = await _unitOfWork.Users.GetByUsernameAsync(username, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with username '{username}' not found.");
            }

            return _mapper.Map<UserDto>(user);
        }

        public async Task<bool> ActivateAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var user = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {id} not found.");
            }

            user.IsActive = true;
            user.ModifiedDate = DateTime.UtcNow;

            return await _unitOfWork.Users.UpdateAsync(objectId, user, cancellationToken);
        }

        public async Task<bool> DeactivateAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var user = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {id} not found.");
            }

            // Don't allow deactivating the last admin
            if (user.Roles.Contains(UserRoles.Admin))
            {
                var activeAdmins = await _unitOfWork.Users.GetByRoleAsync(UserRoles.Admin, cancellationToken);
                activeAdmins = activeAdmins.Where(u => u.IsActive);
                if (activeAdmins.Count() <= 1)
                {
                    throw new InvalidOperationException("Cannot deactivate the last active admin user.");
                }
            }

            user.IsActive = false;
            user.ModifiedDate = DateTime.UtcNow;

            return await _unitOfWork.Users.UpdateAsync(objectId, user, cancellationToken);
        }

        public async Task<UserProfileDto> GetProfileAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var user = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {id} not found.");
            }

            return _mapper.Map<UserProfileDto>(user);
        }

        public async Task<List<string>> GetEffectivePermissionsAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var user = await _unitOfWork.Users.GetByIdAsync(objectId, cancellationToken);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {id} not found.");
            }

            // Get all permissions for the user (role-based + direct permissions)
            return RolePermissions.GetUserPermissions(user);
        }
    }
}