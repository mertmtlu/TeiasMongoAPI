using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Services.DTOs.Request.Client
{
    public class ClientUpdateDto
    {
        [MaxLength(200)]
        public string? Name { get; set; }

        public ClientType? Type { get; set; }
    }
}