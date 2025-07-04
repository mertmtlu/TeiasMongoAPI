using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Request.Collaboration
{
    public class FileValidationRequest
    {
        [Required]
        public required string FileName { get; set; }

        [Required]
        public required byte[] Content { get; set; }

        [Required]
        public required string ContentType { get; set; }
    }

    public class BulkDownloadRequest
    {
        public List<string> FilePaths { get; set; } = new();
        public bool IncludeMetadata { get; set; } = false;
        public string? CompressionLevel { get; set; } = "optimal";
    }
}
