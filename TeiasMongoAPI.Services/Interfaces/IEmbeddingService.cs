namespace TeiasMongoAPI.Services.Interfaces
{
    /// <summary>
    /// Service for generating embeddings from text using LLM providers
    /// </summary>
    public interface IEmbeddingService
    {
        /// <summary>
        /// Generate embedding vector for a single text input
        /// </summary>
        /// <param name="text">Text to embed</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Embedding vector (dimensions depend on model)</returns>
        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate embeddings for multiple texts in a single batch
        /// More efficient than calling GenerateEmbeddingAsync multiple times
        /// </summary>
        /// <param name="texts">Texts to embed</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of embedding vectors in the same order as input texts</returns>
        Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the dimension of embeddings produced by this service
        /// </summary>
        /// <returns>Embedding dimension (e.g., 768 for Google, 1536 for OpenAI)</returns>
        int GetEmbeddingDimension();

        /// <summary>
        /// Get the maximum number of tokens that can be embedded at once
        /// </summary>
        /// <returns>Maximum token limit</returns>
        int GetMaxTokens();
    }
}
