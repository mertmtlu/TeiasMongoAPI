using System.Collections.Generic;

namespace TeiasMongoAPI.Core.Models.KeyModels
{
    public static class RolePermissions
    {
        public static Dictionary<string, List<string>> RolePermissionMap = new()
        {
            [UserRoles.Admin] = new List<string>
            {
                 // Client permissions (Manager only)
                 UserPermissions.ViewClients,
                 UserPermissions.CreateClients,
                 UserPermissions.UpdateClients,
                 UserPermissions.DeleteClients,

                 // Region permissions (Manager only)
                 UserPermissions.ViewRegions,
                 UserPermissions.CreateRegions,
                 UserPermissions.UpdateRegions,
                 UserPermissions.DeleteRegions,

                 // TM permissions (Manager only)
                 UserPermissions.ViewTMs,
                 UserPermissions.CreateTMs,
                 UserPermissions.UpdateTMs,
                 UserPermissions.DeleteTMs,

                 // Building permissions (Engineer only)
                 UserPermissions.ViewBuildings,
                 UserPermissions.CreateBuildings,
                 UserPermissions.UpdateBuildings,
                 UserPermissions.DeleteBuildings,

                 // Block permissions (Engineer only)
                 UserPermissions.ViewBlocks,
                 UserPermissions.CreateBlocks,
                 UserPermissions.UpdateBlocks,
                 UserPermissions.DeleteBlocks,

                 // Alternative TM permissions (Engineer for technical, Manager for business)
                 UserPermissions.ViewAlternativeTMs,
                 UserPermissions.CreateAlternativeTMs,
                 UserPermissions.UpdateAlternativeTMs,
                 UserPermissions.DeleteAlternativeTMs,

                 // User management (Admin only)
                 UserPermissions.ViewUsers,
                 UserPermissions.CreateUsers,
                 UserPermissions.UpdateUsers,
                 UserPermissions.DeleteUsers,
                 UserPermissions.ManageRoles,

                 // Reports permissions
                 UserPermissions.ViewReports,
                 UserPermissions.GenerateReports,
                 UserPermissions.ExportReports,
                 UserPermissions.ViewAuditLogs,

                 // Program (software) permission
                 UserPermissions.ViewPrograms,
                 UserPermissions.CreatePrograms,
                 UserPermissions.UpdatePrograms,
                 UserPermissions.DeletePrograms,
                 UserPermissions.DeployPrograms,
                 UserPermissions.ManagePrograms,
                 UserPermissions.ExecutePrograms,

                 // Version permissions  
                 UserPermissions.ViewVersions,
                 UserPermissions.CreateVersions,
                 UserPermissions.UpdateVersions,
                 UserPermissions.DeleteVersions,
                 UserPermissions.ApproveVersions,
                 UserPermissions.RejectVersions,
                 UserPermissions.DeployVersions,

                 // Execution permissions
                 UserPermissions.ViewExecutions,
                 UserPermissions.CreateExecutions,
                 UserPermissions.ManageExecutions,
                 UserPermissions.ViewExecutionResults,

                 // UI Component permissions
                 UserPermissions.ViewComponents,
                 UserPermissions.CreateComponents,
                 UserPermissions.UpdateComponents,
                 UserPermissions.DeleteComponents,

                 // Request permissions
                 UserPermissions.ViewRequests,
                 UserPermissions.CreateRequests,
                 UserPermissions.UpdateRequests,
                 UserPermissions.DeleteRequests,
                 UserPermissions.AssignRequests,

                 // Workflow permissions
                 UserPermissions.ViewWorkflows,
                 UserPermissions.CreateWorkflows,
                 UserPermissions.EditWorkflows,
                 UserPermissions.DeleteWorkflows,
                 UserPermissions.ExecuteWorkflows,
                 UserPermissions.ManageWorkflows,

                 // Group permissions
                 UserPermissions.GroupView,
                 UserPermissions.GroupCreate,
                 UserPermissions.GroupEdit,
                 UserPermissions.GroupDelete,
                 UserPermissions.GroupMemberManage,
                 UserPermissions.GroupPermissionManage
            },

            [UserRoles.ExternalDev] = new List<string>
            {
                 // Program (software) permission
                 UserPermissions.ViewPrograms,
                 UserPermissions.CreatePrograms,
                 UserPermissions.UpdatePrograms,
                 UserPermissions.DeletePrograms,
                 UserPermissions.DeployPrograms,
                 UserPermissions.ManagePrograms,
                 UserPermissions.ExecutePrograms,

                 // Version permissions  
                 UserPermissions.ViewVersions,
                 UserPermissions.CreateVersions,
                 UserPermissions.UpdateVersions,
                 UserPermissions.DeleteVersions,
                 UserPermissions.ApproveVersions,
                 UserPermissions.RejectVersions,
                 UserPermissions.DeployVersions,

                 // Execution permissions
                 UserPermissions.ViewExecutions,
                 UserPermissions.CreateExecutions,
                 UserPermissions.ManageExecutions,
                 UserPermissions.ViewExecutionResults,

                 // UI Component permissions
                 UserPermissions.ViewComponents,
                 UserPermissions.CreateComponents,
                 UserPermissions.UpdateComponents,
                 UserPermissions.DeleteComponents,

                 // Request permissions
                 UserPermissions.ViewRequests,
                 UserPermissions.CreateRequests,
                 UserPermissions.UpdateRequests,
                 UserPermissions.DeleteRequests,
                 UserPermissions.AssignRequests,

                 // Workflow permissions
                 UserPermissions.ViewWorkflows,
                 UserPermissions.CreateWorkflows,
                 UserPermissions.EditWorkflows,
                 UserPermissions.DeleteWorkflows,
                 UserPermissions.ExecuteWorkflows,
                 UserPermissions.ManageWorkflows,

                 // Group permissions (limited for external dev)
                 UserPermissions.GroupView,
                 UserPermissions.GroupCreate,
                 UserPermissions.GroupEdit,
                 UserPermissions.GroupMemberManage
            },

            [UserRoles.InternalDev] = new List<string>
            {
                 // Program (software) permission
                 UserPermissions.ViewPrograms,
                 UserPermissions.CreatePrograms,
                 UserPermissions.UpdatePrograms,
                 UserPermissions.DeletePrograms,
                 UserPermissions.DeployPrograms,
                 UserPermissions.ManagePrograms,
                 UserPermissions.ExecutePrograms,

                 // Version permissions  
                 UserPermissions.ViewVersions,
                 UserPermissions.CreateVersions,
                 UserPermissions.UpdateVersions,
                 UserPermissions.DeleteVersions,
                 UserPermissions.ApproveVersions,
                 UserPermissions.RejectVersions,
                 UserPermissions.DeployVersions,

                 // Execution permissions
                 UserPermissions.ViewExecutions,
                 UserPermissions.CreateExecutions,
                 UserPermissions.ManageExecutions,
                 UserPermissions.ViewExecutionResults,

                 // UI Component permissions
                 UserPermissions.ViewComponents,
                 UserPermissions.CreateComponents,
                 UserPermissions.UpdateComponents,
                 UserPermissions.DeleteComponents,

                 // Request permissions
                 UserPermissions.ViewRequests,
                 UserPermissions.CreateRequests,
                 UserPermissions.UpdateRequests,
                 UserPermissions.DeleteRequests,
                 UserPermissions.AssignRequests,

                 // Workflow permissions
                 UserPermissions.ViewWorkflows,
                 UserPermissions.CreateWorkflows,
                 UserPermissions.EditWorkflows,
                 UserPermissions.DeleteWorkflows,
                 UserPermissions.ExecuteWorkflows,
                 UserPermissions.ManageWorkflows,

                 // Group permissions (full for internal dev)
                 UserPermissions.GroupView,
                 UserPermissions.GroupCreate,
                 UserPermissions.GroupEdit,
                 UserPermissions.GroupDelete,
                 UserPermissions.GroupMemberManage,
                 UserPermissions.GroupPermissionManage
            },

            [UserRoles.ExternalUser] = new List<string>
            {
                 // Program (software) permission
                 UserPermissions.ViewPrograms,
                 UserPermissions.ExecutePrograms,

                 // Execution permissions
                 UserPermissions.ViewExecutions,
                 UserPermissions.CreateExecutions,
                 UserPermissions.ManageExecutions,
                 UserPermissions.ViewExecutionResults,

                 // Request permissions
                 UserPermissions.ViewRequests,
                 UserPermissions.CreateRequests,
                 UserPermissions.UpdateRequests,
                 UserPermissions.DeleteRequests,
                 UserPermissions.AssignRequests,

                 // UI Component permissions
                 UserPermissions.ViewComponents,

                 // Workflow permissions
                 UserPermissions.ViewWorkflows,
                 UserPermissions.ExecuteWorkflows,

                 // Group permissions (view only for external users)
                 UserPermissions.GroupView,
            },

            [UserRoles.InternalUser] = new List<string>
            {
                 // Program (software) permission
                 UserPermissions.ViewPrograms,
                 UserPermissions.ExecutePrograms,

                 // Version permissions  
                 UserPermissions.ViewVersions,

                 // Execution permissions
                 UserPermissions.ViewExecutions,
                 UserPermissions.CreateExecutions,
                 UserPermissions.ManageExecutions,
                 UserPermissions.ViewExecutionResults,

                 // UI Component permissions
                 UserPermissions.ViewComponents,

                 // Request permissions
                 UserPermissions.ViewRequests,
                 UserPermissions.CreateRequests,
                 UserPermissions.UpdateRequests,
                 UserPermissions.DeleteRequests,
                 UserPermissions.AssignRequests,

                 // Workflow permissions
                 UserPermissions.ViewWorkflows,
                 UserPermissions.ExecuteWorkflows,

                 // Group permissions (view only for internal users)
                 UserPermissions.GroupView,
            },
        };

