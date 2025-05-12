using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.KeyModels
{
    public class Region : AEntityBase
    {
        public required ObjectId ClientID { get; set; }
        public required int Id { get; set; }  // Replaced with No (genel_bilgi: bolge_no)
        public required List<string> Cities { get; set; }
        public required string Headquarters { get; set; }
    }
}
