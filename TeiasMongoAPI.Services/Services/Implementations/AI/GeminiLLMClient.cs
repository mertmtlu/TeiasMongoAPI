using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeiasMongoAPI.Services.Configuration;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Google Gemini implementation of ILLMClient using REST API
    /// </summary>
    public class GeminiLLMClient : ILLMClient
    {
        private readonly LLMOptions _options;
        private readonly ILogger<GeminiLLMClient> _logger;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly string _apiEndpoint;

        public string ModelName => _options.Model;

        public GeminiLLMClient(
            IOptions<LLMOptions> options,
            ILogger<GeminiLLMClient> logger)
        {
            _options = options.Value;
            _logger = logger;

            if (string.IsNullOrEmpty(_options.ApiKey))
            {
                throw new InvalidOperationException("Gemini API key is not configured. Please set GEMINI_API_KEY in environment variables.");
            }

            _apiEndpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";
        }

        public async Task<LLMResponse> ChatCompletionAsync(
            string systemPrompt,
            List<LLMMessage> messages,
            double? temperature = null,
            int? maxTokens = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Sending chat completion request to Gemini API with {MessageCount} messages", messages.Count);

                // Build the prompt combining system prompt and conversation
                var fullPrompt = new StringBuilder();

                if (!string.IsNullOrEmpty(systemPrompt))
                {
                    fullPrompt.AppendLine(systemPrompt);
                    fullPrompt.AppendLine();
                }

                foreach (var message in messages)
                {
                    fullPrompt.AppendLine($"{(message.Role == "user" ? "User" : "Assistant")}: {message.Content}");
                }

                // Prepare request body
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = fullPrompt.ToString() }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = temperature ?? _options.Temperature,
                        maxOutputTokens = maxTokens ?? _options.MaxResponseTokens
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Send request
                var response = await _httpClient.PostAsync(_apiEndpoint, content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (geminiResponse?.Candidates == null || geminiResponse.Candidates.Length == 0)
                {
                    throw new InvalidOperationException("No response received from Gemini API");
                }

                var candidate = geminiResponse.Candidates[0];
                var responseText = candidate.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;

                // Extract token usage
                var promptTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0;
                var completionTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0;
                var totalTokens = geminiResponse.UsageMetadata?.TotalTokenCount ?? (promptTokens + completionTokens);

                _logger.LogInformation("Gemini API response received. Tokens - Prompt: {PromptTokens}, Completion: {CompletionTokens}, Total: {TotalTokens}",
                    promptTokens, completionTokens, totalTokens);

                return new LLMResponse
                {
                    Content = responseText.Trim(),
                    TotalTokens = totalTokens,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    Model = _options.Model,
                    FinishReason = candidate.FinishReason ?? "STOP"
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling Gemini API");
                throw new InvalidOperationException($"Failed to call Gemini API: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini API");
                throw new InvalidOperationException($"Failed to get response from Gemini API: {ex.Message}", ex);
            }
        }

        public int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Rough estimation: ~4 characters per token for English text
            // This is a simplified estimation; actual token count may vary
            return (int)Math.Ceiling(text.Length / 4.0);
        }

        #region API Response Models

        private class GeminiApiResponse
        {
            [JsonPropertyName("candidates")]
            public GeminiCandidate[]? Candidates { get; set; }

            [JsonPropertyName("usageMetadata")]
            public GeminiUsageMetadata? UsageMetadata { get; set; }
        }

        private class GeminiCandidate
        {
            [JsonPropertyName("content")]
            public GeminiContent? Content { get; set; }

            [JsonPropertyName("finishReason")]
            public string? FinishReason { get; set; }
        }

        private class GeminiContent
        {
            [JsonPropertyName("parts")]
            public GeminiPart[]? Parts { get; set; }
        }

        private class GeminiPart
        {
            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }

        private class GeminiUsageMetadata
        {
            [JsonPropertyName("promptTokenCount")]
            public int PromptTokenCount { get; set; }

            [JsonPropertyName("candidatesTokenCount")]
            public int CandidatesTokenCount { get; set; }

            [JsonPropertyName("totalTokenCount")]
            public int TotalTokenCount { get; set; }
        }

        #endregion
    }
}
