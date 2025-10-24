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

        /// <summary>
        /// Response quality evaluation score (0.0 to 1.0)
        /// Null if evaluation was not performed or disabled
        /// </summary>
        public double? EvaluationScore { get; set; }

        /// <summary>
        /// Whether response evaluation failed quality checks
        /// True indicates the response may not adequately address the user's request
        /// </summary>
        public bool EvaluationFailed { get; set; }

        /// <summary>
        /// Number of retry attempts made (includes both 503 retries and evaluation retries)
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Issues found during evaluation (if any)
        /// </summary>
        public List<string>? EvaluationIssues { get; set; }
    }
}
