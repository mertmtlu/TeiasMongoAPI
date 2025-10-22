using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.AI
{
    /// <summary>
    /// A single message in the conversation
    /// </summary>
    public class ConversationMessage
    {
        /// <summary>
        /// Role: "user" or "assistant"
        /// </summary>
        [Required]
        public required string Role { get; set; }

        /// <summary>
        /// Message content
        /// </summary>
        [Required]
        public required string Content { get; set; }

        /// <summary>
        /// Timestamp of the message
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional: File operations that were part of this message (for assistant messages)
        /// Helps AI understand what changes were previously made
        /// </summary>
        public List<FileOperationDto>? FileOperations { get; set; }
    }
}
