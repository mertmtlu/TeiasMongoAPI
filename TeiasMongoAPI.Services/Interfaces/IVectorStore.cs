using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// Service for storing and querying code embeddings in a vector database
    /// </summary>
    public interface IVectorStore
    {
        /// <summary>
        /// Initialize collection for a program if it doesn't exist
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="dimension">Embedding vector dimension</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task EnsureCollectionExistsAsync(string programId, int dimension, CancellationToken cancellationToken = default);

        /// <summary>
        /// Store a single code chunk with its embedding
        /// </summary>
        /// <param name="chunk">Code chunk with embedding</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task UpsertChunkAsync(CodeChunk chunk, CancellationToken cancellationToken = default);

        /// <summary>
        /// Store multiple code chunks in batch
        /// More efficient than calling UpsertChunkAsync multiple times
        /// </summary>
        /// <param name="chunks">Code chunks with embeddings</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task UpsertChunksAsync(List<CodeChunk> chunks, CancellationToken cancellationToken = default);

        /// <summary>
        /// Search for similar code chunks using vector similarity
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="queryEmbedding">Query embedding vector</param>
        /// <param name="topK">Number of results to return</param>
        /// <param name="minimumScore">Minimum similarity score threshold</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of similar code chunks with scores</returns>
        Task<List<VectorSearchResult>> SearchSimilarAsync(
            string programId,
            string versionId,
            float[] queryEmbedding,
            int topK = 20,
            double minimumScore = 0.5,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete all chunks for a specific version
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteVersionAsync(string programId, string versionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete entire collection for a program
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteCollectionAsync(string programId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get total number of chunks indexed for a program version
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of indexed chunks</returns>
        Task<int> GetChunkCountAsync(string programId, string versionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if the vector store is healthy and reachable
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if healthy, false otherwise</returns>
        Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
    }
}
