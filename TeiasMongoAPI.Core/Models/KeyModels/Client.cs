using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.KeyModels
{
    public class Client : AEntityBase
    {
        public required string Name { get; set; }
        public ClientType Type { get; set; }
    }

    public enum ClientType
    {
        Private,
        State,
    }
}
