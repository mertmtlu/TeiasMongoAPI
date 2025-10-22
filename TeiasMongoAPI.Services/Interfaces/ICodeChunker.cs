using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// Service for intelligently chunking code into semantic units for embedding
    /// </summary>
    public interface ICodeChunker
    {
        /// <summary>
        /// Chunk a single file into semantic code blocks
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <param name="content">File content</param>
        /// <param name="language">Programming language</param>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <returns>List of code chunks</returns>
        List<CodeChunk> ChunkFile(
            string filePath,
            string content,
            string language,
            string programId,
            string versionId);

        /// <summary>
        /// Chunk all files in a project
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of all code chunks in the project</returns>
        Task<List<CodeChunk>> ChunkProjectAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Determine optimal chunk strategy for a given language
        /// </summary>
        /// <param name="language">Programming language</param>
        /// <returns>Chunking strategy description</returns>
        string GetChunkingStrategy(string language);
    }
}