        /// <summary>
        /// Check if a user has a specific permission based on their role
        /// </summary>
        public static bool UserHasPermission(User user, string permission)
        {
            // Check direct permissions first
            if (user.Permissions.Contains(permission))
                return true;

            // Check role-based permissions
            if (!string.IsNullOrEmpty(user.Role) && RolePermissionMap.TryGetValue(user.Role, out var rolePermissions))
            {
                if (rolePermissions.Contains(permission))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Get all permissions for a user based on their role
        /// </summary>
        public static List<string> GetUserPermissions(User user)
        {
            var permissions = new HashSet<string>(user.Permissions);

            if (!string.IsNullOrEmpty(user.Role) && RolePermissionMap.TryGetValue(user.Role, out var rolePermissions))
            {
                foreach (var permission in rolePermissions)
                {
                    permissions.Add(permission);
                }
            }

            return permissions.ToList();
        }

        /// <summary>
        /// Check if a user can modify business entities (clients, regions, TMs)
        /// </summary>
        public static bool UserCanModifyBusinessEntities(User user)
        {
            return user.Role == UserRoles.Admin;
        }

        /// <summary>
        /// Check if a user can modify technical entities (buildings, blocks)
        /// </summary>
        public static bool UserCanModifyTechnicalEntities(User user)
        {
            return user.Role == UserRoles.Admin ||
                   user.Role == UserRoles.InternalDev;
        }

        /// <summary>
        /// Check if a user can manage other users
        /// </summary>
        public static bool UserCanManageUsers(User user)
        {
            return user.Role == UserRoles.Admin;
        }
    }
}