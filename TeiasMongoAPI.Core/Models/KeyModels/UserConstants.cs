namespace TeiasMongoAPI.Core.Models.KeyModels
{
    // Common roles for the system
    public static class UserRoles
    {
        public const string Admin = "Admin";           // Full system access
        public const string Manager = "Manager";       // Business/client management
        public const string Engineer = "Engineer";     // Technical/building operations
        public const string Viewer = "Viewer";         // Read-only access
        public const string Auditor = "Auditor";       // Audit/compliance access
    }

    // Common permissions
    public static class UserPermissions
    {
        // Client permissions (Manager only)
        public const string ViewClients = "clients.view";
        public const string CreateClients = "clients.create";
        public const string UpdateClients = "clients.update";
        public const string DeleteClients = "clients.delete";

        // Region permissions (Manager only)
        public const string ViewRegions = "regions.view";
        public const string CreateRegions = "regions.create";
        public const string UpdateRegions = "regions.update";
        public const string DeleteRegions = "regions.delete";

        // TM permissions (Manager only)
        public const string ViewTMs = "tms.view";
        public const string CreateTMs = "tms.create";
        public const string UpdateTMs = "tms.update";
        public const string DeleteTMs = "tms.delete";

        // Building permissions (Engineer only)
        public const string ViewBuildings = "buildings.view";
        public const string CreateBuildings = "buildings.create";
        public const string UpdateBuildings = "buildings.update";
        public const string DeleteBuildings = "buildings.delete";

        // Block permissions (Engineer only)
        public const string ViewBlocks = "blocks.view";
        public const string CreateBlocks = "blocks.create";
        public const string UpdateBlocks = "blocks.update";
        public const string DeleteBlocks = "blocks.delete";

        // Alternative TM permissions (Engineer for technical, Manager for business)
        public const string ViewAlternativeTMs = "alternativetms.view";
        public const string CreateAlternativeTMs = "alternativetms.create";
        public const string UpdateAlternativeTMs = "alternativetms.update";
        public const string DeleteAlternativeTMs = "alternativetms.delete";

        // User management (Admin only)
        public const string ViewUsers = "users.view";
        public const string CreateUsers = "users.create";
        public const string UpdateUsers = "users.update";
        public const string DeleteUsers = "users.delete";
        public const string ManageRoles = "users.roles";

        // Reports permissions
        public const string ViewReports = "reports.view";
        public const string GenerateReports = "reports.generate";
        public const string ExportReports = "reports.export";
        public const string ViewAuditLogs = "reports.audit";
    }
}