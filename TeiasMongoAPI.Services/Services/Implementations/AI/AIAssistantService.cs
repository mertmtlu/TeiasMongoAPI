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
        private readonly ICodeIndexer _codeIndexer;
        private readonly IVectorStore? _vectorStore;
        private readonly IEmbeddingService? _embeddingService;
        private readonly LLMOptions _llmOptions;
        private readonly VectorStoreOptions _vectorStoreOptions;
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
            ICodeIndexer codeIndexer,
            IOptions<LLMOptions> llmOptions,
            IOptions<VectorStoreOptions> vectorStoreOptions,
            ILogger<AIAssistantService> logger,
            ILoggerFactory loggerFactory,
            IVectorStore? vectorStore = null,
            IEmbeddingService? embeddingService = null)
        {
            _llmClient = llmClient;
            _intentClassifier = intentClassifier;
            _executionService = executionService;
            _fileStorageService = fileStorageService;
            _programService = programService;
            _uiComponentService = uiComponentService;
            _codeIndexer = codeIndexer;
            _vectorStore = vectorStore;
            _embeddingService = embeddingService;
            _llmOptions = llmOptions.Value;
            _vectorStoreOptions = vectorStoreOptions.Value;
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
                var systemPrompt = BuildSystemPrompt(context, request.CurrentlyOpenFiles);
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
            List<OpenFileContext>? currentlyOpenFiles,
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

            // Step 1.5: Generate project index (all symbols, no implementations)
            ProjectIndex? projectIndex = null;
            try
            {
                projectIndex = await _codeIndexer.GenerateProjectIndexAsync(programId, versionId, cancellationToken);
                _logger.LogInformation("Project index generated: {TotalSymbols} symbols, ~{Tokens} tokens",
                    projectIndex.TotalSymbols, projectIndex.EstimatedTokens);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate project index, continuing without it");
            }

            // Step 2: Classify intent
            var openFilePaths = currentlyOpenFiles?.Select(f => f.FilePath).ToList();
            var intent = await _intentClassifier.ClassifyIntentAsync(
                userPrompt,
                structure,
                openFilePaths,
                cursorContext,
                cancellationToken);

            // Step 3: Semantic search using vector store (if enabled)
            var vectorSearchFiles = new List<string>();
            if (_vectorStore != null && _embeddingService != null && _vectorStoreOptions.EnableVectorSearch)
            {
                try
                {
                    // Generate embedding for user prompt
                    var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(userPrompt, cancellationToken);

                    // Search for semantically similar code chunks
                    var searchResults = await _vectorStore.SearchSimilarAsync(
                        programId,
                        versionId,
                        queryEmbedding,
                        _vectorStoreOptions.TopK,
                        _vectorStoreOptions.MinimumSimilarityScore,
                        cancellationToken);

                    // Extract unique file paths from search results
                    vectorSearchFiles = searchResults
                        .Select(r => r.Chunk.FilePath)
                        .Distinct()
                        .ToList();

                    _logger.LogInformation("Vector search found {ResultCount} relevant chunks across {FileCount} files",
                        searchResults.Count, vectorSearchFiles.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Vector search failed, continuing with intent-based selection only");
                }
            }

            // Step 4: Determine token budget for file content
            // With Gemini 2.0 Flash's 100K context window, we can be much more generous
            var contextMode = preferences?.ContextMode ?? "balanced";
            var maxTokensForFiles = contextMode switch
            {
                "aggressive" => 15000,     // Minimize token usage but still include 20-25 files
                "balanced" => 40000,       // Balanced - can include 50-60 files
                "comprehensive" => 70000,  // Use most of context for complete picture
                "unlimited" => 90000,      // Try to send everything (leave 10K for response)
                _ => 40000                 // Default to balanced
            };

            // Step 5: Select files within budget (combining intent-based and vector search)
            var intentBasedFiles = _intentClassifier.SelectFilesForIntent(intent, structure, maxTokensForFiles);
            var filesToRead = intentBasedFiles.Union(vectorSearchFiles).Distinct().ToList();

            _logger.LogInformation("File selection: {IntentFiles} from intent, {VectorFiles} from vector search, {TotalFiles} total unique files",
                intentBasedFiles.Count, vectorSearchFiles.Count, filesToRead.Count);

            // Step 6: Read selected files (prioritizing live content from frontend)
            var fileContents = new Dictionary<string, string>();
            var estimatedTokens = 1000; // Base tokens for structure

            // Determine max files to read based on context mode
            var maxFilesToRead = contextMode == "unlimited"
                ? int.MaxValue  // Try to read all files
                : (preferences?.MaxFileOperations ?? 100);  // Increased default from 10 to 100

            var filesProcessed = 0;
            var filesSkipped = 0;

            foreach (var filePath in filesToRead)
            {
                // Stop if we've exceeded the token budget (leave room for response)
                if (estimatedTokens > maxTokensForFiles)
                {
                    filesSkipped++;
                    _logger.LogDebug("Skipping {FilePath} - token budget exceeded ({Current} > {Max})",
                        filePath, estimatedTokens, maxTokensForFiles);
                    continue;
                }

                // Stop if we've hit the max file limit
                if (filesProcessed >= maxFilesToRead)
                {
                    filesSkipped++;
                    _logger.LogDebug("Skipping {FilePath} - max files limit reached", filePath);
                    continue;
                }

                try
                {
                    // Check if this file is open in the editor with live content
                    var openFile = currentlyOpenFiles?.FirstOrDefault(f =>
                        f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase) ||
                        filePath.EndsWith(f.FilePath, StringComparison.OrdinalIgnoreCase));

                    string content;

                    if (openFile?.Content != null)
                    {
                        // Use live content from editor (handles unsaved changes)
                        content = openFile.Content;
                        _logger.LogInformation("Using live content from editor for {FilePath} (unsaved: {HasUnsaved})",
                            filePath, openFile.HasUnsavedChanges);
                    }
                    else
                    {
                        // Read from storage
                        var fileBytes = await _fileStorageService.GetFileContentAsync(programId, versionId, filePath, cancellationToken);
                        content = Encoding.UTF8.GetString(fileBytes);
                    }

                    fileContents[filePath] = content;
                    estimatedTokens += _llmClient.EstimateTokenCount(content);
                    filesProcessed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read file {FilePath}", filePath);
                }
            }

            if (filesSkipped > 0)
            {
                _logger.LogInformation("Context gathering complete: {Included} files included, {Skipped} files skipped due to budget constraints",
                    filesProcessed, filesSkipped);
            }

            // Step 7: Add UI Component generator documentation to system prompt
            // Include info about how UI components work in the system
            var uiComponentSystemInfo = @"
# SYSTEM: UI Component Generators

The TeiasMongoAPI system includes automatic UI component code generation:

**UIComponentPythonGenerator.cs**: Generates Python code for UI components
- Creates Python classes with properties, methods, and event handlers
- Includes DTO type definitions for structured data
- Supports form elements, buttons, inputs, tables, charts

**UIComponentCSharpGenerator.cs**: Generates C# code for UI components
- Creates C# classes with properties, methods, and event handlers
- Includes DTO class definitions for structured data
- Supports form elements, buttons, inputs, tables, charts

When users work with UI components in their projects:
- They can reference 'UIComponent' module in Python or C#
- The ui object provides access to registered components
- Components are dynamically generated at runtime
- Import statements like 'from UIComponent import ui' are valid

If the AI needs to help users with UI components, it should:
1. Understand these generators create the component code
2. Know that UIComponent imports are runtime-generated (not source files)
3. Help users use the 'ui' object to access their registered components
";

            fileContents["[SYSTEM] UI Component System Documentation"] = uiComponentSystemInfo;
            estimatedTokens += _llmClient.EstimateTokenCount(uiComponentSystemInfo);

            // Step 8: Generate virtual UIComponent module if UI components are registered
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
                Index = projectIndex,
                FileContents = fileContents,
                Intent = intent,
                EstimatedTokens = estimatedTokens
            };
        }

        private string BuildSystemPrompt(ProjectContext context, List<OpenFileContext>? openFiles = null)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are an expert programming assistant helping developers with their code projects.");
            sb.AppendLine();

            // Show info about currently open files and unsaved changes
            if (openFiles != null && openFiles.Any())
            {
                var filesWithUnsavedChanges = openFiles.Where(f => f.HasUnsavedChanges).ToList();
                if (filesWithUnsavedChanges.Any())
                {
                    sb.AppendLine("# IMPORTANT: UNSAVED CHANGES");
                    sb.AppendLine("The following files have UNSAVED changes in the editor. The content shown below reflects the CURRENT state in the editor:");
                    foreach (var file in filesWithUnsavedChanges)
                    {
                        sb.AppendLine($"- {file.FilePath} (unsaved)");
                    }
                    sb.AppendLine();
                }

                var focusedFile = openFiles.FirstOrDefault(f => f.IsFocused);
                if (focusedFile != null)
                {
                    sb.AppendLine("# USER FOCUS");
                    sb.AppendLine($"Currently focused file: {focusedFile.FilePath}");
                    if (focusedFile.CursorLine.HasValue)
                    {
                        sb.AppendLine($"Cursor position: Line {focusedFile.CursorLine}, Column {focusedFile.CursorColumn}");
                    }
                    if (!string.IsNullOrEmpty(focusedFile.SelectedRange))
                    {
                        sb.AppendLine($"Selected range: {focusedFile.SelectedRange}");
                    }
                    sb.AppendLine();
                }
            }

            // Include project index if available (shows all symbols in codebase)
            if (context.Index != null)
            {
                sb.AppendLine("# COMPLETE PROJECT INDEX");
                sb.AppendLine("Below is a complete index of ALL files and symbols in the project.");
                sb.AppendLine("This shows you the entire codebase structure without implementations.");
                sb.AppendLine("You can reference ANY of these symbols even if the full file content is not included below.");
                sb.AppendLine();
                sb.AppendLine(_codeIndexer.FormatIndexForPrompt(context.Index));
                sb.AppendLine();
            }

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
