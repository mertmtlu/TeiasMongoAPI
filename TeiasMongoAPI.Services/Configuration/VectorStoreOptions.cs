namespace TeiasMongoAPI.Services.Configuration
{
    /// <summary>
    /// Configuration options for vector store (Qdrant) and embeddings
    /// </summary>
    public class VectorStoreOptions
    {
        /// <summary>
        /// Configuration section name in appsettings.json
        /// </summary>
        public const string SectionName = "VectorStore";

        /// <summary>
        /// Vector store provider: "Qdrant", "MongoDB", "InMemory"
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Qdrant server URL
        /// Default: http://localhost:6333
        /// </summary>
        public string QdrantUrl { get; set; } = string.Empty;

        /// <summary>
        /// Qdrant API key (if authentication is enabled)
        /// </summary>
        public string? QdrantApiKey { get; set; }

        /// <summary>
        /// Collection name prefix for storing code embeddings
        /// Actual collection name will be: {CollectionPrefix}_{programId}
        /// </summary>
        public string CollectionPrefix { get; set; } = string.Empty;

        /// <summary>
        /// Embedding provider: "Google", "OpenAI", "Local"
        /// </summary>
        public string EmbeddingProvider { get; set; } = string.Empty;

        /// <summary>
        /// Embedding model to use
        /// Google: "gemini-embedding-001" (768/1536/3072 dimensions, recommended)
        ///         "text-embedding-004" (768 dimensions, DEPRECATED - will be discontinued Nov 2025)
        /// OpenAI: "text-embedding-3-small" (1536 dimensions)
        ///         "text-embedding-3-large" (3072 dimensions)
        /// </summary>
        public string EmbeddingModel { get; set; } = string.Empty;

        /// <summary>
        /// Embedding vector dimension
        /// Must match the model's output dimension
        /// gemini-embedding-001: 768, 1536, or 3072 (recommended: 768 for balance, 3072 for quality)
        /// text-embedding-004: 768 only
        /// OpenAI text-embedding-3-small: 1536
        /// OpenAI text-embedding-3-large: 3072
        /// </summary>
        public int EmbeddingDimension { get; set; }

        /// <summary>
        /// API key for embedding provider
        /// For Google: use same API key as Gemini (from LLM config)
        /// For OpenAI: separate API key
        /// </summary>
        public string? EmbeddingApiKey { get; set; }

        /// <summary>
        /// Number of results to retrieve from vector search
        /// </summary>
        public int TopK { get; set; }

        /// <summary>
        /// Minimum similarity score threshold (0.0 to 1.0)
        /// Results below this threshold will be filtered out
        /// </summary>
        public double MinimumSimilarityScore { get; set; }

        /// <summary>
        /// Whether to enable vector search
        /// If false, only uses code indexing without semantic search
        /// </summary>
        public bool EnableVectorSearch { get; set; }

        /// <summary>
        /// Whether to automatically index new versions
        /// </summary>
        public bool AutoIndexVersions { get; set; }

        /// <summary>
        /// Batch size for embedding generation
        /// </summary>
        public int EmbeddingBatchSize { get; set; }

        /// <summary>
        /// Timeout for vector search queries in seconds
        /// </summary>
        public int QueryTimeoutSeconds { get; set; }
    }
}
