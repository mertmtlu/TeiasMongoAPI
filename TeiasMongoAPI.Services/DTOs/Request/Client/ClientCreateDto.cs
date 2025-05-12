using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Services.DTOs.Request.Client
{
    public class ClientCreateDto
    {
        [Required]
        [MaxLength(200)]
        public required string Name { get; set; }

        [Required]
        public ClientType Type { get; set; }
    }
}