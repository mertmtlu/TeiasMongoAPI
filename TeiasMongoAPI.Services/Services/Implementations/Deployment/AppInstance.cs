namespace TeiasMongoAPI.Services.Services.Implementations.Deployment
{
    public class AppInstance
    {
        public string ProgramId { get; set; } = string.Empty;
        public string DeploymentPath { get; set; } = string.Empty;
        public string ApplicationUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? StoppedAt { get; set; }
        public Dictionary<string, object> Configuration { get; set; } = new();
        public int Port { get; set; }
        public int? ProcessId { get; set; }
    }

}
