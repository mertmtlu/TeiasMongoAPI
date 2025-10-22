namespace TeiasMongoAPI.Services.DTOs.Response.AI
{
    /// <summary>
    /// Metadata about the AI response
    /// </summary>
    public class AIResponseMetadata
    {
        /// <summary>
        /// Number of tokens used in the request
        /// </summary>
        public int TokensUsed { get; set; }

        /// <summary>
        /// Processing time in milliseconds
        /// </summary>
        public int ProcessingTimeMs { get; set; }

        /// <summary>
        /// Files analyzed to generate the response
        /// </summary>
        public List<string> FilesAnalyzed { get; set; } = new();

        /// <summary>
        /// Confidence level: "high", "medium", "low"
        /// Indicates how confident the AI is about the suggested changes
        /// </summary>
        public string ConfidenceLevel { get; set; } = "medium";

        /// <summary>
        /// Whether additional context would improve the response
        /// If true, user might want to provide more details
        /// </summary>
        public bool NeedsMoreContext { get; set; }
    }
}
