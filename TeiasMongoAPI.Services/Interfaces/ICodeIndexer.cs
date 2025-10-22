using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// Service for generating comprehensive project structure indexes
    /// Creates lightweight summaries of entire codebases showing all symbols without implementations
    /// </summary>
    public interface ICodeIndexer
    {
        /// <summary>
        /// Generate a complete project index showing all files, classes, functions, and their signatures
        /// Does NOT include function bodies - only declarations and signatures
        /// This allows the AI to "see" the entire project structure without consuming many tokens
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Project index with all symbols</returns>
        Task<ProjectIndex> GenerateProjectIndexAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate a formatted text representation of the project index
        /// Suitable for including in LLM prompts
        /// </summary>
        /// <param name="index">Project index</param>
        /// <returns>Formatted text with file tree and symbol signatures</returns>
        string FormatIndexForPrompt(ProjectIndex index);

        /// <summary>
        /// Extract symbols (classes, functions, interfaces) from a single file
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <param name="content">File content</param>
        /// <param name="language">Programming language</param>
        /// <returns>List of extracted symbols</returns>
        List<CodeSymbol> ExtractSymbols(string filePath, string content, string language);
    }
}
