using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// Interface for Language Model (LLM) clients
    /// Provides abstraction over different LLM providers (Gemini, OpenAI, Claude, etc.)
    /// </summary>
    public interface ILLMClient
    {
        /// <summary>
        /// Send a chat completion request to the LLM
        /// </summary>
        /// <param name="systemPrompt">System instructions for the LLM</param>
        /// <param name="messages">Conversation messages (role + content)</param>
        /// <param name="temperature">Temperature for response generation (0.0 to 1.0)</param>
        /// <param name="maxTokens">Maximum tokens in the response</param>
        /// <param name="responseSchema">Optional JSON schema for structured output</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>LLM response</returns>
        Task<LLMResponse> ChatCompletionAsync(
            string systemPrompt,
            List<LLMMessage> messages,
            double? temperature = null,
            int? maxTokens = null,
            object? responseSchema = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Estimate token count for a given text
        /// </summary>
        /// <param name="text">Text to estimate</param>
        /// <returns>Estimated token count</returns>
        int EstimateTokenCount(string text);

        /// <summary>
        /// Get the model name being used
        /// </summary>
        string ModelName { get; }
    }

    /// <summary>
    /// Represents a message in the LLM conversation
    /// </summary>
    public class LLMMessage
    {
        /// <summary>
        /// Role: "user", "assistant", or "system"
        /// </summary>
        public required string Role { get; set; }

        /// <summary>
        /// Message content
        /// </summary>
        public required string Content { get; set; }
    }

    /// <summary>
    /// Response from the LLM
    /// </summary>
    public class LLMResponse
    {
        /// <summary>
        /// Generated text content
        /// </summary>
        public required string Content { get; set; }

        /// <summary>
        /// Total tokens used (prompt + completion)
        /// </summary>
        public int TotalTokens { get; set; }

        /// <summary>
        /// Tokens used in the prompt
        /// </summary>
        public int PromptTokens { get; set; }

        /// <summary>
        /// Tokens used in the completion
        /// </summary>
        public int CompletionTokens { get; set; }

        /// <summary>
        /// Model used for generation
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Finish reason (e.g., "stop", "length", "content_filter")
        /// </summary>
        public string? FinishReason { get; set; }
    }
}
