using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Request.Collaboration
{
    public class RollbackRequestDto
    {
        [Required]
        public required string TargetVersion { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        public bool ForceRollback { get; set; } = false;
    }
}
