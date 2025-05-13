using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Response.Block
{
    public class BlockStatisticsDto
    {
        public string BlockId { get; set; } = string.Empty;
        public string ModelingType { get; set; } = string.Empty;
        public double Area { get; set; }
        public double Height { get; set; }
        public int StoreyCount { get; set; }
        public double AspectRatio { get; set; }
        public double VolumeEstimate { get; set; }
    }
}
