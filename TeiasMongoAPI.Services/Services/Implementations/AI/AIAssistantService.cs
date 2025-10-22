using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TeiasMongoAPI.Services.Configuration;
using TeiasMongoAPI.Services.DTOs.Request.AI;
using TeiasMongoAPI.Services.DTOs.Response.AI;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Main AI Assistant service implementation
    /// Orchestrates context gathering, intent classification, and LLM interaction
    /// </summary>
    public class AIAssistantService : IAIAssistantService
    {
        private readonly ILLMClient _llmClient;
        private readonly IIntentClassifier _intentClassifier;
        private readonly IExecutionService _executionService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IProgramService _programService;
        private readonly IUiComponentService _uiComponentService;
        private readonly LLMOptions _llmOptions;
        private readonly ILogger<AIAssistantService> _logger;
        private readonly UIComponentContextGenerator _uiComponentContextGenerator;
        private readonly ILoggerFactory _loggerFactory;

        public AIAssistantService(
            ILLMClient llmClient,
            IIntentClassifier intentClassifier,
            IExecutionService executionService,
            IFileStorageService fileStorageService,
            IProgramService programService,
            IUiComponentService uiComponentService,
            IOptions<LLMOptions> llmOptions,
            ILogger<AIAssistantService> logger,
            ILoggerFactory loggerFactory)
        {
            _llmClient = llmClient;
            _intentClassifier = intentClassifier;
            _executionService = executionService;
            _fileStorageService = fileStorageService;
            _programService = programService;
            _uiComponentService = uiComponentService;
            _llmOptions = llmOptions.Value;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _uiComponentContextGenerator = new UIComponentContextGenerator(
                uiComponentService,
                _loggerFactory.CreateLogger<UIComponentContextGenerator>());
        }

        public async Task<AIConversationResponseDto> ConverseAsync(
            AIConversationRequestDto request,
            string userId,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Processing AI conversation request for program {ProgramId} by user {UserId}",
                    request.ProgramId, userId);

                // Step 1: Validate access to program
                var program = await _programService.GetByIdAsync(request.ProgramId, cancellationToken);
                if (program == null)
                {
                    throw new InvalidOperationException($"Program {request.ProgramId} not found");
                }

                // Step 2: Determine version to use
                var versionId = request.VersionId ?? program.CurrentVersion?.ToString();
                if (string.IsNullOrEmpty(versionId))
                {
                    throw new InvalidOperationException("No version available for this program");
                }

                // Step 3: Gather project context with token optimization
                var context = await GatherOptimizedContextAsync(
                    request.ProgramId,
                    versionId,
                    request.UserPrompt,
                    request.CurrentlyOpenFiles,
                    request.CursorContext,
                    request.Preferences,
                    cancellationToken);

                _logger.LogInformation("Gathered context: {FileCount} files, {EstimatedTokens} tokens",
                    context.FileContents.Count, context.EstimatedTokens);

                // Step 4: Build LLM prompt
                var systemPrompt = BuildSystemPrompt(context);
                var conversationMessages = BuildConversationMessages(request.ConversationHistory, request.UserPrompt);

                // Step 5: Call LLM
                var llmResponse = await _llmClient.ChatCompletionAsync(
                    systemPrompt,
                    conversationMessages,
                    request.Preferences?.Verbosity == "concise" ? 0.5 :
                    request.Preferences?.Verbosity == "detailed" ? 0.9 : null,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("LLM response received. Tokens: {TotalTokens}", llmResponse.TotalTokens);

                // Step 6: Parse LLM response
                var aiResponse = ParseLLMResponse(llmResponse.Content, context);

                // Step 7: Build updated conversation history
                var updatedHistory = BuildUpdatedHistory(request.ConversationHistory, request.UserPrompt, aiResponse);

                // Step 8: Generate suggested follow-ups
                var suggestedFollowUps = GenerateSuggestedFollowUps(context.Intent, aiResponse.FileOperations);

                stopwatch.Stop();

                return new AIConversationResponseDto
                {
                    DisplayText = aiResponse.DisplayText,
                    FileOperations = aiResponse.FileOperations,
                    ConversationHistory = updatedHistory,
                    SuggestedFollowUps = suggestedFollowUps,
                    Warnings = aiResponse.Warnings,
                    Metadata = new AIResponseMetadata
                    {
                        TokensUsed = llmResponse.TotalTokens,
                        ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                        FilesAnalyzed = context.FileContents.Keys.ToList(),
                        ConfidenceLevel = DetermineConfidenceLevel(context.Intent, aiResponse.FileOperations.Count),
                        NeedsMoreContext = context.Intent?.Confidence < 0.6
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AI conversation request");
                stopwatch.Stop();

                // Return error response
                return new AIConversationResponseDto
                {
                    DisplayText = $"I encountered an error while processing your request: {ex.Message}. Please try again or rephrase your question.",
                    FileOperations = new List<FileOperationDto>(),
                    ConversationHistory = request.ConversationHistory,
                    Warnings = new List<string> { "An error occurred during processing" },
                    Metadata = new AIResponseMetadata
                    {
                        ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                        ConfidenceLevel = "low",
                        NeedsMoreContext = true
                    }
                };
            }
        }

        public async Task<List<string>> GetSuggestedPromptsAsync(
            string programId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var program = await _programService.GetByIdAsync(programId, cancellationToken);
                if (program == null)
                {
                    return new List<string>();
                }

                var versionId = program.CurrentVersion?.ToString();
                if (string.IsNullOrEmpty(versionId))
                {
                    return GetDefaultSuggestions();
                }

                // Analyze project structure to provide context-aware suggestions
                var structure = await _executionService.AnalyzeProjectStructureAsync(programId, versionId, cancellationToken);

                return GenerateContextAwareSuggestions(structure);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting suggested prompts for program {ProgramId}", programId);
                return GetDefaultSuggestions();
            }
        }

        #region Private Helper Methods

        private async Task<ProjectContext> GatherOptimizedContextAsync(
            string programId,
            string versionId,
            string userPrompt,
            List<string>? currentlyOpenFiles,
            string? cursorContext,
            AIPreferences? preferences,
            CancellationToken cancellationToken)
        {
            // Step 1: Get project structure (low token cost ~500-1000)
            var structureDto = await _executionService.AnalyzeProjectStructureAsync(programId, versionId, cancellationToken);

            var structure = new ProjectStructureAnalysis
            {
                Language = structureDto.Language,
                ProjectType = structureDto.ProjectType,
                EntryPoints = structureDto.EntryPoints,
                SourceFiles = structureDto.SourceFiles,
                ConfigFiles = structureDto.ConfigFiles,
                Dependencies = structureDto.Dependencies,
                Metadata = structureDto.Metadata
            };

            // Step 2: Classify intent
            var intent = await _intentClassifier.ClassifyIntentAsync(
                userPrompt,
                structure,
                currentlyOpenFiles,
                cursorContext,
                cancellationToken);

            // Step 3: Determine token budget for file content
            var contextMode = preferences?.ContextMode ?? "balanced";
            var maxTokensForFiles = contextMode switch
            {
                "aggressive" => 2000,  // Minimize token usage
                "comprehensive" => 8000,  // Use more tokens for complete context
                _ => 4000  // Balanced
            };

            // Step 4: Select files within budget
            var filesToRead = _intentClassifier.SelectFilesForIntent(intent, structure, maxTokensForFiles);

            // Step 5: Read selected files
            var fileContents = new Dictionary<string, string>();
            var estimatedTokens = 1000; // Base tokens for structure

            foreach (var filePath in filesToRead.Take(preferences?.MaxFileOperations ?? 10))
            {
                try
                {
                    var fileBytes = await _fileStorageService.GetFileContentAsync(programId, versionId, filePath, cancellationToken);
                    var content = Encoding.UTF8.GetString(fileBytes);
                    fileContents[filePath] = content;
                    estimatedTokens += _llmClient.EstimateTokenCount(content);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read file {FilePath}", filePath);
                }
            }

            // Step 6: Generate virtual UIComponent module if UI components are registered
            try
            {
                var virtualUIComponent = await _uiComponentContextGenerator.GenerateVirtualUIComponentModuleAsync(
                    programId,
                    versionId,
                    cancellationToken);

                if (!string.IsNullOrEmpty(virtualUIComponent))
                {
                    fileContents["UIComponent.py (dynamically generated)"] = virtualUIComponent;
                    estimatedTokens += _llmClient.EstimateTokenCount(virtualUIComponent);
                    _logger.LogInformation("Added virtual UIComponent module to context");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate virtual UIComponent module");
            }

            return new ProjectContext
            {
                Structure = structure,
                FileContents = fileContents,
                Intent = intent,
                EstimatedTokens = estimatedTokens
            };
        }

        private string BuildSystemPrompt(ProjectContext context)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are an expert programming assistant helping developers with their code projects.");
            sb.AppendLine();
            sb.AppendLine("# PROJECT STRUCTURE");
            sb.AppendLine($"Language: {context.Structure.Language}");
            sb.AppendLine($"Project Type: {context.Structure.ProjectType}");
            sb.AppendLine($"Entry Points: {string.Join(", ", context.Structure.EntryPoints)}");
            sb.AppendLine($"Total Files: {context.Structure.SourceFiles.Count}");
            sb.AppendLine();

            if (context.Structure.Dependencies.Any())
            {
                sb.AppendLine("# DEPENDENCIES");
                sb.AppendLine(string.Join(", ", context.Structure.Dependencies.Take(20)));
                sb.AppendLine();
            }

            // Check if UIComponent module is present
            var hasUIComponents = context.FileContents.Any(f => f.Key.Contains("UIComponent"));
            if (hasUIComponents)
            {
                sb.AppendLine("# IMPORTANT: UI COMPONENTS");
                sb.AppendLine("This project uses dynamically-generated UI components:");
                sb.AppendLine("- UIComponent.py is NOT a source file - it's generated at runtime");
                sb.AppendLine("- The 'ui' object provides access to registered UI components");
                sb.AppendLine("- Imports like 'from UIComponent import ui' or 'import UIComponent' are VALID and work at runtime");
                sb.AppendLine("- DO NOT report UIComponent imports as errors");
                sb.AppendLine("- The UIComponent module structure shown below represents what will be available at runtime");
                sb.AppendLine();
            }

            if (context.FileContents.Any())
            {
                sb.AppendLine("# CURRENT FILES CONTENT");
                foreach (var (filePath, content) in context.FileContents)
                {
                    if (filePath.Contains("dynamically generated"))
                    {
                        sb.AppendLine($"## File: {filePath}");
                        sb.AppendLine("### This is a VIRTUAL file showing runtime structure - it does NOT exist in source code");
                    }
                    else
                    {
                        sb.AppendLine($"## File: {filePath}");
                    }
                    sb.AppendLine("```");
                    sb.AppendLine(content);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("# YOUR TASK");
            sb.AppendLine("When the user asks for code changes, respond with a JSON object containing:");
            sb.AppendLine("1. displayText: A conversational explanation of what you're doing");
            sb.AppendLine("2. fileOperations: An array of file operations to apply");
            sb.AppendLine();
            sb.AppendLine("RESPONSE FORMAT:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"displayText\": \"Your explanation here\",");
            sb.AppendLine("  \"fileOperations\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"operationType\": \"CREATE\" | \"UPDATE\" | \"DELETE\",");
            sb.AppendLine("      \"filePath\": \"relative/path/to/file.ext\",");
            sb.AppendLine("      \"content\": \"Full file content for CREATE/UPDATE\",");
            sb.AppendLine("      \"description\": \"What this operation does\",");
            sb.AppendLine("      \"focusLine\": 42");
            sb.AppendLine("    }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"warnings\": [\"Optional warnings\"]");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("IMPORTANT RULES:");
            sb.AppendLine("- Always respond with valid JSON in the format above");
            sb.AppendLine("- Use relative file paths from project root");
            sb.AppendLine("- Include full file content for CREATE/UPDATE operations");
            sb.AppendLine("- For questions without code changes, set fileOperations to empty array");
            sb.AppendLine("- Be concise but clear in explanations");
            sb.AppendLine("- Follow best practices for the programming language");

            return sb.ToString();
        }

        private List<LLMMessage> BuildConversationMessages(List<ConversationMessage> history, string currentPrompt)
        {
            var messages = new List<LLMMessage>();

            // Add conversation history
            foreach (var msg in history.TakeLast(_llmOptions.ConversationHistoryLimit))
            {
                messages.Add(new LLMMessage
                {
                    Role = msg.Role,
                    Content = msg.Content
                });
            }

            // Add current prompt
            messages.Add(new LLMMessage
            {
                Role = "user",
                Content = currentPrompt
            });

            return messages;
        }

        private ParsedAIResponse ParseLLMResponse(string responseContent, ProjectContext context)
        {
            try
            {
                // Try to extract JSON from the response
                var jsonMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"\{[\s\S]*\}", System.Text.RegularExpressions.RegexOptions.Multiline);
                if (jsonMatch.Success)
                {
                    var jsonContent = jsonMatch.Value;
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    };

                    var parsed = JsonSerializer.Deserialize<ParsedAIResponse>(jsonContent, options);
                    if (parsed != null && !string.IsNullOrEmpty(parsed.DisplayText))
                    {
                        return parsed;
                    }
                }

                // Fallback: treat entire response as display text
                return new ParsedAIResponse
                {
                    DisplayText = responseContent,
                    FileOperations = new List<FileOperationDto>(),
                    Warnings = new List<string>()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse LLM response as JSON, using fallback");
                return new ParsedAIResponse
                {
                    DisplayText = responseContent,
                    FileOperations = new List<FileOperationDto>(),
                    Warnings = new List<string> { "Response format was not structured as expected" }
                };
            }
        }

        private List<ConversationMessage> BuildUpdatedHistory(
            List<ConversationMessage> existingHistory,
            string userPrompt,
            ParsedAIResponse aiResponse)
        {
            var updatedHistory = existingHistory.ToList();

            // Add user message
            updatedHistory.Add(new ConversationMessage
            {
                Role = "user",
                Content = userPrompt,
                Timestamp = DateTime.UtcNow
            });

            // Add assistant message
            updatedHistory.Add(new ConversationMessage
            {
                Role = "assistant",
                Content = aiResponse.DisplayText,
                Timestamp = DateTime.UtcNow,
                FileOperations = aiResponse.FileOperations
            });

            // Trim to conversation history limit
            if (updatedHistory.Count > _llmOptions.ConversationHistoryLimit)
            {
                updatedHistory = updatedHistory.TakeLast(_llmOptions.ConversationHistoryLimit).ToList();
            }

            return updatedHistory;
        }

        private List<string> GenerateSuggestedFollowUps(UserIntent? intent, List<FileOperationDto> operations)
        {
            var suggestions = new List<string>();

            if (intent == null) return suggestions;

            switch (intent.Type)
            {
                case IntentType.FileCreation:
                    suggestions.Add("Add unit tests for the new file");
                    suggestions.Add("Import and use the new file in main entry point");
                    suggestions.Add("Add documentation comments");
                    break;

                case IntentType.BugFix:
                    suggestions.Add("Add a test case to prevent this bug");
                    suggestions.Add("Check for similar issues in other files");
                    suggestions.Add("Update documentation if needed");
                    break;

                case IntentType.FeatureAddition:
                    suggestions.Add("Create tests for the new feature");
                    suggestions.Add("Update documentation");
                    suggestions.Add("Review error handling");
                    break;

                case IntentType.Question:
                    suggestions.Add("Show me an example");
                    suggestions.Add("Explain it in simpler terms");
                    suggestions.Add("How can I modify this?");
                    break;

                default:
                    suggestions.Add("Explain what you just did");
                    suggestions.Add("Add error handling");
                    suggestions.Add("Optimize this code");
                    break;
            }

            return suggestions.Take(3).ToList();
        }

        private string DetermineConfidenceLevel(UserIntent? intent, int fileOperationCount)
        {
            if (intent == null) return "low";

            if (intent.Confidence > 0.8 && fileOperationCount <= 3)
                return "high";

            if (intent.Confidence > 0.6)
                return "medium";

            return "low";
        }

        private List<string> GetDefaultSuggestions()
        {
            return new List<string>
            {
                "Create a new utility file",
                "Fix bugs in my code",
                "Add error handling",
                "Explain how this works",
                "Refactor the code structure",
                "Add unit tests"
            };
        }

        private List<string> GenerateContextAwareSuggestions(DTOs.Response.Collaboration.ProjectStructureAnalysisDto structure)
        {
            var suggestions = new List<string>();

            // Language-specific suggestions
            if (structure.Language.ToLower() == "python")
            {
                suggestions.Add("Add type hints to functions");
                suggestions.Add("Create requirements.txt file");
            }
            else if (structure.Language.ToLower() == "csharp" || structure.Language.ToLower() == "c#")
            {
                suggestions.Add("Add XML documentation comments");
                suggestions.Add("Implement error handling with try-catch");
            }
            else if (structure.Language.ToLower() == "javascript" || structure.Language.ToLower() == "typescript")
            {
                suggestions.Add("Add JSDoc comments");
                suggestions.Add("Create package.json file");
            }

            // General suggestions
            suggestions.Add($"Explain the structure of this {structure.Language} project");
            suggestions.Add("Add a README file");
            suggestions.Add("Review code for improvements");

            return suggestions.Take(6).ToList();
        }

        #endregion

        #region Helper Classes

        private class ParsedAIResponse
        {
            public string DisplayText { get; set; } = string.Empty;
            public List<FileOperationDto> FileOperations { get; set; } = new();
            public List<string> Warnings { get; set; } = new();
        }

        #endregion
    }
}
