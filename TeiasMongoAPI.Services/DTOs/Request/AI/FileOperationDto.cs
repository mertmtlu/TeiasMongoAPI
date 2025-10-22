using System.ComponentModel.DataAnnotations;

namespace TeiasMongoAPI.Services.DTOs.Request.AI
{
    /// <summary>
    /// Represents a single file operation to be applied by the frontend
    /// </summary>
    public class FileOperationDto
    {
        /// <summary>
        /// Type of operation to perform
        /// </summary>
        [Required]
        public FileOperationType OperationType { get; set; }

        /// <summary>
        /// Relative path to the file within the project
        /// Examples: "src/utils.py", "Program.cs", "components/Header.tsx"
        /// </summary>
        [Required]
        public required string FilePath { get; set; }

        /// <summary>
        /// Full content of the file (for CREATE and UPDATE)
        /// Null for DELETE operations
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Optional: Explanation of what this operation does
        /// Displayed to user as a comment or tooltip
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Optional: Line number to focus/scroll to after applying
        /// </summary>
        public int? FocusLine { get; set; }

        /// <summary>
        /// Optional: Specific lines that were changed (for UPDATE operations)
        /// Helps with highlighting/diffing in the editor
        /// Format: "start-end" (e.g., "10-15,20-25")
        /// </summary>
        public string? ChangedLines { get; set; }
    }

    /// <summary>
    /// File operation types
    /// </summary>
    public enum FileOperationType
    {
        /// <summary>
        /// Create a new file
        /// </summary>
        CREATE,

        /// <summary>
        /// Update existing file content
        /// </summary>
        UPDATE,

        /// <summary>
        /// Delete a file
        /// </summary>
        DELETE
    }
}
