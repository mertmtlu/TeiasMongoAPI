using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Response.Hazard
{
    public class HazardResponseDto
    {
        public double Score { get; set; }
        public string Level { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool? HasCCTV { get; set; }
    }
}
