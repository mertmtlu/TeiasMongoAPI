using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TeiasMongoAPI.Services.Configuration;
using TeiasMongoAPI.Services.DTOs.Request.AI;
using TeiasMongoAPI.Services.DTOs.Response.AI;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Evaluates AI-generated responses for quality and appropriateness
    /// </summary>
    public class ResponseEvaluator : IResponseEvaluator
    {
        private readonly ILLMClient _llmClient;
        private readonly LLMOptions _options;
        private readonly ILogger<ResponseEvaluator> _logger;

        public ResponseEvaluator(
            ILLMClient llmClient,
            IOptions<LLMOptions> options,
            ILogger<ResponseEvaluator> logger)
        {
            _llmClient = llmClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<ResponseEvaluationResult> EvaluateResponseAsync(
            string userPrompt,
            AIConversationResponseDto aiResponse,
            string? context = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ResponseEvaluationResult
            {
                IsAcceptable = true,
                ConfidenceScore = 1.0,
                Issues = new List<string>()
            };

            try
            {
                // Check 1: Completeness - ensure response is not too short
                if (!CheckCompleteness(aiResponse, result))
                {
                    _logger.LogWarning("Response failed completeness check. Length: {Length}", aiResponse.DisplayText?.Length ?? 0);
                }

                // Check 2: File operations validation (if any)
                if (!ValidateFileOperations(aiResponse, result))
                {
                    _logger.LogWarning("Response has invalid file operations");
                }

                // Check 3: LLM-based quality evaluation
                if (!await EvaluateWithLLMAsync(userPrompt, aiResponse, context, result, cancellationToken))
                {
                    _logger.LogWarning("Response failed LLM quality evaluation");
                }

                // Determine overall acceptability based on all checks
                result.IsAcceptable = result.ConfidenceScore >= 0.6 && !result.IsTooShort;

                _logger.LogInformation(
                    "Response evaluation complete. Acceptable: {IsAcceptable}, Score: {Score}, Issues: {IssueCount}",
                    result.IsAcceptable,
                    result.ConfidenceScore,
                    result.Issues.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during response evaluation. Accepting response by default.");
                // In case of evaluation error, accept the response but with lower confidence
                result.IsAcceptable = true;
                result.ConfidenceScore = 0.5;
                result.Issues.Add($"Evaluation error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Check if the response is long enough and complete
        /// </summary>
        private bool CheckCompleteness(AIConversationResponseDto aiResponse, ResponseEvaluationResult result)
        {
            var displayText = aiResponse.DisplayText ?? string.Empty;

            if (displayText.Length < _options.MinimumResponseLength)
            {
                result.IsTooShort = true;
                result.Issues.Add($"Response is too short ({displayText.Length} chars, minimum {_options.MinimumResponseLength})");
                result.ConfidenceScore = Math.Min(result.ConfidenceScore, 0.4);
                return false;
            }

            // Check for common truncation indicators
            if (displayText.TrimEnd().EndsWith("...") || displayText.TrimEnd().EndsWith("…"))
            {
                result.IsTooShort = true;
                result.Issues.Add("Response appears to be truncated (ends with ellipsis)");
                result.ConfidenceScore = Math.Min(result.ConfidenceScore, 0.5);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validate that file operations (if any) make sense
        /// </summary>
        private bool ValidateFileOperations(AIConversationResponseDto aiResponse, ResponseEvaluationResult result)
        {
            if (aiResponse.FileOperations == null || aiResponse.FileOperations.Count == 0)
            {
                return true; // No file operations to validate
            }

            var issues = new List<string>();

            foreach (var fileOp in aiResponse.FileOperations)
            {
                // Check for empty or null paths
                if (string.IsNullOrWhiteSpace(fileOp.FilePath))
                {
                    issues.Add("File operation has empty file path");
                    continue;
                }

                // Check for suspicious paths
                if (fileOp.FilePath.Contains("..") || fileOp.FilePath.Contains("~"))
                {
                    issues.Add($"Suspicious file path: {fileOp.FilePath}");
                }

                // Check for operations with empty content where content is expected
                if ((fileOp.OperationType == FileOperationType.CREATE || fileOp.OperationType == FileOperationType.UPDATE) &&
                    string.IsNullOrWhiteSpace(fileOp.Content))
                {
                    issues.Add($"File operation '{fileOp.OperationType}' for {fileOp.FilePath} has no content");
                }
            }

            if (issues.Count > 0)
            {
                result.AreFileOperationsValid = false;
                result.Issues.AddRange(issues);
                result.ConfidenceScore = Math.Min(result.ConfidenceScore, 0.6);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Use LLM to evaluate response quality
        /// </summary>
        private async Task<bool> EvaluateWithLLMAsync(
            string userPrompt,
            AIConversationResponseDto aiResponse,
            string? context,
            ResponseEvaluationResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting LLM-based evaluation for response of length {Length}",
                    aiResponse.DisplayText?.Length ?? 0);

                var evaluationPrompt = BuildEvaluationPrompt(userPrompt, aiResponse, context);

                _logger.LogDebug("Evaluation prompt length: {Length} characters", evaluationPrompt.Length);

                var messages = new List<LLMMessage>
                {
                    new LLMMessage
                    {
                        Role = "user",
                        Content = evaluationPrompt
                    }
                };

                // Build response schema for structured output
                var evaluationSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        adequate = new
                        {
                            type = "boolean",
                            description = "Whether the response adequately addresses the user's request"
                        },
                        score = new
                        {
                            type = "number",
                            description = "Confidence score from 0.0 to 1.0"
                        },
                        issues = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "string"
                            },
                            description = "List of issues found in the response"
                        },
                        reason = new
                        {
                            type = "string",
                            description = "Brief explanation of the evaluation"
                        }
                    },
                    required = new[] { "adequate", "score", "issues", "reason" }
                };

                _logger.LogInformation("Calling LLM for evaluation...");

                // Add a small delay to avoid rate limiting on free tier
                // Gemini free tier has strict per-minute limits
                await Task.Delay(1000, cancellationToken); // 1 second delay

                // Use a lower temperature for more consistent evaluation
                var llmResponse = await _llmClient.ChatCompletionAsync(
                    systemPrompt: "You are a quality evaluator for AI-generated code assistance responses. Analyze responses objectively.",
                    messages: messages,
                    temperature: 0.3,
                    maxTokens: 3000,
                    responseSchema: evaluationSchema,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("LLM evaluation call completed. Response tokens: {Tokens}, Content length: {Length}",
                    llmResponse.TotalTokens,
                    llmResponse.Content?.Length ?? 0);

                // Log the response for debugging
                if (string.IsNullOrEmpty(llmResponse.Content))
                {
                    _logger.LogError("LLM evaluation returned empty content. FinishReason: {FinishReason}, Model: {Model}",
                        llmResponse.FinishReason,
                        llmResponse.Model);

                    // Empty response is a critical issue - mark as failed
                    result.Issues.Add("LLM evaluation failed - empty response from API");
                    result.ConfidenceScore = Math.Min(result.ConfidenceScore, 0.5);
                    return true; // Still accept to avoid blocking user, but with reduced confidence
                }

                _logger.LogDebug("LLM evaluation response content: {Response}",
                    llmResponse.Content.Length > 500 ? llmResponse.Content.Substring(0, 500) + "..." : llmResponse.Content);

                // Parse evaluation response
                return ParseEvaluationResponse(llmResponse.Content, result);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("503") || ex.Message.Contains("Service Unavailable"))
            {
                _logger.LogWarning(ex, "LLM evaluation failed due to 503 Service Unavailable - Gemini free tier likely rate limited");
                result.Issues.Add("Evaluation skipped - API rate limit");
                result.ConfidenceScore = Math.Min(result.ConfidenceScore, 0.7);
                return true; // Accept response but note the issue
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform LLM-based evaluation. Exception type: {Type}, Message: {Message}",
                    ex.GetType().Name,
                    ex.Message);

                // Don't fail the entire evaluation if LLM evaluation fails
                result.Issues.Add($"Evaluation error: {ex.Message}");
                result.ConfidenceScore = Math.Min(result.ConfidenceScore, 0.6);
                return true;
            }
        }

        /// <summary>
        /// Build the prompt for LLM-based evaluation
        /// </summary>
        private string BuildEvaluationPrompt(string userPrompt, AIConversationResponseDto aiResponse, string? context)
        {
            var prompt = $@"Evaluate the following AI assistant response for quality and appropriateness.

USER'S REQUEST:
{userPrompt}

AI'S RESPONSE:
{aiResponse.DisplayText}

{(string.IsNullOrEmpty(context) ? "" : $@"ADDITIONAL CONTEXT:
{context}

")}EVALUATION CRITERIA:
1. Does the response adequately address the user's request?
2. Is the response clear and well-structured?
3. Are there any obvious errors or inconsistencies?
4. Is the level of detail appropriate?

Respond with a JSON object containing:
{{
  ""adequate"": true/false,
  ""score"": 0.0-1.0,
  ""issues"": [""list of issues""],
  ""reason"": ""brief explanation""
}}

Be concise and objective.";

            return prompt;
        }

        /// <summary>
        /// Parse the LLM evaluation response
        /// </summary>
        private bool ParseEvaluationResponse(string llmResponseContent, ResponseEvaluationResult result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(llmResponseContent))
                {
                    _logger.LogWarning("LLM evaluation response is empty or whitespace");
                    return true;
                }

                // With structured output, the response should be pure JSON
                // Try direct deserialization first
                try
                {
                    var evaluation = JsonSerializer.Deserialize<LLMEvaluationResponse>(llmResponseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (evaluation != null)
                    {
                        result.ConfidenceScore = Math.Min(result.ConfidenceScore, evaluation.Score);

                        if (evaluation.Issues != null && evaluation.Issues.Count > 0)
                        {
                            result.Issues.AddRange(evaluation.Issues);
                        }

                        result.EvaluationReason = evaluation.Reason;

                        _logger.LogInformation("Evaluation parsed successfully. Adequate: {Adequate}, Score: {Score}",
                            evaluation.Adequate, evaluation.Score);

                        return evaluation.Adequate;
                    }
                }
                catch (JsonException)
                {
                    // If direct deserialization fails, try extracting JSON
                    _logger.LogDebug("Direct JSON deserialization failed, trying to extract JSON from response");
                }

                // Fallback: Try to extract JSON from the response
                var jsonStart = llmResponseContent.IndexOf('{');
                var jsonEnd = llmResponseContent.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonContent = llmResponseContent.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var evaluation = JsonSerializer.Deserialize<LLMEvaluationResponse>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (evaluation != null)
                    {
                        result.ConfidenceScore = Math.Min(result.ConfidenceScore, evaluation.Score);

                        if (evaluation.Issues != null && evaluation.Issues.Count > 0)
                        {
                            result.Issues.AddRange(evaluation.Issues);
                        }

                        result.EvaluationReason = evaluation.Reason;

                        _logger.LogInformation("Evaluation parsed successfully (extracted). Adequate: {Adequate}, Score: {Score}",
                            evaluation.Adequate, evaluation.Score);

                        return evaluation.Adequate;
                    }
                }

                // If parsing fails, assume response is acceptable
                _logger.LogWarning("Could not parse LLM evaluation response. Content length: {Length}, Content preview: {Preview}",
                    llmResponseContent.Length,
                    llmResponseContent.Length > 100 ? llmResponseContent.Substring(0, 100) : llmResponseContent);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing LLM evaluation response. Content: {Content}",
                    llmResponseContent?.Length > 200 ? llmResponseContent.Substring(0, 200) + "..." : llmResponseContent);
                return true;
            }
        }

        #region Helper Classes

        private class LLMEvaluationResponse
        {
            public bool Adequate { get; set; }
            public double Score { get; set; }
            public List<string> Issues { get; set; } = new();
            public string? Reason { get; set; }
        }

        #endregion
    }
}
