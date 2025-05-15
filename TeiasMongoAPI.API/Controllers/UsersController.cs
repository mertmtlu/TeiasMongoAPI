using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.Auth;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Request.Search;
using TeiasMongoAPI.Services.DTOs.Request.User;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.DTOs.Response.User;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : BaseController
    {
        private readonly IUserService _userService;

        public UsersController(
            IUserService userService,
            ILogger<UsersController> logger)
            : base(logger)
        {
            _userService = userService;
        }

        #region Basic CRUD Operations

        /// <summary>
        /// Get all users with pagination
        /// </summary>
        [HttpGet]
        [RequirePermission(UserPermissions.ViewUsers)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserListDto>>>> GetAll(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _userService.GetAllAsync(pagination, cancellationToken);
            }, "Get all users");
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        [HttpGet("{id}")]
        [RequirePermission(UserPermissions.ViewUsers)]
        public async Task<ActionResult<ApiResponse<UserDetailDto>>> GetById(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _userService.GetByIdAsync(id, cancellationToken);
            }, $"Get user {id}");
        }

        /// <summary>
        /// Create new user (admin only)
        /// </summary>
        [HttpPost]
        [RequirePermission(UserPermissions.CreateUsers)]
        [AuditLog("CreateUser")]
        public async Task<ActionResult<ApiResponse<UserDto>>> Create(
            [FromBody] UserRegisterDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<UserDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _userService.CreateAsync(dto, cancellationToken);
            }, "Create user");
        }

        /// <summary>
        /// Update user details
        /// </summary>
        [HttpPut("{id}")]
        [RequirePermission(UserPermissions.UpdateUsers)]
        [AuditLog("UpdateUser")]
        public async Task<ActionResult<ApiResponse<UserDto>>> Update(
            string id,
            [FromBody] UserUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<UserDto>();
            if (validationResult != null) return validationResult;

            // Check if user is updating their own profile or is admin
            if (CurrentUserId?.ToString() != id && !IsInRole(UserRoles.Admin))
            {
                return Forbidden<UserDto>("You can only update your own profile");
            }

            return await ExecuteAsync(async () =>
            {
                return await _userService.UpdateAsync(id, dto, cancellationToken);
            }, $"Update user {id}");
        }

        /// <summary>
        /// Delete user
        /// </summary>
        [HttpDelete("{id}")]
        [RequirePermission(UserPermissions.DeleteUsers)]
        [AuditLog("DeleteUser")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _userService.DeleteAsync(id, cancellationToken);
            }, $"Delete user {id}");
        }

        #endregion

        #region Profile Management

        /// <summary>
        /// Get current user profile
        /// </summary>
        [HttpGet("me")]
        public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetCurrentUserProfile(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                var userId = CurrentUserId?.ToString();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                return await _userService.GetProfileAsync(userId, cancellationToken);
            }, "Get current user profile");
        }

        /// <summary>
        /// Update current user profile
        /// </summary>
        [HttpPut("me")]
        [AuditLog("UpdateOwnProfile")]
        public async Task<ActionResult<ApiResponse<UserDto>>> UpdateCurrentUserProfile(
            [FromBody] UserUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<UserDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                var userId = CurrentUserId?.ToString();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                return await _userService.UpdateAsync(userId, dto, cancellationToken);
            }, "Update current user profile");
        }

        /// <summary>
        /// Get current user permissions
        /// </summary>
        [HttpGet("me/permissions")]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetCurrentUserPermissions(
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync<List<string>>(async () =>
            {
                return await Task.FromResult(CurrentUserPermissions.ToList());
            }, "Get current user permissions");
        }

        #endregion

        #region Search & Filters

        /// <summary>
        /// Advanced user search
        /// </summary>
        [HttpPost("search")]
        [RequirePermission(UserPermissions.ViewUsers)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserListDto>>>> Search(
            [FromBody] UserSearchDto searchDto,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _userService.SearchAsync(searchDto, pagination, cancellationToken);
            }, "Search users");
        }

        /// <summary>
        /// Get users by role
        /// </summary>
        [HttpGet("by-role/{role}")]
        [RequirePermission(UserPermissions.ViewUsers)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserListDto>>>> GetByRole(
            string role,
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var searchDto = new UserSearchDto { Roles = new List<string> { role } };

            return await ExecuteAsync(async () =>
            {
                return await _userService.SearchAsync(searchDto, pagination, cancellationToken);
            }, $"Get users by role {role}");
        }

        /// <summary>
        /// Get active users only
        /// </summary>
        [HttpGet("active")]
        [RequirePermission(UserPermissions.ViewUsers)]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserListDto>>>> GetActiveUsers(
            [FromQuery] PaginationRequestDto pagination,
            CancellationToken cancellationToken = default)
        {
            var searchDto = new UserSearchDto { IsActive = true };

            return await ExecuteAsync(async () =>
            {
                return await _userService.SearchAsync(searchDto, pagination, cancellationToken);
            }, "Get active users");
        }

        #endregion

        #region Role & Permission Management

        /// <summary>
        /// Update user roles
        /// </summary>
        [HttpPut("{id}/roles")]
        [RequirePermission(UserPermissions.ManageRoles)]
        [AuditLog("UpdateUserRoles")]
        public async Task<ActionResult<ApiResponse<UserDto>>> UpdateRoles(
            string id,
            [FromBody] UserRoleUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<UserDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _userService.UpdateRolesAsync(id, dto, cancellationToken);
            }, $"Update roles for user {id}");
        }

        /// <summary>
        /// Update user permissions
        /// </summary>
        [HttpPut("{id}/permissions")]
        [RequirePermission(UserPermissions.ManageRoles)]
        [AuditLog("UpdateUserPermissions")]
        public async Task<ActionResult<ApiResponse<UserDto>>> UpdatePermissions(
            string id,
            [FromBody] UserPermissionUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<UserDto>();
            if (validationResult != null) return validationResult;

            return await ExecuteAsync(async () =>
            {
                return await _userService.UpdatePermissionsAsync(id, dto, cancellationToken);
            }, $"Update permissions for user {id}");
        }

        /// <summary>
        /// Get user's effective permissions (role + direct)
        /// </summary>
        [HttpGet("{id}/effective-permissions")]
        [RequirePermission(UserPermissions.ViewUsers)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetEffectivePermissions(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _userService.GetEffectivePermissionsAsync(id, cancellationToken);
            }, $"Get effective permissions for user {id}");
        }

        #endregion

        #region Client Assignment Management

        /// <summary>
        /// Assign clients to user
        /// </summary>
        [HttpPut("{id}/clients")]
        [RequirePermission(UserPermissions.UpdateUsers)]
        [AuditLog("AssignUserClients")]
        public async Task<ActionResult<ApiResponse<UserDto>>> AssignClients(
            string id,
            [FromBody] UserClientAssignmentDto dto,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            // Validate model state
            var validationResult = ValidateModelState<UserDto>();
            if (validationResult != null) return validationResult;

            // Update dto.UserId to match the id parameter
            dto.UserId = id;

            return await ExecuteAsync(async () =>
            {
                return await _userService.AssignClientsAsync(id, dto, cancellationToken);
            }, $"Assign clients to user {id}");
        }

        #endregion

        #region Account Management

        /// <summary>
        /// Activate user account
        /// </summary>
        [HttpPut("{id}/activate")]
        [RequirePermission(UserPermissions.UpdateUsers)]
        [AuditLog("ActivateUser")]
        public async Task<ActionResult<ApiResponse<bool>>> Activate(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _userService.ActivateAsync(id, cancellationToken);
            }, $"Activate user {id}");
        }

        /// <summary>
        /// Deactivate user account
        /// </summary>
        [HttpPut("{id}/deactivate")]
        [RequirePermission(UserPermissions.UpdateUsers)]
        [AuditLog("DeactivateUser")]
        public async Task<ActionResult<ApiResponse<bool>>> Deactivate(
            string id,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(id);
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _userService.DeactivateAsync(id, cancellationToken);
            }, $"Deactivate user {id}");
        }

        // Removed: ResendVerificationEmail - no longer needed

        /// <summary>
        /// Revoke all refresh tokens for user
        /// </summary>
        [HttpPost("{id}/revoke-tokens")]
        [RequirePermission(UserPermissions.UpdateUsers)]
        [AuditLog("RevokeUserTokens")]
        public async Task<ActionResult<ApiResponse<bool>>> RevokeAllTokens(
            string id,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync<bool>(async () =>
            {
                var ipAddress = GetClientIpAddress() ?? "Unknown";
                return await _userService.RevokeAllTokensAsync(id, ipAddress, cancellationToken);
            }, $"Revoke all tokens for user {id}");
        }

        #endregion

        #region Additional Endpoints

        /// <summary>
        /// Get user by email
        /// </summary>
        [HttpGet("by-email/{email}")]
        [RequirePermission(UserPermissions.ViewUsers)]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetByEmail(
            string email,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _userService.GetByEmailAsync(email, cancellationToken);
            }, $"Get user by email {email}");
        }

        /// <summary>
        /// Get user by username
        /// </summary>
        [HttpGet("by-username/{username}")]
        [RequirePermission(UserPermissions.ViewUsers)]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetByUsername(
            string username,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async () =>
            {
                return await _userService.GetByUsernameAsync(username, cancellationToken);
            }, $"Get user by username {username}");
        }

        /// <summary>
        /// Get available roles
        /// </summary>
        [HttpGet("available-roles")]
        [RequirePermission(UserPermissions.ViewUsers)]
        public ActionResult<ApiResponse<List<string>>> GetAvailableRoles()
        {
            var roles = new List<string>
            {
                UserRoles.Admin,
                UserRoles.Manager,
                UserRoles.Engineer,
                UserRoles.Viewer,
                UserRoles.Auditor
            };

            return Success(roles, "Available roles retrieved");
        }

        /// <summary>
        /// Get available permissions
        /// </summary>
        [HttpGet("available-permissions")]
        [RequirePermission(UserPermissions.ViewUsers)]
        public ActionResult<ApiResponse<Dictionary<string, List<string>>>> GetAvailablePermissions()
        {
            // Group permissions by category
            var permissions = new Dictionary<string, List<string>>
            {
                ["Clients"] = new List<string>
                {
                    UserPermissions.ViewClients,
                    UserPermissions.CreateClients,
                    UserPermissions.UpdateClients,
                    UserPermissions.DeleteClients
                },
                ["Regions"] = new List<string>
                {
                    UserPermissions.ViewRegions,
                    UserPermissions.CreateRegions,
                    UserPermissions.UpdateRegions,
                    UserPermissions.DeleteRegions
                },
                ["TMs"] = new List<string>
                {
                    UserPermissions.ViewTMs,
                    UserPermissions.CreateTMs,
                    UserPermissions.UpdateTMs,
                    UserPermissions.DeleteTMs
                },
                ["Buildings"] = new List<string>
                {
                    UserPermissions.ViewBuildings,
                    UserPermissions.CreateBuildings,
                    UserPermissions.UpdateBuildings,
                    UserPermissions.DeleteBuildings
                },
                ["Blocks"] = new List<string>
                {
                    UserPermissions.ViewBlocks,
                    UserPermissions.CreateBlocks,
                    UserPermissions.UpdateBlocks,
                    UserPermissions.DeleteBlocks
                },
                ["AlternativeTMs"] = new List<string>
                {
                    UserPermissions.ViewAlternativeTMs,
                    UserPermissions.CreateAlternativeTMs,
                    UserPermissions.UpdateAlternativeTMs,
                    UserPermissions.DeleteAlternativeTMs
                },
                ["Users"] = new List<string>
                {
                    UserPermissions.ViewUsers,
                    UserPermissions.CreateUsers,
                    UserPermissions.UpdateUsers,
                    UserPermissions.DeleteUsers,
                    UserPermissions.ManageRoles
                },
                ["Reports"] = new List<string>
                {
                    UserPermissions.ViewReports,
                    UserPermissions.GenerateReports,
                    UserPermissions.ExportReports,
                    UserPermissions.ViewAuditLogs
                }
            };

            return Success(permissions, "Available permissions retrieved");
        }

        #endregion
    }
}