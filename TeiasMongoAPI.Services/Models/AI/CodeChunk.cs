namespace TeiasMongoAPI.Services.Models.AI
{
    /// <summary>
    /// Represents a semantic chunk of code for embedding and vector search
    /// </summary>
    public class CodeChunk
    {
        /// <summary>
        /// Unique identifier for this chunk
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// Program ID this chunk belongs to
        /// </summary>
        public required string ProgramId { get; set; }

        /// <summary>
        /// Version ID this chunk belongs to
        /// </summary>
        public required string VersionId { get; set; }

        /// <summary>
        /// File path where this chunk originates
        /// </summary>
        public required string FilePath { get; set; }

        /// <summary>
        /// Type of chunk
        /// </summary>
        public required ChunkType Type { get; set; }

        /// <summary>
        /// Name/identifier of the chunk (e.g., function name, class name)
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The actual code content
        /// </summary>
        public required string Content { get; set; }

        /// <summary>
        /// Start line number in the original file
        /// </summary>
        public int StartLine { get; set; }

        /// <summary>
        /// End line number in the original file
        /// </summary>
        public int EndLine { get; set; }

        /// <summary>
        /// Programming language
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Parent context (e.g., class name for a method)
        /// </summary>
        public string? ParentContext { get; set; }

        /// <summary>
        /// Embedding vector for this chunk
        /// Will be populated after embedding generation
        /// </summary>
        public float[]? Embedding { get; set; }

        /// <summary>
        /// Embedding model used to generate the vector
        /// Example: "gemini-embedding-001", "text-embedding-004"
        /// </summary>
        public string? EmbeddingModel { get; set; }

        /// <summary>
        /// Embedding dimension (e.g., 768, 1536, 3072)
        /// </summary>
        public int? EmbeddingDimension { get; set; }

        /// <summary>
        /// Additional metadata about the chunk
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// When this chunk was created/indexed
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Hash of the content for detecting changes
        /// </summary>
        public string? ContentHash { get; set; }
    }

    /// <summary>
    /// Types of code chunks
    /// </summary>
    public enum ChunkType
    {
        /// <summary>
        /// Complete function/method with implementation
        /// </summary>
        Function,

        /// <summary>
        /// Complete class definition
        /// </summary>
        Class,

        /// <summary>
        /// Interface definition
        /// </summary>
        Interface,

        /// <summary>
        /// Module or namespace block
        /// </summary>
        Module,

        /// <summary>
        /// Struct definition
        /// </summary>
        Struct,

        /// <summary>
        /// Enum definition
        /// </summary>
        Enum,

        /// <summary>
        /// Configuration file content
        /// </summary>
        Config,

        /// <summary>
        /// Documentation or comments
        /// </summary>
        Documentation,

        /// <summary>
        /// Generic code block
        /// </summary>
        CodeBlock,

        /// <summary>
        /// Import/using statements
        /// </summary>
        Imports,

        /// <summary>
        /// Other/Unknown
        /// </summary>
        Other
    }

    /// <summary>
    /// Result from a vector search query
    /// </summary>
    public class VectorSearchResult
    {
        /// <summary>
        /// The code chunk that was found
        /// </summary>
        public required CodeChunk Chunk { get; set; }

        /// <summary>
        /// Similarity score (0.0 to 1.0)
        /// Higher is more similar
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Rank in the result set (1-based)
        /// </summary>
        public int Rank { get; set; }
    }
}
