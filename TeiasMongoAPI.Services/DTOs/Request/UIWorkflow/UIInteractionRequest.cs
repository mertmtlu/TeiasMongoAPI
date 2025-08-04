using MongoDB.Bson;

namespace TeiasMongoAPI.Services.DTOs.Request.UIWorkflow
{
    public class UIInteractionRequest
    {
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required string InteractionType { get; set; } // "UserInput", "Confirmation", "Selection", etc.
        public required BsonDocument InputSchema { get; set; }
        public BsonDocument? InitialData { get; set; }
        public TimeSpan? Timeout { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class UIInteractionSubmissionRequest
    {
        public required Dictionary<string, object> ResponseData { get; set; }
        public string Action { get; set; } = "submit"; // "submit", "skip", "cancel"
        public string? Comments { get; set; }
    }
}