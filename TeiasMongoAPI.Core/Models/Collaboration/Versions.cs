using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public class Version : AEntityBase
    {
        public ObjectId ProgramId { get; set; }  // Reference to parent program

        public int VersionNumber { get; set; }

        public string CommitMessage { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;  // User ID

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string Status { get; set; } = "pending";  // pending, approved, rejected

        public string? Reviewer { get; set; }  // User ID of reviewer

        public DateTime? ReviewedAt { get; set; }

        public string? ReviewComments { get; set; }

        public List<VersionFile> Files { get; set; } = new List<VersionFile>();
    }

    public class VersionFile
    {
        public string Path { get; set; } = string.Empty;  // Relative path within program

        public string StorageKey { get; set; } = string.Empty;  // Reference to storage location

        public string Hash { get; set; } = string.Empty;  // Content hash for change detection

        public long Size { get; set; }  // File size in bytes

        public string FileType {  get; set; } = string.Empty;
    }
}