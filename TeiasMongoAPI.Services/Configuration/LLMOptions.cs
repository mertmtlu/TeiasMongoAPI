namespace TeiasMongoAPI.Services.Configuration
{
    /// <summary>
    /// Configuration options for LLM (Language Model) integration
    /// </summary>
    public class LLMOptions
    {
        /// <summary>
        /// Configuration section name in appsettings.json
        /// </summary>
        public const string SectionName = "LLM";

        /// <summary>
        /// LLM provider: "Gemini", "OpenAI", "Claude", etc.
        /// </summary>
        public string Provider { get; set; } = "Gemini";

        /// <summary>
        /// API key for the LLM provider (loaded from environment variables)
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Model name/ID to use
        /// </summary>
        public string Model { get; set; } = "gemini-2.0-flash-exp";

        /// <summary>
        /// Maximum tokens to use for context
        /// </summary>
        public int MaxContextTokens { get; set; } = 100000;

        /// <summary>
        /// Maximum tokens in the response
        /// </summary>
        public int MaxResponseTokens { get; set; } = 8192;

        /// <summary>
        /// Temperature for response generation (0.0 to 1.0)
        /// Lower = more deterministic, Higher = more creative
        /// </summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// Maximum number of messages to keep in conversation history
        /// </summary>
        public int ConversationHistoryLimit { get; set; } = 10;

        /// <summary>
        /// API endpoint URL (if using custom endpoint)
        /// </summary>
        public string? ApiEndpoint { get; set; }

        /// <summary>
        /// Timeout for API requests in seconds
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 120;
    }
}
