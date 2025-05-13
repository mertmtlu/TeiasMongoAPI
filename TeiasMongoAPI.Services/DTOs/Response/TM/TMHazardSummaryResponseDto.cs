using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeiasMongoAPI.Services.DTOs.Response.Hazard;

namespace TeiasMongoAPI.Services.DTOs.Response.TM
{
    public class TMHazardSummaryResponseDto
    {
        public string TMId { get; set; } = string.Empty;
        public HazardResponseDto FireHazard { get; set; } = null!;
        public HazardResponseDto SecurityHazard { get; set; } = null!;
        public HazardResponseDto FloodHazard { get; set; } = null!;
        public double OverallRiskScore { get; set; }
    }
}
