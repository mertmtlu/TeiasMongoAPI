using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Response.Client
{
    public class ClientStatisticsResponseDto
    {
        public string ClientId { get; set; } = string.Empty;
        public int RegionCount { get; set; }
        public int TotalTMs { get; set; }
        public int TotalBuildings { get; set; }
        public int ActiveTMs { get; set; }
    }
}
