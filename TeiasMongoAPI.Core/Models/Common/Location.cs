using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Core.Models.Common
{
    public class Location
    {
        public required double Latitude { get; set; }
        public required double Longitude { get; set; }
    }
}
