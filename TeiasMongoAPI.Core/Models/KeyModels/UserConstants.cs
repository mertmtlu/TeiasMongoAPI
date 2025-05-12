namespace TeiasMongoAPI.Core.Models.KeyModels
{
    // Common roles for the system
    public static class UserRoles
    {
        public const string Admin = "Admin";
        public const string RegionManager = "RegionManager";
        public const string TMOperator = "TMOperator";
        public const string Viewer = "Viewer";
        public const string Auditor = "Auditor";
    }

    // Common permissions
    public static class UserPermissions
    {
        // Client permissions
        public const string ViewClients = "clients.view";
        public const string CreateClients = "clients.create";
        public const string UpdateClients = "clients.update";
        public const string DeleteClients = "clients.delete";

        // Region permissions
        public const string ViewRegions = "regions.view";
        public const string CreateRegions = "regions.create";
        public const string UpdateRegions = "regions.update";
        public const string DeleteRegions = "regions.delete";

        // TM permissions
        public const string ViewTMs = "tms.view";
        public const string CreateTMs = "tms.create";
        public const string UpdateTMs = "tms.update";
        public const string DeleteTMs = "tms.delete";

        // Building permissions
        public const string ViewBuildings = "buildings.view";
        public const string CreateBuildings = "buildings.create";
        public const string UpdateBuildings = "buildings.update";
        public const string DeleteBuildings = "buildings.delete";

        // User management permissions
        public const string ViewUsers = "users.view";
        public const string CreateUsers = "users.create";
        public const string UpdateUsers = "users.update";
        public const string DeleteUsers = "users.delete";
        public const string ManageRoles = "users.roles";

        // Reports permissions
        public const string ViewReports = "reports.view";
        public const string GenerateReports = "reports.generate";
        public const string ExportReports = "reports.export";
    }
}