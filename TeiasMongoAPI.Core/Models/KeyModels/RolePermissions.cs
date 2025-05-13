using System.Collections.Generic;

namespace TeiasMongoAPI.Core.Models.KeyModels
{
    public static class RolePermissions
    {
        public static Dictionary<string, List<string>> RolePermissionMap = new()
        {
            [UserRoles.Admin] = new List<string>
            {
                // Client permissions
                UserPermissions.ViewClients,
                UserPermissions.CreateClients,
                UserPermissions.UpdateClients,
                UserPermissions.DeleteClients,
                
                // Region permissions
                UserPermissions.ViewRegions,
                UserPermissions.CreateRegions,
                UserPermissions.UpdateRegions,
                UserPermissions.DeleteRegions,
                
                // TM permissions
                UserPermissions.ViewTMs,
                UserPermissions.CreateTMs,
                UserPermissions.UpdateTMs,
                UserPermissions.DeleteTMs,
                
                // Building permissions
                UserPermissions.ViewBuildings,
                UserPermissions.CreateBuildings,
                UserPermissions.UpdateBuildings,
                UserPermissions.DeleteBuildings,
                
                // User management permissions
                UserPermissions.ViewUsers,
                UserPermissions.CreateUsers,
                UserPermissions.UpdateUsers,
                UserPermissions.DeleteUsers,
                UserPermissions.ManageRoles,
                
                // Reports permissions
                UserPermissions.ViewReports,
                UserPermissions.GenerateReports,
                UserPermissions.ExportReports
            },

            [UserRoles.Manager] = new List<string>
            {
                // Full access to business entities
                UserPermissions.ViewClients,
                UserPermissions.CreateClients,
                UserPermissions.UpdateClients,
                UserPermissions.DeleteClients,

                UserPermissions.ViewRegions,
                UserPermissions.CreateRegions,
                UserPermissions.UpdateRegions,
                UserPermissions.DeleteRegions,

                UserPermissions.ViewTMs,
                UserPermissions.CreateTMs,
                UserPermissions.UpdateTMs,
                UserPermissions.DeleteTMs,
                
                // View-only access to technical entities
                UserPermissions.ViewBuildings,
                
                // View-only access to users
                UserPermissions.ViewUsers,
                
                // Reports permissions
                UserPermissions.ViewReports,
                UserPermissions.GenerateReports,
                UserPermissions.ExportReports
            },

            [UserRoles.Engineer] = new List<string>
            {
                // View-only access to business entities
                UserPermissions.ViewClients,
                UserPermissions.ViewRegions,
                UserPermissions.ViewTMs,
                
                // Full access to technical entities
                UserPermissions.ViewBuildings,
                UserPermissions.CreateBuildings,
                UserPermissions.UpdateBuildings,
                UserPermissions.DeleteBuildings,
                
                // View-only access to users
                UserPermissions.ViewUsers,
                
                // Reports permissions
                UserPermissions.ViewReports,
                UserPermissions.GenerateReports,
                UserPermissions.ExportReports
            },

            [UserRoles.Viewer] = new List<string>
            {
                // Read-only access to all entities
                UserPermissions.ViewClients,
                UserPermissions.ViewRegions,
                UserPermissions.ViewTMs,
                UserPermissions.ViewBuildings,
                UserPermissions.ViewUsers,
                UserPermissions.ViewReports,
                UserPermissions.ExportReports
            },

            [UserRoles.Auditor] = new List<string>
            {
                // Read access to all entities
                UserPermissions.ViewClients,
                UserPermissions.ViewRegions,
                UserPermissions.ViewTMs,
                UserPermissions.ViewBuildings,
                UserPermissions.ViewUsers,
                
                // Full reports access
                UserPermissions.ViewReports,
                UserPermissions.GenerateReports,
                UserPermissions.ExportReports
            }
        };

        /// <summary>
        /// Check if a user has a specific permission based on their roles
        /// </summary>
        public static bool UserHasPermission(User user, string permission)
        {
            // Check direct permissions first
            if (user.Permissions.Contains(permission))
                return true;

            // Check role-based permissions
            foreach (var role in user.Roles)
            {
                if (RolePermissionMap.TryGetValue(role, out var rolePermissions))
                {
                    if (rolePermissions.Contains(permission))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get all permissions for a user based on their roles
        /// </summary>
        public static List<string> GetUserPermissions(User user)
        {
            var permissions = new HashSet<string>(user.Permissions);

            foreach (var role in user.Roles)
            {
                if (RolePermissionMap.TryGetValue(role, out var rolePermissions))
                {
                    foreach (var permission in rolePermissions)
                    {
                        permissions.Add(permission);
                    }
                }
            }

            return permissions.ToList();
        }

        /// <summary>
        /// Check if a user can modify business entities (clients, regions, TMs)
        /// </summary>
        public static bool UserCanModifyBusinessEntities(User user)
        {
            return user.Roles.Contains(UserRoles.Admin) ||
                   user.Roles.Contains(UserRoles.Manager);
        }

        /// <summary>
        /// Check if a user can modify technical entities (buildings, blocks)
        /// </summary>
        public static bool UserCanModifyTechnicalEntities(User user)
        {
            return user.Roles.Contains(UserRoles.Admin) ||
                   user.Roles.Contains(UserRoles.Engineer);
        }

        /// <summary>
        /// Check if a user can manage other users
        /// </summary>
        public static bool UserCanManageUsers(User user)
        {
            return user.Roles.Contains(UserRoles.Admin);
        }
    }
}