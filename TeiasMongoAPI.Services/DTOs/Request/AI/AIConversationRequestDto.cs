using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.AI
{
    /// <summary>
    /// Request DTO for conversing with the AI assistant
    /// </summary>
    public class AIConversationRequestDto
    {
        /// <summary>
        /// The user's current prompt/question
        /// </summary>
        [Required(ErrorMessage = "User prompt is required")]
        [StringLength(4000, ErrorMessage = "Prompt must be 4000 characters or less")]
        public required string UserPrompt { get; set; }

        /// <summary>
        /// The program ID the user is working on
        /// </summary>
        [Required(ErrorMessage = "Program ID is required")]
        public required string ProgramId { get; set; }

        /// <summary>
        /// Optional specific version ID. If null, uses current version or draft state
        /// </summary>
        public string? VersionId { get; set; }

        /// <summary>
        /// Recent conversation history (client maintains this)
        /// Limited to last N messages to control token usage
        /// </summary>
        public List<ConversationMessage> ConversationHistory { get; set; } = new();

        /// <summary>
        /// Optional: Currently open/visible files in the editor
        /// Helps AI understand user's current focus
        /// Includes support for unsaved changes - if Content is provided, it takes precedence over stored files
        /// </summary>
        public List<OpenFileContext>? CurrentlyOpenFiles { get; set; }

        /// <summary>
        /// Optional: Current cursor position/selection for targeted assistance
        /// Format: "filename:lineNumber:columnNumber"
        /// </summary>
        public string? CursorContext { get; set; }

        /// <summary>
        /// Optional: User preferences for AI behavior
        /// </summary>
        public AIPreferences? Preferences { get; set; }
    }
}
