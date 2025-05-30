using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using TeiasMongoAPI.Core.Models.Base;
using System;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public class UiComponent : AEntityBase
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;  // input_form, visualization, composite
        public string Creator { get; set; } = string.Empty;  // User ID
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Version-specific properties (required)
        public ObjectId ProgramId { get; set; }  // Required - which program this component belongs to
        public ObjectId VersionId { get; set; }  // Required - which version this component belongs to

        public object Configuration { get; set; } = new object();  // Component-specific configuration
        public object Schema { get; set; } = new object();  // Expected inputs/outputs schema
        public string Status { get; set; } = "draft";  // draft, active, deprecated
        public List<string> Tags { get; set; } = new();
    }
}