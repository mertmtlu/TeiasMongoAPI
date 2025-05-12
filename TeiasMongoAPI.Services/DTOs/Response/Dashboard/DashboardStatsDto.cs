namespace TeiasMongoAPI.Services.DTOs.Response.Dashboard
{
    public class DashboardStatsDto
    {
        public OverviewStatsDto Overview { get; set; } = null!;
        public List<RegionStatsDto> RegionStats { get; set; } = new();
        public RiskDistributionDto RiskDistribution { get; set; } = null!;
        public List<RecentActivityDto> RecentActivities { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class OverviewStatsDto
    {
        public int TotalClients { get; set; }
        public int TotalRegions { get; set; }
        public int TotalTMs { get; set; }
        public int ActiveTMs { get; set; }
        public int TotalBuildings { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
    }

    public class RegionStatsDto
    {
        public string RegionId { get; set; } = string.Empty;
        public string RegionName { get; set; } = string.Empty;
        public int TMCount { get; set; }
        public int ActiveTMCount { get; set; }
        public double AverageRiskScore { get; set; }
        public string HighestRisk { get; set; } = string.Empty;
    }

    public class RiskDistributionDto
    {
        public int HighRiskCount { get; set; }
        public int MediumRiskCount { get; set; }
        public int LowRiskCount { get; set; }
        public Dictionary<string, int> RiskByType { get; set; } = new();
    }

    public class RecentActivityDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}