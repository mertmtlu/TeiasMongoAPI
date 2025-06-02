using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeiasMongoAPI.Services.DTOs.Request.Auth
{
    public class RevokeTokenDto
    {
        public required string Token { get; set; }
    }
}
