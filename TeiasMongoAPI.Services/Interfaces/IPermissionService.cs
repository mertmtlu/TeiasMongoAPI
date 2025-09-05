using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IPermissionService
    {
        /// <summary>
        /// Checks if a user has a specific permission for a resource
        /// </summary>
        Task<bool> HasPermissionAsync(string userId, string resourceType, string resourceId, EntityPermissionType permission, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a user has any access to a resource (at least view permission)
        /// </summary>
        Task<bool> HasUserAccessToResourceAsync(string userId, string resourceType, string resourceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the user's access level for a specific resource
        /// </summary>
        Task<EntityAccessLevel> GetUserAccessLevelAsync(string userId, string resourceType, string resourceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all group IDs that a user belongs to
        /// </summary>
        Task<List<ObjectId>> GetUserGroupIdsAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a user is a member of a specific group
        /// </summary>
        Task<bool> IsUserInGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates the group cache for a specific user (call when user's group membership changes)
        /// </summary>
        void InvalidateUserGroupCache(string userId);

        /// <summary>
        /// Invalidates all group caches (call when group structures change significantly)
        /// </summary>
        void InvalidateAllGroupCaches();
    }
}