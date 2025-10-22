using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeiasMongoAPI.API.Attributes;
using TeiasMongoAPI.API.Controllers.Base;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Services.DTOs.Request.AI;
using TeiasMongoAPI.Services.DTOs.Response.AI;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.API.Controllers
{
    /// <summary>
    /// AI Assistant controller - handles conversational AI interactions for code assistance
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Route("api/ai")]
    [Authorize]
    public class AIAssistantController : BaseController
    {
        private readonly IAIAssistantService _aiAssistantService;

        public AIAssistantController(
            IAIAssistantService aiAssistantService,
            ILogger<AIAssistantController> logger)
            : base(logger)
        {
            _aiAssistantService = aiAssistantService;
        }

        /// <summary>
        /// Converse with the AI assistant about a specific program/version.
        /// The AI can suggest file modifications, answer questions, and provide guidance.
        /// </summary>
        /// <param name="request">Conversation request containing user prompt, history, and target program/version</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>AI response with display text and file operations to apply</returns>
        [HttpPost("converse")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        [ProducesResponseType(typeof(ApiResponse<AIConversationResponseDto>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<ApiResponse<AIConversationResponseDto>>> Converse(
            [FromBody] AIConversationRequestDto request,
            CancellationToken cancellationToken = default)
        {
            // Validate model state
            var validationResult = ValidateModelState<AIConversationResponseDto>();
            if (validationResult != null) return validationResult;

            // Validate ObjectIds
            var programIdResult = ParseObjectId(request.ProgramId, "programId");
            if (programIdResult.Result != null) return programIdResult.Result!;

            if (!string.IsNullOrEmpty(request.VersionId))
            {
                var versionIdResult = ParseObjectId(request.VersionId, "versionId");
                if (versionIdResult.Result != null) return versionIdResult.Result!;
            }

            return await ExecuteAsync(async () =>
            {
                var userId = CurrentUserId?.ToString();
                if (string.IsNullOrEmpty(userId))
                {
                    throw new UnauthorizedAccessException("User not authenticated");
                }

                return await _aiAssistantService.ConverseAsync(request, userId, cancellationToken);
            }, $"AI conversation for program {request.ProgramId}");
        }

        /// <summary>
        /// Get suggested prompts based on current project context
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of suggested prompts</returns>
        [HttpGet("suggestions/{programId}")]
        [RequirePermission(UserPermissions.ViewPrograms)]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetSuggestedPrompts(
            string programId,
            CancellationToken cancellationToken = default)
        {
            var objectIdResult = ParseObjectId(programId, "programId");
            if (objectIdResult.Result != null) return objectIdResult.Result!;

            return await ExecuteAsync(async () =>
            {
                return await _aiAssistantService.GetSuggestedPromptsAsync(programId, cancellationToken);
            }, $"Get AI suggestions for program {programId}");
        }
    }
}
