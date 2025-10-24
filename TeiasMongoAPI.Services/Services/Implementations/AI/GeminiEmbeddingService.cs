using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TeiasMongoAPI.Services.Configuration;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Embedding service using Google Gemini API (direct HTTP calls)
    /// Supports text-embedding-004 (768 dims) and gemini-embedding-001 (768-3072 dims)
    /// </summary>
    public class GeminiEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly EmbeddingSettings _settings;
        private readonly ILogger<GeminiEmbeddingService> _logger;
        private readonly string _modelName;
        private readonly string _apiKey;
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

        public GeminiEmbeddingService(
            IOptions<EmbeddingSettings> settings,
            IOptions<LLMOptions> llmOptions,
            ILogger<GeminiEmbeddingService> logger)
        {
            _settings = settings.Value;
            _logger = logger;

            // Use API key from environment or LLM settings
            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                ?? llmOptions.Value.ApiKey;

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Gemini API key not configured. Set GEMINI_API_KEY environment variable.");
            }

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _apiKey);
            _modelName = _settings.Model;

            _logger.LogInformation("Initialized GeminiEmbeddingService with model: {Model}, dimensions: {Dimensions}",
                _modelName, _settings.Dimensions);
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("Attempted to generate embedding for empty text");
                    return new float[_settings.Dimensions];
                }

                _logger.LogDebug("Generating embedding for text of length {Length}", text.Length);

                // Use the embedContent method from Gemini API
                var request = new
                {
                    model = $"models/{_modelName}",
                    content = new { parts = new[] { new { text } } }
                };

                var jsonContent = JsonSerializer.Serialize(request);
                var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                var apiUrl = $"{BaseUrl}/{_modelName}:embedContent?key={_apiKey}";
                var response = await _httpClient.PostAsync(apiUrl, httpContent, cancellationToken);

                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var embeddingResponse = JsonSerializer.Deserialize<EmbeddingResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (embeddingResponse?.Embedding?.Values == null || embeddingResponse.Embedding.Values.Length == 0)
                {
                    _logger.LogError("Empty embedding response from Gemini API");
                    throw new InvalidOperationException("Received empty embedding from Gemini API");
                }

                var embedding = embeddingResponse.Embedding.Values;

                _logger.LogDebug("Successfully generated embedding with {Dimensions} dimensions", embedding.Length);

                return embedding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding for text: {TextPreview}",
                    text.Length > 100 ? text.Substring(0, 100) + "..." : text);
                throw;
            }
        }

        public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
        {
            try
            {
                if (texts == null || texts.Count == 0)
                {
                    _logger.LogWarning("Attempted to generate embeddings for empty text list");
                    return new List<float[]>();
                }

                _logger.LogInformation("Generating {Count} embeddings in batch", texts.Count);

                // Filter out empty texts
                var validTexts = texts.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
                if (validTexts.Count != texts.Count)
                {
                    _logger.LogWarning("Filtered out {Count} empty texts", texts.Count - validTexts.Count);
                }

                // Process in batches to respect API limits
                var results = new List<float[]>();
                var batchSize = _settings.MaxBatchSize;

                for (int i = 0; i < validTexts.Count; i += batchSize)
                {
                    var batch = validTexts.Skip(i).Take(batchSize).ToList();
                    _logger.LogDebug("Processing batch {Start}-{End} of {Total}",
                        i + 1, Math.Min(i + batchSize, validTexts.Count), validTexts.Count);

                    // Generate embeddings for batch
                    var batchResults = await GenerateBatchEmbeddingsInternal(batch, cancellationToken);
                    results.AddRange(batchResults);

                    // Small delay between batches to avoid rate limiting
                    if (i + batchSize < validTexts.Count)
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }

                _logger.LogInformation("Successfully generated {Count} embeddings", results.Count);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate batch embeddings for {Count} texts", texts.Count);
                throw;
            }
        }

        public int GetEmbeddingDimensions()
        {
            return _settings.Dimensions;
        }

        public string GetModelName()
        {
            return _modelName;
        }

        private async Task<List<float[]>> GenerateBatchEmbeddingsInternal(
            List<string> texts,
            CancellationToken cancellationToken)
        {
            var embeddings = new List<float[]>();

            // Note: Gemini embedding API doesn't support true batch processing yet
            // Process sequentially with small delays to avoid rate limiting
            foreach (var text in texts)
            {
                var embedding = await GenerateEmbeddingAsync(text, cancellationToken);
                embeddings.Add(embedding);
            }

            return embeddings;
        }
    }

    /// <summary>
    /// Response structure for Gemini embedContent API
    /// </summary>
    internal class EmbeddingResponse
    {
        public EmbeddingData? Embedding { get; set; }
    }

    internal class EmbeddingData
    {
        public float[] Values { get; set; } = Array.Empty<float>();
    }
}
