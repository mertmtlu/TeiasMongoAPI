using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeiasMongoAPI.Services.Configuration;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Google embedding implementation using text-embedding-004 model
    /// </summary>
    public class GoogleEmbeddingService : IEmbeddingService
    {
        private readonly VectorStoreOptions _options;
        private readonly LLMOptions _llmOptions;
        private readonly ILogger<GoogleEmbeddingService> _logger;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _apiEndpoint;

        public GoogleEmbeddingService(
            IOptions<VectorStoreOptions> options,
            IOptions<LLMOptions> llmOptions,
            ILogger<GoogleEmbeddingService> logger)
        {
            _options = options.Value;
            _llmOptions = llmOptions.Value;
            _logger = logger;

            // Use EmbeddingApiKey if provided, otherwise fall back to LLM API key
            var apiKey = !string.IsNullOrEmpty(_options.EmbeddingApiKey)
                ? _options.EmbeddingApiKey
                : _llmOptions.ApiKey;

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException(
                    "Google API key is not configured. Please set VectorStore:EmbeddingApiKey or LLM:ApiKey in configuration.");
            }

            // Use appropriate API endpoint based on model
            var isGeminiModel = _options.EmbeddingModel.StartsWith("gemini-");
            _apiEndpoint = isGeminiModel
                ? $"https://generativelanguage.googleapis.com/v1beta/models/{_options.EmbeddingModel}:batchEmbedContents?key={apiKey}"
                : $"https://generativelanguage.googleapis.com/v1beta/models/{_options.EmbeddingModel}:batchEmbedContents?key={apiKey}";
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var embeddings = await GenerateEmbeddingsAsync(new List<string> { text }, cancellationToken);
            return embeddings.First();
        }

        public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
        {
            if (texts == null || !texts.Any())
            {
                return new List<float[]>();
            }

            try
            {
                _logger.LogInformation("Generating embeddings for {Count} texts using {Model}",
                    texts.Count, _options.EmbeddingModel);

                // Google Embedding API supports batch requests
                // Format differs slightly between gemini-embedding-001 and older models
                var isGeminiModel = _options.EmbeddingModel.StartsWith("gemini-");

                var requestBody = new
                {
                    requests = texts.Select(text => new
                    {
                        model = $"models/{_options.EmbeddingModel}",
                        content = new
                        {
                            parts = new[]
                            {
                                new { text = text }
                            }
                        },
                        // For gemini-embedding-001, we can specify output dimension
                        // Supports: 128-3072, recommended: 768, 1536, 3072
                        outputDimensionality = isGeminiModel ? (int?)_options.EmbeddingDimension : null
                    }).ToArray()
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_apiEndpoint, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Google Embedding API error: {StatusCode} - {Error}",
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"Google Embedding API returned {response.StatusCode}: {errorContent}");
                }

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var embeddingResponse = JsonSerializer.Deserialize<GoogleEmbeddingResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (embeddingResponse?.Embeddings == null || embeddingResponse.Embeddings.Length == 0)
                {
                    throw new InvalidOperationException("Google Embedding API returned no embeddings");
                }

                var embeddings = embeddingResponse.Embeddings
                    .Select(e => e.Values.ToArray())
                    .ToList();

                _logger.LogInformation("Successfully generated {Count} embeddings with dimension {Dimension}",
                    embeddings.Count, embeddings.First().Length);

                return embeddings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embeddings");
                throw;
            }
        }

        public int GetEmbeddingDimension()
        {
            return _options.EmbeddingDimension;
        }

        public int GetMaxTokens()
        {
            // gemini-embedding-001 supports up to 2048 tokens
            // older models (text-embedding-004) also support 2048 tokens
            return 2048;
        }

        #region Response Models

        private class GoogleEmbeddingResponse
        {
            [JsonPropertyName("embeddings")]
            public GoogleEmbedding[] Embeddings { get; set; } = Array.Empty<GoogleEmbedding>();
        }

        private class GoogleEmbedding
        {
            [JsonPropertyName("values")]
            public List<float> Values { get; set; } = new();
        }

        #endregion
    }
}
