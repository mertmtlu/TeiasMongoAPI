using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Response.Region
{
    public class RegionStatisticsDto
    {
        public string RegionId { get; set; } = string.Empty;
        public int CityCount { get; set; }
        public int TMCount { get; set; }
        public int ActiveTMCount { get; set; }
        public int BuildingCount { get; set; }
        public Dictionary<string, int> TMsPerCity { get; set; } = new();
    }
}
