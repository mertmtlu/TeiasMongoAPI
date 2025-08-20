using TeiasMongoAPI.Core.Models.Common;

namespace TeiasMongoAPI.Services.DTOs.Response.Icon
{
    public class IconResponseDto
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public required string IconData { get; set; }
        public required string Format { get; set; }
        public int Size { get; set; }
        public required IconEntityType EntityType { get; set; }
        public required string EntityId { get; set; }
        public required string Creator { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}