using TeiasMongoAPI.Services.DTOs.Response.AI;

namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// Service for evaluating the quality of AI-generated responses
    /// </summary>
    public interface IResponseEvaluator
    {
        /// <summary>
        /// Evaluates the quality and appropriateness of an AI response
        /// </summary>
        /// <param name="userPrompt">The original user prompt</param>
        /// <param name="aiResponse">The AI-generated response to evaluate</param>
        /// <param name="context">Optional context information for evaluation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Evaluation result with quality metrics</returns>
        Task<ResponseEvaluationResult> EvaluateResponseAsync(
            string userPrompt,
            AIConversationResponseDto aiResponse,
            string? context = null,
            CancellationToken cancellationToken = default);
    }
}
