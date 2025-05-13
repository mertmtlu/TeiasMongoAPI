using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Response.TM
{
    public class TMStatisticsDto
    {
        public string TMId { get; set; } = string.Empty;
        public int BuildingCount { get; set; }
        public int MaxVoltage { get; set; }
        public int AlternativeTMCount { get; set; }
        public double OverallRiskScore { get; set; }
        public int DaysSinceAcceptance { get; set; }
    }
}
