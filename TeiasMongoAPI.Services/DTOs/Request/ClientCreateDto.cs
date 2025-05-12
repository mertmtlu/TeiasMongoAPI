using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Services.DTOs.Request
{
    public class ClientCreateDto
    {
        public required string Name { get; set; }
        public ClientType Type { get; set; }
    }
}