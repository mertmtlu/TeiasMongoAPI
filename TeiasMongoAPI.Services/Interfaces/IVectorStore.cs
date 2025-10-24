using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// Interface for vector database operations
    /// Abstracts away the specific vector store implementation (Qdrant, Pinecone, etc.)
    /// </summary>
    public interface IVectorStore
    {
        /// <summary>
        /// Create or update a collection for storing code embeddings
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="dimensions">Embedding vector dimensions (e.g., 768)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task CreateOrUpdateCollectionAsync(
            string programId,
            string versionId,
            int dimensions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if a collection exists for the given program/version
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if collection exists</returns>
        Task<bool> CollectionExistsAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Insert or update code chunks with their embeddings
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="chunks">Code chunks with embeddings</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task UpsertChunksAsync(
            string programId,
            string versionId,
            List<CodeChunk> chunks,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Search for similar code chunks using vector similarity
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="queryEmbedding">Query embedding vector</param>
        /// <param name="topK">Number of results to return</param>
        /// <param name="filters">Optional metadata filters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of search results with similarity scores</returns>
        Task<List<VectorSearchResult>> SearchSimilarAsync(
            string programId,
            string versionId,
            float[] queryEmbedding,
            int topK = 10,
            Dictionary<string, object>? filters = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a specific chunk by ID
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="chunkId">Chunk ID to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteChunkAsync(
            string programId,
            string versionId,
            string chunkId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete entire collection for a program version
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteCollectionAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get collection statistics (number of vectors, index status, etc.)
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection statistics</returns>
        Task<CollectionStats?> GetCollectionStatsAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the timestamp when the index was created
        /// </summary>
        /// <param name="programId">Program ID</param>
        /// <param name="versionId">Version ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Index creation timestamp, or null if not found</returns>
        Task<DateTime?> GetIndexTimestampAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Statistics about a vector collection
    /// </summary>
    public class CollectionStats
    {
        /// <summary>
        /// Number of vectors in the collection
        /// </summary>
        public long VectorCount { get; set; }

        /// <summary>
        /// Number of indexed vectors (may be less if indexing in progress)
        /// </summary>
        public long IndexedVectorCount { get; set; }

        /// <summary>
        /// Collection size in bytes
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Is the collection currently being indexed
        /// </summary>
        public bool IsIndexing { get; set; }

        /// <summary>
        /// When the collection was created
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the collection was last updated
        /// </summary>
        public DateTime LastUpdatedAt { get; set; }
    }
}
