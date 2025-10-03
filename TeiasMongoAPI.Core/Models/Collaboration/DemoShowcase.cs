using MongoDB.Bson.Serialization.Attributes;
using TeiasMongoAPI.Core.Models.Base;

namespace TeiasMongoAPI.Core.Models.Collaboration
{
    public class DemoShowcase : AEntityBase
    {
        [BsonElement("associatedAppId")]
        public required string AssociatedAppId { get; set; }  // ObjectId as string

        [BsonElement("appType")]
        public AppType AppType { get; set; }

        [BsonElement("tab")]
        public required string Tab { get; set; }  // e.g., "Data Processing", "Visualization"

        [BsonElement("primaryGroup")]
        public required string PrimaryGroup { get; set; }  // e.g., "Text Analysis", "Image Tools"

        [BsonElement("secondaryGroup")]
        public required string SecondaryGroup { get; set; }  // e.g., "Sentiment", "Translation"

        [BsonElement("tertiaryGroup")]
        public string? TertiaryGroup { get; set; }  // Optional third-level grouping

        [BsonElement("videoPath")]
        public required string VideoPath { get; set; }  // e.g., "/videos/abc123.mp4"
    }

    public enum AppType
    {
        Program,
        Workflow,
        RemoteApp
    }
}
