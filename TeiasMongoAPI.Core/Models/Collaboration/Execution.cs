﻿using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public class Execution : AEntityBase
    {
        public ObjectId ProgramId { get; set; }
        public ObjectId VersionId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string ExecutionType { get; set; } = "code_execution"; // "code_execution", "web_app_deploy"
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; } = "running"; // running, completed, failed, stopped, paused
        public object Parameters { get; set; } = new object(); // Input parameters
        public ExecutionResults Results { get; set; } = new ExecutionResults();
        public ResourceUsage ResourceUsage { get; set; } = new ResourceUsage();
    }

    public class ExecutionResults
    {
        public int ExitCode { get; set; }

        public string Output { get; set; } = string.Empty; // Standard output (limited size)

        public List<string> OutputFiles { get; set; } = new List<string>(); // References to output files

        public string? Error { get; set; } // Error output if any

        public string? WebAppUrl { get; set; } // URL for deployed web applications
    }

    public class ResourceUsage
    {
        public double CpuTime { get; set; }

        public long MemoryUsed { get; set; }

        public long DiskUsed { get; set; }
    }
}