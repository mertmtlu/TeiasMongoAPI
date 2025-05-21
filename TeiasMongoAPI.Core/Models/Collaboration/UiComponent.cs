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

        public bool IsGlobal { get; set; } = false;  // Available to all programs?

        public ObjectId? ProgramId { get; set; }  // If not global, specific to program

        public object Configuration { get; set; } = new object();  // Component-specific configuration

        public object Schema { get; set; } = new object();  // Expected inputs/outputs schema

        public string Status { get; set; } = "draft";  // draft, active, deprecated
    }
}