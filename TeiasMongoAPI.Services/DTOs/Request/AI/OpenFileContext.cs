namespace TeiasMongoAPI.Services.DTOs.Request.AI
{
    /// <summary>
    /// Represents context for a file that is currently open in the editor
    /// Includes support for unsaved changes
    /// </summary>
    public class OpenFileContext
    {
        /// <summary>
        /// Relative path to the file within the project
        /// Example: "src/utils.py", "Program.cs"
        /// </summary>
        public required string FilePath { get; set; }

        /// <summary>
        /// Current content of the file in the editor
        /// If null, content will be read from storage
        /// If provided, this takes precedence over stored content
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Whether the file has unsaved changes in the editor
        /// </summary>
        public bool HasUnsavedChanges { get; set; }

        /// <summary>
        /// Whether this file is currently in focus in the editor
        /// </summary>
        public bool IsFocused { get; set; }

        /// <summary>
        /// Optional: Line number where the cursor is currently positioned
        /// </summary>
        public int? CursorLine { get; set; }

        /// <summary>
        /// Optional: Column number where the cursor is currently positioned
        /// </summary>
        public int? CursorColumn { get; set; }

        /// <summary>
        /// Optional: Selected text range in the editor
        /// Format: "startLine:startCol-endLine:endCol"
        /// </summary>
        public string? SelectedRange { get; set; }
    }
}
