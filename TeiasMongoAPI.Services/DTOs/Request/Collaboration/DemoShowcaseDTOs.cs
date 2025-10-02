using System.ComponentModel.DataAnnotations;
using TeiasMongoAPI.Core.Models.Collaboration;
using MongoDB.Bson;

namespace TeiasMongoAPI.Services.DTOs.Request.Collaboration
{
    public class DemoShowcaseCreateDto
    {
        [Required]
        public required string AssociatedAppId { get; set; }

        [Required]
        public AppType AppType { get; set; }

        [Required]
        public required string Tab { get; set; }

        [Required]
        public required string PrimaryGroup { get; set; }

        [Required]
        public required string SecondaryGroup { get; set; }

        [Required]
        public required string VideoPath { get; set; }
    }

    public class DemoShowcaseUpdateDto
    {
        public string? AssociatedAppId { get; set; }
        public AppType? AppType { get; set; }
        public string? Tab { get; set; }
        public string? PrimaryGroup { get; set; }
        public string? SecondaryGroup { get; set; }
        public string? VideoPath { get; set; }
    }

    public class ExecutionRequestDto
    {
        [Required]
        public required object Inputs { get; set; }
    }
}
