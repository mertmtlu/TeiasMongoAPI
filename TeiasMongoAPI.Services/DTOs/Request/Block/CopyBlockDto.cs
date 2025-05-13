using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Request.Block
{
    public class CopyBlockDto
    {
        public required string NewBlockId { get; set; }
        public string? NewName { get; set; }
    }
}
