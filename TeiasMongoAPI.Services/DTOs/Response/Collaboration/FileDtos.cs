using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Response.Collaboration
{
    public class BulkOperationResult
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int TotalProcessed { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class BulkDownloadResult
    {
        public byte[] ZipContent { get; set; } = Array.Empty<byte>();
        public string FileName { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public int FileCount { get; set; }
        public List<string> IncludedFiles { get; set; } = new();
        public List<string> SkippedFiles { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

}
