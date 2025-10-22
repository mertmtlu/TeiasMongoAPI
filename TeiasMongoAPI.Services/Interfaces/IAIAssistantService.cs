using TeiasMongoAPI.Services.DTOs.Request.AI;
using TeiasMongoAPI.Services.DTOs.Response.AI;

namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// Service for AI-assisted code development and conversational interactions
    /// </summary>
    public interface IAIAssistantService
    {
        /// <summary>
        /// Process a conversational request from the user and return AI-generated response
        /// with optional file operations to apply
        /// </summary>
        /// <param name="request">Conversation request with user prompt and context</param>
        /// <param name="userId">ID of the requesting user</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>AI response with display text, file operations, and updated conversation history</returns>
        Task<AIConversationResponseDto> ConverseAsync(
            AIConversationRequestDto request,
            string userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get context-aware suggested prompts for a given program
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of suggested prompts</returns>
        Task<List<string>> GetSuggestedPromptsAsync(
            string programId,
            CancellationToken cancellationToken = default);
    }
}
