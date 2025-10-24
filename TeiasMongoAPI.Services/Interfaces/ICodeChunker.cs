using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// Service for intelligently chunking code into semantic units for embedding
    /// </summary>
    public interface ICodeChunker
    {
        /// <summary>
        /// Chunk an entire project into semantic code units
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of code chunks ready for embedding</returns>
        Task<List<CodeChunk>> ChunkProjectAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Chunk a single file into semantic units
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="filePath">Path to the file</param>
        /// <param name="content">File content</param>
        /// <param name="language">Programming language</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of code chunks from this file</returns>
        Task<List<CodeChunk>> ChunkFileAsync(
            string programId,
            string versionId,
            string filePath,
            string content,
            string language,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Determine the chunking strategy based on file type and content
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <param name="content">File content</param>
        /// <param name="language">Programming language</param>
        /// <returns>Chunking strategy to use</returns>
        ChunkingStrategy DetermineStrategy(string filePath, string content, string language);
    }

    /// <summary>
    /// Strategy for chunking code files
    /// </summary>
    public enum ChunkingStrategy
    {
        /// <summary>
        /// Chunk by individual functions/methods
        /// Best for: Most code files with clear function boundaries
        /// </summary>
        FunctionLevel,

        /// <summary>
        /// Chunk by classes (include all methods in the class)
        /// Best for: Object-oriented code with cohesive classes
        /// </summary>
        ClassLevel,

        /// <summary>
        /// Use sliding window for large files without clear structure
        /// Best for: Long procedural code, configuration files
        /// </summary>
        SlidingWindow,

        /// <summary>
        /// Treat entire file as one chunk
        /// Best for: Small files (< 512 tokens), config files
        /// </summary>
        WholeFile,

        /// <summary>
        /// Chunk by top-level statements/blocks
        /// Best for: Scripts, notebooks
        /// </summary>
        BlockLevel
    }
}
