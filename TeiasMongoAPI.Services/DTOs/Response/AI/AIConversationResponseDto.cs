using TeiasMongoAPI.Services.DTOs.Request.AI;

namespace TeiasMongoAPI.Services.DTOs.Response.AI
{
    /// <summary>
    /// Response from the AI assistant containing chat text and file operations
    /// </summary>
    public class AIConversationResponseDto
    {
        /// <summary>
        /// Human-readable text to display in the chat interface
        /// This is the AI's conversational response
        /// </summary>
        public required string DisplayText { get; set; }

        /// <summary>
        /// List of file operations for the frontend to apply to the code editor
        /// Empty list if the response is purely conversational (no code changes)
        /// </summary>
        public List<FileOperationDto> FileOperations { get; set; } = new();

        /// <summary>
        /// Updated conversation history including the new exchange
        /// Client should replace its local history with this
        /// </summary>
        public List<ConversationMessage> ConversationHistory { get; set; } = new();

        /// <summary>
        /// Optional: Suggested follow-up prompts
        /// Helps users continue the conversation naturally
        /// </summary>
        public List<string>? SuggestedFollowUps { get; set; }

        /// <summary>
        /// Optional: Warnings or important notes to display to the user
        /// Example: "This change affects 3 other files that import this module"
        /// </summary>
        public List<string>? Warnings { get; set; }

        /// <summary>
        /// Metadata about the AI's response
        /// </summary>
        public AIResponseMetadata Metadata { get; set; } = new();
    }
}
