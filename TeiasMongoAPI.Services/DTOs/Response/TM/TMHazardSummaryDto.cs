using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeiasMongoAPI.Services.DTOs.Response.Hazard;

namespace TeiasMongoAPI.Services.DTOs.Response.TM
{
    public class TMHazardSummaryDto
    {
        public string TMId { get; set; } = string.Empty;
        public HazardDto FireHazard { get; set; } = null!;
        public HazardDto SecurityHazard { get; set; } = null!;
        public HazardDto FloodHazard { get; set; } = null!;
        public double OverallRiskScore { get; set; }
    }
}
