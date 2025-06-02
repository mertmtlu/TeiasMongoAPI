using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Response.Collaboration
{
    public class DeploymentHistoryDto
    {
        public string Id { get; set; } = string.Empty;
        public string ProgramId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string DeploymentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime DeployedAt { get; set; }
        public string DeployedBy { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ActiveDeploymentDto
    {
        public string ProgramId { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string DeploymentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime DeployedAt { get; set; }
        public string Url { get; set; } = string.Empty;
        public string HealthStatus { get; set; } = string.Empty;
    }

    public class DeploymentStatisticsDto
    {
        public int TotalDeployments { get; set; }
        public int SuccessfulDeployments { get; set; }
        public int FailedDeployments { get; set; }
        public int ActiveDeployments { get; set; }
        public Dictionary<string, int> DeploymentsByType { get; set; } = new();
        public TimeSpan AverageDeploymentTime { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
    }

    public class DeploymentResourceUsageDto
    {
        public string ProgramId { get; set; } = string.Empty;
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageMB { get; set; }
        public long DiskUsageMB { get; set; }
        public long NetworkInMB { get; set; }
        public long NetworkOutMB { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class ConnectionTestResult
    {
        public bool IsConnected { get; set; }
        public int ResponseTimeMs { get; set; }
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime TestedAt { get; set; }
    }
}
