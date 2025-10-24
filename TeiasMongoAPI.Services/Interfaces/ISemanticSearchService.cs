using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// High-level service for semantic code search
    /// Orchestrates chunking, embedding, storing, and searching code
    /// </summary>
    public interface ISemanticSearchService
    {
        /// <summary>
        /// Index an entire project for semantic search
        /// Chunks code, generates embeddings, and stores in vector database
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="forceReindex">If true, delete existing index and recreate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Indexing result with statistics</returns>
        Task<IndexingResult> IndexProjectAsync(
            string programId,
            string versionId,
            bool forceReindex = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Search for code using natural language query
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="query">Natural language query (e.g., "find database queries")</param>
        /// <param name="topK">Number of results to return</param>
        /// <param name="filters">Optional filters (e.g., file type, language)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns">List of matching code chunks with similarity scores</returns>
        Task<List<VectorSearchResult>> SearchCodeAsync(
            string programId,
            string versionId,
            string query,
            int topK = 10,
            Dictionary<string, object>? filters = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Find code similar to a specific chunk
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="chunkId">Chunk ID to find similar code to</param>
        /// <param name="topK">Number of results to return</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of similar code chunks</returns>
        Task<List<VectorSearchResult>> FindSimilarCodeAsync(
            string programId,
            string versionId,
            string chunkId,
            int topK = 10,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if a project version is indexed
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if indexed</returns>
        Task<bool> IsProjectIndexedAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete index for a project version
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteProjectIndexAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get index statistics for a project version
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Index statistics</returns>
        Task<IndexStats?> GetIndexStatsAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Result of an indexing operation
    /// </summary>
    public class IndexingResult
    {
        /// <summary>
        /// Whether indexing was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Number of files processed
        /// </summary>
        public int FilesProcessed { get; set; }

        /// <summary>
        /// Number of chunks created
        /// </summary>
        public int ChunksCreated { get; set; }

        /// <summary>
        /// Number of embeddings generated
        /// </summary>
        public int EmbeddingsGenerated { get; set; }

        /// <summary>
        /// Time taken to index
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Warnings encountered during indexing
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Statistics about an indexed project
    /// </summary>
    public class IndexStats
    {
        /// <summary>
        /// Program ID
        /// </summary>
        public string ProgramId { get; set; } = string.Empty;

        /// <summary>
        /// Version ID
        /// </summary>
        public string VersionId { get; set; } = string.Empty;

        /// <summary>
        /// Total number of chunks
        /// </summary>
        public long TotalChunks { get; set; }

        /// <summary>
        /// Number of indexed chunks
        /// </summary>
        public long IndexedChunks { get; set; }

        /// <summary>
        /// When the index was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the index was last updated
        /// </summary>
        public DateTime LastUpdatedAt { get; set; }

        /// <summary>
        /// Is indexing currently in progress
        /// </summary>
        public bool IsIndexing { get; set; }

        /// <summary>
        /// Embedding model used
        /// </summary>
        public string EmbeddingModel { get; set; } = string.Empty;

        /// <summary>
        /// Embedding dimensions
        /// </summary>
        public int EmbeddingDimensions { get; set; }
    }
}
