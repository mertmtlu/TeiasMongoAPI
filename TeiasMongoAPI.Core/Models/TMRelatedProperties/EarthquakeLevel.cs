using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Core.Models.TMRelatedProperties
{
    public class EarthquakeLevel
    {
        public double PGA { get; set; }
        public double PGV { get; set; }
        public double Ss { get; set; }
        public double S1 { get; set; }
        public double Sds { get; set; }
        public double Sd1 { get; set; }
    }
}
