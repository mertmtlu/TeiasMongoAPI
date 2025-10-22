namespace TeiasMongoAPI.Services.DTOs.Request.AI
{
    /// <summary>
    /// AI behavior preferences
    /// </summary>
    public class AIPreferences
    {
        /// <summary>
        /// Verbosity level: "concise", "normal", "detailed"
        /// </summary>
        public string Verbosity { get; set; } = "normal";

        /// <summary>
        /// Whether to explain reasoning for changes
        /// </summary>
        public bool ExplainReasoning { get; set; } = true;

        /// <summary>
        /// Whether to suggest best practices
        /// </summary>
        public bool SuggestBestPractices { get; set; } = true;

        /// <summary>
        /// Maximum number of files to modify in a single response
        /// </summary>
        public int MaxFileOperations { get; set; } = 5;

        /// <summary>
        /// Context gathering mode: "aggressive", "balanced", "comprehensive"
        /// </summary>
        public string ContextMode { get; set; } = "balanced";
    }
}
