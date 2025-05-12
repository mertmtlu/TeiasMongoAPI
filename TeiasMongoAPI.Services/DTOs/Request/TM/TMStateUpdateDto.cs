using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Services.DTOs.Request.TM
{
    public class TMStateUpdateDto
    {
        [Required]
        public TMState State { get; set; }
    }
}