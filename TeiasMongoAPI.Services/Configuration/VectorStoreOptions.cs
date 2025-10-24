namespace TeiasMongoAPI.Services.Configuration
{
    /// <summary>
    /// Configuration options for vector store functionality
    /// </summary>
    public class VectorStoreSettings
    {
        /// <summary>
        /// Vector store provider (currently only "Qdrant" supported)
        /// </summary>
        public string Provider { get; set; } = "Qdrant";

        /// <summary>
        /// Qdrant-specific settings
        /// </summary>
        public QdrantSettings Qdrant { get; set; } = new();
    }

    /// <summary>
    /// Qdrant vector database configuration
    /// </summary>
    public class QdrantSettings
    {
        /// <summary>
        /// Qdrant server host (default: localhost for development)
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// HTTP/REST API port (default: 6333)
        /// </summary>
        public int Port { get; set; } = 6333;

        /// <summary>
        /// gRPC API port (default: 6334)
        /// </summary>
        public int GrpcPort { get; set; } = 6334;

        /// <summary>
        /// Optional API key for authentication (null for development)
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Whether to use TLS/SSL (false for local development)
        /// </summary>
        public bool UseTls { get; set; } = false;

        /// <summary>
        /// Prefix for collection names (e.g., "teias_code_")
        /// Final collection name: {prefix}{programId}_{versionId}
        /// </summary>
        public string CollectionPrefix { get; set; } = "teias_code_";

        /// <summary>
        /// Connection timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Embedding generation configuration
    /// </summary>
    public class EmbeddingSettings
    {
        /// <summary>
        /// Embedding provider (currently only "Gemini" supported)
        /// </summary>
        public string Provider { get; set; } = "Gemini";

        /// <summary>
        /// Gemini embedding model name
        /// Options: "text-embedding-004" (768 dims), "gemini-embedding-001" (768-3072 dims)
        /// </summary>
        public string Model { get; set; } = "text-embedding-004";

        /// <summary>
        /// Embedding vector dimensions (must match model output)
        /// text-embedding-004: 768
        /// gemini-embedding-001: 768 (default) or up to 3072
        /// </summary>
        public int Dimensions { get; set; } = 768;

        /// <summary>
        /// Maximum number of texts to embed in a single batch request
        /// </summary>
        public int MaxBatchSize { get; set; } = 100;

        /// <summary>
        /// Maximum chunk size in tokens for code chunking
        /// </summary>
        public int ChunkSize { get; set; } = 512;

        /// <summary>
        /// Token overlap between adjacent chunks for sliding window approach
        /// </summary>
        public int ChunkOverlap { get; set; } = 50;

        /// <summary>
        /// Task type for embedding generation
        /// Options: "RETRIEVAL_QUERY", "RETRIEVAL_DOCUMENT", "SEMANTIC_SIMILARITY"
        /// </summary>
        public string TaskType { get; set; } = "RETRIEVAL_DOCUMENT";

        /// <summary>
        /// Optional title prefix for document embeddings
        /// </summary>
        public string? TitlePrefix { get; set; } = "Code: ";
    }
}
