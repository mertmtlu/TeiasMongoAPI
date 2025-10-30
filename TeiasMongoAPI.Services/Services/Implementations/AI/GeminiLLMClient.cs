using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System.Net;
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
        private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;

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

            // Configure retry policy for transient HTTP errors
            _retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = _options.MaxRetryAttempts,
                    Delay = TimeSpan.FromSeconds(_options.RetryDelaySeconds),
                    BackoffType = DelayBackoffType.Constant,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .HandleResult(response =>
                        {
                            // Retry on these transient HTTP status codes
                            return response.StatusCode == HttpStatusCode.ServiceUnavailable || // 503
                                   response.StatusCode == HttpStatusCode.TooManyRequests ||    // 429
                                   response.StatusCode == HttpStatusCode.RequestTimeout ||      // 408
                                   response.StatusCode == HttpStatusCode.InternalServerError || // 500
                                   response.StatusCode == HttpStatusCode.BadGateway ||          // 502
                                   response.StatusCode == HttpStatusCode.GatewayTimeout;        // 504
                        }),
                    OnRetry = args =>
                    {
                        var statusCode = args.Outcome.Result?.StatusCode.ToString() ?? "Exception";
                        _logger.LogWarning(
                            "Retrying Gemini API call. Attempt {AttemptNumber} of {MaxAttempts}. Status: {StatusCode}. Waiting {Delay}ms before retry.",
                            args.AttemptNumber + 1,
                            _options.MaxRetryAttempts + 1,
                            statusCode,
                            args.RetryDelay.TotalMilliseconds);
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
        }

        public async Task<LLMResponse> ChatCompletionAsync(
            string systemPrompt,
            List<LLMMessage> messages,
            double? temperature = null,
            int? maxTokens = null,
            object? responseSchema = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Sending chat completion request to Gemini API with {MessageCount} messages (Structured: {IsStructured})",
                    messages.Count, responseSchema != null);

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

                // Prepare generation config
                var generationConfig = new Dictionary<string, object>
                {
                    { "temperature", temperature ?? _options.Temperature },
                    { "maxOutputTokens", maxTokens ?? _options.MaxResponseTokens }
                };

                // Add structured output configuration if schema is provided
                if (responseSchema != null)
                {
                    generationConfig["responseMimeType"] = "application/json";
                    generationConfig["responseSchema"] = responseSchema;
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
                    generationConfig = generationConfig
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Send request with retry logic
                var response = await _retryPipeline.ExecuteAsync(
                    async ct => await _httpClient.PostAsync(_apiEndpoint, content, ct),
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (geminiResponse == null)
                {
                    throw new InvalidOperationException("Failed to deserialize Gemini API response");
                }

                // Check for prompt-level safety blocks first
                if (geminiResponse.PromptFeedback?.BlockReason != null)
                {
                    var blockReason = geminiResponse.PromptFeedback.BlockReason;
                    var safetyInfo = FormatSafetyRatings(geminiResponse.PromptFeedback.SafetyRatings);

                    _logger.LogWarning(
                        "Gemini API blocked the prompt. BlockReason: {BlockReason}. Safety Ratings: {SafetyRatings}",
                        blockReason,
                        safetyInfo);

                    return new LLMResponse
                    {
                        Content = $"I'm unable to process this request as it was blocked due to safety concerns. Reason: {blockReason}",
                        TotalTokens = geminiResponse.UsageMetadata?.TotalTokenCount ?? 0,
                        PromptTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0,
                        CompletionTokens = 0,
                        Model = _options.Model,
                        FinishReason = "SAFETY",
                        IsBlocked = true,
                        BlockReason = blockReason
                    };
                }

                // Check if we have candidates
                if (geminiResponse.Candidates == null || geminiResponse.Candidates.Length == 0)
                {
                    // No candidates but no block reason - likely empty response
                    _logger.LogWarning("Gemini API returned no candidates and no explicit block reason");

                    return new LLMResponse
                    {
                        Content = "I'm unable to generate a response for this request. Please try rephrasing your question.",
                        TotalTokens = geminiResponse.UsageMetadata?.TotalTokenCount ?? 0,
                        PromptTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0,
                        CompletionTokens = 0,
                        Model = _options.Model,
                        FinishReason = "OTHER",
                        IsBlocked = true,
                        BlockReason = "NO_CANDIDATES"
                    };
                }

                var candidate = geminiResponse.Candidates[0];

                // Check for candidate-level safety blocks
                if (candidate.FinishReason == "SAFETY")
                {
                    var safetyInfo = FormatSafetyRatings(candidate.SafetyRatings);

                    _logger.LogWarning(
                        "Gemini API blocked the response. FinishReason: SAFETY. Safety Ratings: {SafetyRatings}",
                        safetyInfo);

                    return new LLMResponse
                    {
                        Content = "I'm unable to complete this response as it was blocked due to safety concerns.",
                        TotalTokens = geminiResponse.UsageMetadata?.TotalTokenCount ?? 0,
                        PromptTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0,
                        CompletionTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0,
                        Model = _options.Model,
                        FinishReason = "SAFETY",
                        IsBlocked = true,
                        BlockReason = "SAFETY_RATINGS"
                    };
                }

                // Check if content exists and has parts
                if (candidate.Content?.Parts == null || candidate.Content.Parts.Length == 0)
                {
                    _logger.LogWarning(
                        "Gemini API returned candidate with no content. FinishReason: {FinishReason}",
                        candidate.FinishReason ?? "UNKNOWN");

                    return new LLMResponse
                    {
                        Content = "I'm unable to generate a response. The API returned no content.",
                        TotalTokens = geminiResponse.UsageMetadata?.TotalTokenCount ?? 0,
                        PromptTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0,
                        CompletionTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0,
                        Model = _options.Model,
                        FinishReason = candidate.FinishReason ?? "OTHER",
                        IsBlocked = true,
                        BlockReason = "NO_CONTENT"
                    };
                }

                // Successfully got content - extract it
                var responseText = candidate.Content.Parts.FirstOrDefault()?.Text ?? string.Empty;

                // Extract token usage
                var promptTokens = geminiResponse.UsageMetadata?.PromptTokenCount ?? 0;
                var completionTokens = geminiResponse.UsageMetadata?.CandidatesTokenCount ?? 0;
                var totalTokens = geminiResponse.UsageMetadata?.TotalTokenCount ?? (promptTokens + completionTokens);

                _logger.LogInformation("Gemini API response received successfully. Tokens - Prompt: {PromptTokens}, Completion: {CompletionTokens}, Total: {TotalTokens}",
                    promptTokens, completionTokens, totalTokens);

                return new LLMResponse
                {
                    Content = responseText.Trim(),
                    TotalTokens = totalTokens,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    Model = _options.Model,
                    FinishReason = candidate.FinishReason ?? "STOP",
                    IsBlocked = false
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

        /// <summary>
        /// Formats safety ratings array into a readable string for logging
        /// </summary>
        private string FormatSafetyRatings(GeminiSafetyRating[]? safetyRatings)
        {
            if (safetyRatings == null || safetyRatings.Length == 0)
            {
                return "No safety ratings available";
            }

            var ratings = safetyRatings
                .Select(r => $"{r.Category}: {r.Probability}" + (r.Blocked == true ? " (BLOCKED)" : ""))
                .ToArray();

            return string.Join(", ", ratings);
        }

        #region API Response Models

        private class GeminiApiResponse
        {
            [JsonPropertyName("candidates")]
            public GeminiCandidate[]? Candidates { get; set; }

            [JsonPropertyName("usageMetadata")]
            public GeminiUsageMetadata? UsageMetadata { get; set; }

            [JsonPropertyName("promptFeedback")]
            public GeminiPromptFeedback? PromptFeedback { get; set; }
        }

        private class GeminiPromptFeedback
        {
            [JsonPropertyName("blockReason")]
            public string? BlockReason { get; set; }

            [JsonPropertyName("safetyRatings")]
            public GeminiSafetyRating[]? SafetyRatings { get; set; }
        }

        private class GeminiCandidate
        {
            [JsonPropertyName("content")]
            public GeminiContent? Content { get; set; }

            [JsonPropertyName("finishReason")]
            public string? FinishReason { get; set; }

            [JsonPropertyName("safetyRatings")]
            public GeminiSafetyRating[]? SafetyRatings { get; set; }
        }

        private class GeminiSafetyRating
        {
            [JsonPropertyName("category")]
            public string? Category { get; set; }

            [JsonPropertyName("probability")]
            public string? Probability { get; set; }

            [JsonPropertyName("blocked")]
            public bool? Blocked { get; set; }
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
