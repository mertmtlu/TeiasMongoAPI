using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Response.Building
{
    public class BuildingStatisticsResponseDto
    {
        public string BuildingId { get; set; } = string.Empty;
        public int BlockCount { get; set; }
        public int ConcreteBlockCount { get; set; }
        public int MasonryBlockCount { get; set; }
        public double TotalArea { get; set; }
        public double MaxHeight { get; set; }
        public int Code { get; set; }
        public int BKS { get; set; }
    }
}
