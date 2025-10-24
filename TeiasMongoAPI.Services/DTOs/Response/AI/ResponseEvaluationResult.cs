namespace TeiasMongoAPI.Services.DTOs.Response.AI
{
    /// <summary>
    /// Result of evaluating an AI response for quality and appropriateness
    /// </summary>
    public class ResponseEvaluationResult
    {
        /// <summary>
        /// Whether the response is acceptable and should be returned to the user
        /// </summary>
        public bool IsAcceptable { get; set; }

        /// <summary>
        /// Confidence score for the response (0.0 to 1.0)
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// List of issues found during evaluation
        /// </summary>
        public List<string> Issues { get; set; } = new();

        /// <summary>
        /// Suggested improvements for the response (if any)
        /// </summary>
        public string? SuggestedImprovements { get; set; }

        /// <summary>
        /// Detailed reason for the evaluation result
        /// </summary>
        public string? EvaluationReason { get; set; }

        /// <summary>
        /// Whether the response is too short or incomplete
        /// </summary>
        public bool IsTooShort { get; set; }

        /// <summary>
        /// Whether file operations (if any) are logical and valid
        /// </summary>
        public bool AreFileOperationsValid { get; set; } = true;
    }
}
