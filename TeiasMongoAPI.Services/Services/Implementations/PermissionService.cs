using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class PermissionService : IPermissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PermissionService> _logger;
        private readonly Dictionary<string, HashSet<ObjectId>> _userGroupCache = new();
        private readonly Dictionary<string, DateTime> _cacheExpiry = new();
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(15);

        public PermissionService(IUnitOfWork unitOfWork, ILogger<PermissionService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<bool> HasPermissionAsync(string userId, string resourceType, string resourceId, EntityPermissionType permission, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            try
            {
                // Get user and their groups
                if (!ObjectId.TryParse(userId, out var userObjectId))
                    return false;

                var user = await _unitOfWork.Users.GetByIdAsync(userObjectId, cancellationToken);
                if (user == null || !user.IsActive)
                    return false;

                var userGroupIds = await GetUserGroupIdsAsync(userId, cancellationToken);

                // Check permissions based on resource type
                return resourceType.ToLower() switch
                {
                    "program" => await CheckProgramPermissionAsync(user, userGroupIds, resourceId, permission, cancellationToken),
                    "workflow" => await CheckWorkflowPermissionAsync(user, userGroupIds, resourceId, permission, cancellationToken),
                    "remoteapp" => await CheckRemoteAppPermissionAsync(user, userGroupIds, resourceId, permission, cancellationToken),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking permission for user {UserId} on resource {ResourceType}:{ResourceId}", userId, resourceType, resourceId);
                return false;
            }
        }

        public async Task<bool> HasUserAccessToResourceAsync(string userId, string resourceType, string resourceId, CancellationToken cancellationToken = default)
        {
            return await HasPermissionAsync(userId, resourceType, resourceId, EntityPermissionType.View, cancellationToken);
        }

        public async Task<EntityAccessLevel> GetUserAccessLevelAsync(string userId, string resourceType, string resourceId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userId))
                return EntityAccessLevel.None;

            try
            {
                if (!ObjectId.TryParse(userId, out var userObjectId))
                    return EntityAccessLevel.None;

                var user = await _unitOfWork.Users.GetByIdAsync(userObjectId, cancellationToken);
                if (user == null || !user.IsActive)
                    return EntityAccessLevel.None;

                var userGroupIds = await GetUserGroupIdsAsync(userId, cancellationToken);

                return resourceType.ToLower() switch
                {
                    "program" => await GetProgramAccessLevelAsync(user, userGroupIds, resourceId, cancellationToken),
                    "workflow" => await GetWorkflowAccessLevelAsync(user, userGroupIds, resourceId, cancellationToken),
                    "remoteapp" => await GetRemoteAppAccessLevelAsync(user, userGroupIds, resourceId, cancellationToken),
                    _ => EntityAccessLevel.None
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting access level for user {UserId} on resource {ResourceType}:{ResourceId}", userId, resourceType, resourceId);
                return EntityAccessLevel.None;
            }
        }

        public async Task<List<ObjectId>> GetUserGroupIdsAsync(string userId, CancellationToken cancellationToken = default)
        {
            // Check cache first
            if (_userGroupCache.TryGetValue(userId, out var cachedGroups) && 
                _cacheExpiry.TryGetValue(userId, out var expiry) && 
                DateTime.UtcNow < expiry)
            {
                return cachedGroups.ToList();
            }

            if (!ObjectId.TryParse(userId, out var userObjectId))
                return new List<ObjectId>();

            var user = await _unitOfWork.Users.GetByIdAsync(userObjectId, cancellationToken);
            var groupIds = user?.Groups ?? new List<ObjectId>();

            // Update cache
            _userGroupCache[userId] = new HashSet<ObjectId>(groupIds);
            _cacheExpiry[userId] = DateTime.UtcNow.Add(_cacheTimeout);

            return groupIds;
        }

        public async Task<bool> IsUserInGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default)
        {
            var userGroups = await GetUserGroupIdsAsync(userId, cancellationToken);
            return ObjectId.TryParse(groupId, out var groupObjectId) && userGroups.Contains(groupObjectId);
        }

        public void InvalidateUserGroupCache(string userId)
        {
            _userGroupCache.Remove(userId);
            _cacheExpiry.Remove(userId);
        }

        public void InvalidateAllGroupCaches()
        {
            _userGroupCache.Clear();
            _cacheExpiry.Clear();
        }

        private async Task<bool> CheckProgramPermissionAsync(User user, List<ObjectId> userGroupIds, string programId, EntityPermissionType permission, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(programId, out var programObjectId))
                return false;

            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);
            if (program == null)
                return false;

            // Public access for view operations
            if (program.IsPublic && permission == EntityPermissionType.View)
                return true;

            // Creator has full access
            if (program.CreatorId == user._ID.ToString())
                return true;

            // Check direct user permissions
            var userPermission = program.Permissions.Users.FirstOrDefault(up => up.UserId == user._ID.ToString());
            if (userPermission != null && HasRequiredPermission(ConvertAccessLevel(userPermission.AccessLevel), permission))
                return true;

            // Check group permissions
            var userGroupIdStrings = userGroupIds.Select(id => id.ToString()).ToList();
            var groupPermission = program.Permissions.Groups
                .FirstOrDefault(gp => userGroupIdStrings.Contains(gp.GroupId));
            
            return groupPermission != null && HasRequiredPermission(ConvertAccessLevel(groupPermission.AccessLevel), permission);
        }

        private async Task<bool> CheckWorkflowPermissionAsync(User user, List<ObjectId> userGroupIds, string workflowId, EntityPermissionType permission, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(workflowId, out var workflowObjectId))
                return false;

            var workflow = await _unitOfWork.Workflows.GetByIdAsync(workflowObjectId, cancellationToken);
            if (workflow == null)
                return false;

            // Public access for view operations
            if (workflow.Permissions.IsPublic && permission == EntityPermissionType.View)
                return true;

            // Creator has full access
            if (workflow.Creator == user._ID.ToString())
                return true;

            // Check workflow-specific permission logic (simplified for now)
            // In a full implementation, you'd check workflow.Permissions structure
            return workflow.Permissions.AllowedUsers.Contains(user._ID.ToString());
        }

        private async Task<bool> CheckRemoteAppPermissionAsync(User user, List<ObjectId> userGroupIds, string remoteAppId, EntityPermissionType permission, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(remoteAppId, out var remoteAppObjectId))
                return false;

            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(remoteAppObjectId, cancellationToken);
            if (remoteApp == null)
                return false;

            // Public access for view operations
            if (remoteApp.IsPublic && permission == EntityPermissionType.View)
                return true;

            // Creator has full access
            if (remoteApp.Creator == user._ID.ToString())
                return true;

            // Check if user has direct permission
            if (remoteApp.Permissions.Users.Any(up => up.UserId == user._ID.ToString()))
                return true;

            // Check if user has group permission
            var userGroupIdStrings = userGroupIds.Select(g => g.ToString()).ToList();
            return remoteApp.Permissions.Groups.Any(gp => userGroupIdStrings.Contains(gp.GroupId));
        }

        private async Task<EntityAccessLevel> GetProgramAccessLevelAsync(User user, List<ObjectId> userGroupIds, string programId, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(programId, out var programObjectId))
                return EntityAccessLevel.None;

            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);
            if (program == null)
                return EntityAccessLevel.None;

            // Creator has full access
            if (program.CreatorId == user._ID.ToString())
                return EntityAccessLevel.Full;

            // Check direct user permissions
            var userPermission = program.Permissions.Users.FirstOrDefault(up => up.UserId == user._ID.ToString());
            if (userPermission != null)
                return ConvertAccessLevel(userPermission.AccessLevel);

            // Check group permissions
            var userGroupIdStrings = userGroupIds.Select(id => id.ToString()).ToList();
            var groupPermission = program.Permissions.Groups
                .FirstOrDefault(gp => userGroupIdStrings.Contains(gp.GroupId));
            
            if (groupPermission != null)
                return ConvertAccessLevel(groupPermission.AccessLevel);

            // Public access gives read-only
            if (program.IsPublic)
                return EntityAccessLevel.Read;

            return EntityAccessLevel.None;
        }

        private async Task<EntityAccessLevel> GetWorkflowAccessLevelAsync(User user, List<ObjectId> userGroupIds, string workflowId, CancellationToken cancellationToken)
        {
            // Simplified implementation - would need to be expanded based on workflow permission model
            if (!ObjectId.TryParse(workflowId, out var workflowObjectId))
                return EntityAccessLevel.None;

            var workflow = await _unitOfWork.Workflows.GetByIdAsync(workflowObjectId, cancellationToken);
            if (workflow == null)
                return EntityAccessLevel.None;

            if (workflow.Creator == user._ID.ToString())
                return EntityAccessLevel.Full;

            if (workflow.Permissions.AllowedUsers.Contains(user._ID.ToString()))
                return EntityAccessLevel.Write;

            if (workflow.Permissions.IsPublic)
                return EntityAccessLevel.Read;

            return EntityAccessLevel.None;
        }

        private async Task<EntityAccessLevel> GetRemoteAppAccessLevelAsync(User user, List<ObjectId> userGroupIds, string remoteAppId, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(remoteAppId, out var remoteAppObjectId))
                return EntityAccessLevel.None;

            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(remoteAppObjectId, cancellationToken);
            if (remoteApp == null)
                return EntityAccessLevel.None;

            if (remoteApp.Creator == user._ID.ToString())
                return EntityAccessLevel.Full;

            // Check user permissions
            var userPerm = remoteApp.Permissions.Users.FirstOrDefault(up => up.UserId == user._ID.ToString());
            if (userPerm != null)
            {
                return userPerm.AccessLevel.ToLower() switch
                {
                    "admin" => EntityAccessLevel.Full,
                    "write" => EntityAccessLevel.Write,
                    "read" => EntityAccessLevel.Read,
                    _ => EntityAccessLevel.None
                };
            }

            // Check group permissions
            var userGroupIdStrings = userGroupIds.Select(g => g.ToString()).ToList();
            var groupPerm = remoteApp.Permissions.Groups
                .Where(gp => userGroupIdStrings.Contains(gp.GroupId))
                .OrderByDescending(gp => gp.AccessLevel == "admin" ? 3 : gp.AccessLevel == "write" ? 2 : 1)
                .FirstOrDefault();

            if (groupPerm != null)
            {
                return groupPerm.AccessLevel.ToLower() switch
                {
                    "admin" => EntityAccessLevel.Full,
                    "write" => EntityAccessLevel.Write,
                    "read" => EntityAccessLevel.Read,
                    _ => EntityAccessLevel.None
                };
            }

            if (remoteApp.IsPublic)
                return EntityAccessLevel.Read;

            return EntityAccessLevel.None;
        }

        private static bool HasRequiredPermission(EntityAccessLevel userLevel, EntityPermissionType requiredPermission)
        {
            return requiredPermission switch
            {
                EntityPermissionType.View => userLevel >= EntityAccessLevel.Read,
                EntityPermissionType.Edit => userLevel >= EntityAccessLevel.Write,
                EntityPermissionType.Delete => userLevel >= EntityAccessLevel.Admin,
                EntityPermissionType.Execute => userLevel >= EntityAccessLevel.Execute,
                EntityPermissionType.Share => userLevel >= EntityAccessLevel.Admin,
                EntityPermissionType.ManagePermissions => userLevel >= EntityAccessLevel.Full,
                _ => false
            };
        }

        private static EntityAccessLevel ConvertAccessLevel(string accessLevel)
        {
            return accessLevel?.ToLower() switch
            {
                "read" => EntityAccessLevel.Read,
                "write" => EntityAccessLevel.Write,
                "execute" => EntityAccessLevel.Execute,
                "admin" => EntityAccessLevel.Admin,
                "full" => EntityAccessLevel.Full,
                _ => EntityAccessLevel.None
            };
        }
    }
}