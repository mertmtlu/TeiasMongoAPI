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
                 UserPermissions.ManageWorkflows
            },

            [UserRoles.Manager] = new List<string>
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
                 UserPermissions.ManageWorkflows
            },

            [UserRoles.Engineer] = new List<string>
            {
                 // Client permissions (Manager only)
                 UserPermissions.ViewClients,

                 // Region permissions (Manager only)
                 UserPermissions.ViewRegions,

                 // TM permissions (Manager only)
                 UserPermissions.ViewTMs,

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

                 // User management (Admin only)
                 UserPermissions.ViewUsers,
                 UserPermissions.UpdateUsers,

                 // Reports permissions
                 UserPermissions.ViewReports,
                 UserPermissions.GenerateReports,
                 UserPermissions.ExportReports,

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

                 // Workflow permissions
                 UserPermissions.ViewWorkflows,
                 UserPermissions.CreateWorkflows,
                 UserPermissions.EditWorkflows,
                 UserPermissions.DeleteWorkflows,
                 UserPermissions.ExecuteWorkflows,
                 UserPermissions.ManageWorkflows
            },

            [UserRoles.Viewer] = new List<string>
            {
                 // Client permissions (Manager only)
                 UserPermissions.ViewClients,

                 // Region permissions (Manager only)
                 UserPermissions.ViewRegions,

                 // TM permissions (Manager only)
                 UserPermissions.ViewTMs,

                 // Building permissions (Engineer only)
                 UserPermissions.ViewBuildings,

                 // Block permissions (Engineer only)
                 UserPermissions.ViewBlocks,

                 // Alternative TM permissions (Engineer for technical, Manager for business)
                 UserPermissions.ViewAlternativeTMs,

                 // User management (Admin only)
                 UserPermissions.ViewUsers,

                 // Reports permissions
                 UserPermissions.ViewReports,
                 UserPermissions.GenerateReports,

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

                 // Workflow permissions
                 UserPermissions.ViewWorkflows,
                 UserPermissions.ExecuteWorkflows
            },

            [UserRoles.Auditor] = new List<string>
            {
                 // Client permissions (Manager only)
                 UserPermissions.ViewClients,

                 // Region permissions (Manager only)
                 UserPermissions.ViewRegions,

                 // TM permissions (Manager only)
                 UserPermissions.ViewTMs,

                 // Building permissions (Engineer only)
                 UserPermissions.ViewBuildings,

                 // Block permissions (Engineer only)
                 UserPermissions.ViewBlocks,

                 // Alternative TM permissions (Engineer for technical, Manager for business)
                 UserPermissions.ViewAlternativeTMs,

                 // User management (Admin only)
                 UserPermissions.ViewUsers,
                 UserPermissions.ManageRoles,

                 // Reports permissions
                 UserPermissions.ViewReports,
                 UserPermissions.GenerateReports,
                 UserPermissions.ExportReports,
                 UserPermissions.ViewAuditLogs,

                 // Program (software) permission
                 UserPermissions.ViewPrograms,
                 UserPermissions.UpdatePrograms,
                 UserPermissions.DeletePrograms,
                 UserPermissions.DeployPrograms,
                 UserPermissions.ExecutePrograms,

                 // Version permissions  
                 UserPermissions.ViewVersions,
                 UserPermissions.UpdateVersions,
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
                 UserPermissions.ManageWorkflows
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