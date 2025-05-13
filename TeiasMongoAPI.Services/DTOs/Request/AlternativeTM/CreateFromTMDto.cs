using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Request.AlternativeTM
{
    public class CreateFromTMDto
    {
        public required Common.LocationRequestDto Location { get; set; }
        public Common.AddressDto? Address { get; set; }
        public bool CopyHazardData { get; set; } = true;
        public bool CopyEarthquakeData { get; set; } = true;
    }
}
