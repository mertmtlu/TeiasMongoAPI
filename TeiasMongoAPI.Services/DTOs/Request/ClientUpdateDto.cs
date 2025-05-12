using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Services.DTOs.Request
{
    public class ClientUpdateDto
    {
        public string? Name { get; set; }
        public ClientType? Type { get; set; }
    }
}