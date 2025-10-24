namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// Service for generating text embeddings using AI models
    /// </summary>
    public interface IEmbeddingService
    {
        /// <summary>
        /// Generate an embedding vector for a single text
        /// </summary>
        /// <param name="text">Text to embed</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Embedding vector (array of floats)</returns>
        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate embedding vectors for multiple texts in a single batch request
        /// More efficient than calling GenerateEmbeddingAsync multiple times
        /// </summary>
        /// <param name="texts">Texts to embed</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of embedding vectors in the same order as input texts</returns>
        Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the dimensionality of embeddings produced by this service
        /// </summary>
        /// <returns>Number of dimensions (e.g., 768 for text-embedding-004)</returns>
        int GetEmbeddingDimensions();

        /// <summary>
        /// Get the name of the model being used for embeddings
        /// </summary>
        /// <returns>Model name (e.g., "text-embedding-004")</returns>
        string GetModelName();
    }
}
