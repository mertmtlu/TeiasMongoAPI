using MongoDB.Bson.Serialization.Attributes;

namespace TeiasMongoAPI.Core.Models.Common
{
    public class UnifiedPermissions : BasePermissions
    {
        public bool HasUserPermission(string userId, EntityPermissionType permission)
        {
            var userPermission = Users.FirstOrDefault(u => u.UserId == userId);
            return userPermission?.Permissions.Contains(permission) == true;
        }

        public bool HasGroupPermission(string groupId, EntityPermissionType permission)
        {
            var groupPermission = Groups.FirstOrDefault(g => g.GroupId == groupId);
            return groupPermission?.Permissions.Contains(permission) == true;
        }

        public bool HasRolePermission(string roleName, EntityPermissionType permission)
        {
            var rolePermission = Roles.FirstOrDefault(r => r.RoleName == roleName);
            return rolePermission?.Permissions.Contains(permission) == true;
        }

        public EntityAccessLevel GetUserAccessLevel(string userId)
        {
            var userPermission = Users.FirstOrDefault(u => u.UserId == userId);
            return userPermission?.AccessLevel ?? EntityAccessLevel.None;
        }

        public EntityAccessLevel GetGroupAccessLevel(string groupId)
        {
            var groupPermission = Groups.FirstOrDefault(g => g.GroupId == groupId);
            return groupPermission?.AccessLevel ?? EntityAccessLevel.None;
        }

        public EntityAccessLevel GetRoleAccessLevel(string roleName)
        {
            var rolePermission = Roles.FirstOrDefault(r => r.RoleName == roleName);
            return rolePermission?.AccessLevel ?? EntityAccessLevel.None;
        }

        public void AddUserPermission(string userId, EntityAccessLevel accessLevel, List<EntityPermissionType> permissions, string grantedBy)
        {
            var existingPermission = Users.FirstOrDefault(u => u.UserId == userId);
            if (existingPermission != null)
            {
                existingPermission.AccessLevel = accessLevel;
                existingPermission.Permissions = permissions;
                existingPermission.GrantedAt = DateTime.UtcNow;
                existingPermission.GrantedBy = grantedBy;
            }
            else
            {
                Users.Add(new EntityUserPermission
                {
                    UserId = userId,
                    AccessLevel = accessLevel,
                    Permissions = permissions,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = grantedBy
                });
            }
        }

        public void AddGroupPermission(string groupId, EntityAccessLevel accessLevel, List<EntityPermissionType> permissions, string grantedBy)
        {
            var existingPermission = Groups.FirstOrDefault(g => g.GroupId == groupId);
            if (existingPermission != null)
            {
                existingPermission.AccessLevel = accessLevel;
                existingPermission.Permissions = permissions;
                existingPermission.GrantedAt = DateTime.UtcNow;
                existingPermission.GrantedBy = grantedBy;
            }
            else
            {
                Groups.Add(new EntityGroupPermission
                {
                    GroupId = groupId,
                    AccessLevel = accessLevel,
                    Permissions = permissions,
                    GrantedAt = DateTime.UtcNow,
                    GrantedBy = grantedBy
                });
            }
        }

        public void RemoveUserPermission(string userId)
        {
            Users.RemoveAll(u => u.UserId == userId);
        }

        public void RemoveGroupPermission(string groupId)
        {
            Groups.RemoveAll(g => g.GroupId == groupId);
        }
    }
}