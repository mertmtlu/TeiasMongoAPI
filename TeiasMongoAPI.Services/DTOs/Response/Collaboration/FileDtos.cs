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
}
