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
        private readonly ISemanticSearchService? _semanticSearchService;
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
            ICodeIndexer codeIndexer,
            IOptions<LLMOptions> llmOptions,
            ILogger<AIAssistantService> logger,
            ILoggerFactory loggerFactory,
            ISemanticSearchService? semanticSearchService = null)
        {
            _llmClient = llmClient;
            _intentClassifier = intentClassifier;
            _executionService = executionService;
            _fileStorageService = fileStorageService;
            _programService = programService;
            _uiComponentService = uiComponentService;
            _codeIndexer = codeIndexer;
            _semanticSearchService = semanticSearchService;
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
                var systemPrompt = BuildSystemPrompt(context, request.CurrentlyOpenFiles);
                var conversationMessages = BuildConversationMessages(request.ConversationHistory, request.UserPrompt);

                // Step 5: Call LLM with structured output schema
                var responseSchema = GeminiSchemaBuilder.BuildAIAssistantResponseSchema();
                var llmResponse = await _llmClient.ChatCompletionAsync(
                    systemPrompt,
                    conversationMessages,
                    request.Preferences?.Verbosity == "concise" ? 0.5 :
                    request.Preferences?.Verbosity == "detailed" ? 0.9 : null,
                    responseSchema: responseSchema,
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
                // Use AI-specific method to work with unapproved/draft versions
                var structure = await _executionService.AnalyzeProjectStructureForAIAsync(programId, versionId, cancellationToken);

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
            // Use AI-specific method to work with unapproved/draft versions
            var structureDto = await _executionService.AnalyzeProjectStructureForAIAsync(programId, versionId, cancellationToken);

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

            // Step 2.5: Perform semantic search if enabled and available
            List<VectorSearchResult>? semanticSearchResults = null;
            if (preferences?.UseSemanticSearch == true && _semanticSearchService != null)
            {
                try
                {
                    // Check if project is indexed
                    var isIndexed = await _semanticSearchService.IsProjectIndexedAsync(programId, versionId, cancellationToken);

                    if (!isIndexed)
                    {
                        _logger.LogInformation("Project not indexed yet. Attempting auto-indexing for {ProgramId}/{VersionId}",
                            programId, versionId);

                        // Auto-index the project in the background (don't block the conversation)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var indexResult = await _semanticSearchService.IndexProjectAsync(
                                    programId, versionId, forceReindex: false, CancellationToken.None);

                                if (indexResult.Success)
                                {
                                    _logger.LogInformation("Auto-indexing completed: {Chunks} chunks in {Duration}",
                                        indexResult.ChunksCreated, indexResult.Duration);
                                }
                                else
                                {
                                    _logger.LogWarning("Auto-indexing failed: {Error}", indexResult.ErrorMessage);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Auto-indexing background task failed");
                            }
                        }, CancellationToken.None);

                        _logger.LogInformation("Auto-indexing started in background. Using file-based context for this conversation.");
                    }
                    else
                    {
                        // Project is indexed - check for staleness before searching
                        var shouldReindex = false;
                        var indexTimestamp = await _semanticSearchService.GetIndexStatsAsync(programId, versionId, cancellationToken);

                        if (indexTimestamp != null)
                        {
                            // Check if files were modified after indexing
                            var modifiedFiles = await _fileStorageService.GetFilesModifiedSinceAsync(
                                programId, versionId, indexTimestamp.CreatedAt, cancellationToken);

                            if (modifiedFiles.Any())
                            {
                                _logger.LogWarning("Detected {Count} modified files since index creation at {IndexTime}. Index is stale!",
                                    modifiedFiles.Count, indexTimestamp.CreatedAt);

                                shouldReindex = true;

                                // Trigger re-indexing in background
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        _logger.LogInformation("Starting re-indexing due to stale index for {ProgramId}/{VersionId}",
                                            programId, versionId);

                                        var indexResult = await _semanticSearchService.IndexProjectAsync(
                                            programId, versionId, forceReindex: true, CancellationToken.None);

                                        if (indexResult.Success)
                                        {
                                            _logger.LogInformation("Stale index re-indexed successfully: {Chunks} chunks in {Duration}",
                                                indexResult.ChunksCreated, indexResult.Duration);
                                        }
                                        else
                                        {
                                            _logger.LogWarning("Re-indexing failed: {Error}", indexResult.ErrorMessage);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Re-indexing background task failed");
                                    }
                                }, CancellationToken.None);

                                _logger.LogInformation("Re-indexing started in background. Using current (stale) index for this conversation.");
                            }
                            else
                            {
                                _logger.LogDebug("Index is up-to-date (no files modified since {IndexTime})",
                                    indexTimestamp.CreatedAt);
                            }
                        }

                        // Perform semantic search (using current index, even if stale)
                        _logger.LogInformation("Performing semantic search for query: '{Query}'", userPrompt);
                        var topK = preferences.SemanticSearchTopK > 0 ? preferences.SemanticSearchTopK : 10;
                        semanticSearchResults = await _semanticSearchService.SearchCodeAsync(
                            programId, versionId, userPrompt, topK, cancellationToken: cancellationToken);

                        _logger.LogInformation("Semantic search found {Count} relevant chunks{StaleWarning}",
                            semanticSearchResults.Count, shouldReindex ? " (index is stale, re-indexing in background)" : "");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Semantic search failed, falling back to file-based context");
                }
            }

            // Step 3: Determine token budget for file content
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

            // Step 4: Select files within budget (using intent-based selection only)
            var filesToRead = _intentClassifier.SelectFilesForIntent(intent, structure, maxTokensForFiles);

            // Step 4.5: Add explicitly mentioned files from user prompt
            var allProjectFiles = structure.SourceFiles
                .Concat(structure.ConfigFiles)
                .Concat(structure.BinaryFiles)
                .Concat(structure.BinaryFiles)
                .ToList();

            foreach (var projectFile in allProjectFiles)
            {
                var fileName = Path.GetFileName(projectFile);
                var fileExtension = Path.GetExtension(projectFile);
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(projectFile);

                // Check if user explicitly mentioned this file in their prompt
                // Match full filename, extension, or filename without extension
                var isExplicitlyMentioned =
                    userPrompt.Contains(fileName, StringComparison.OrdinalIgnoreCase) ||  // "TestDll.sln"
                    userPrompt.Contains(fileExtension, StringComparison.OrdinalIgnoreCase) ||  // ".sln"
                    userPrompt.Contains(fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase) ||  // "TestDll"
                    userPrompt.Contains(projectFile, StringComparison.OrdinalIgnoreCase);  // Full path

                if (isExplicitlyMentioned && !filesToRead.Contains(projectFile))
                {
                    filesToRead.Add(projectFile);
                    _logger.LogInformation("Added explicitly mentioned file: {FileName}", projectFile);
                }
            }

            _logger.LogInformation("File selection: {TotalFiles} files selected based on intent analysis", filesToRead.Count);

            // Step 5: Read selected files (prioritizing live content from frontend)
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

            // Step 6: Add UI Component generator documentation to system prompt
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

            // Step 7: Generate virtual UIComponent module if UI components are registered
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
                SemanticSearchResults = semanticSearchResults,
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
            sb.AppendLine($"Source Files: {context.Structure.SourceFiles.Count}");
            sb.AppendLine($"Config Files: {context.Structure.ConfigFiles.Count}");
            sb.AppendLine($"Binary/Other Files: {context.Structure.BinaryFiles.Count} (non-text-readable: images, DLLs, executables, etc.)");
            sb.AppendLine($"Total Files: {context.Structure.SourceFiles.Count + context.Structure.ConfigFiles.Count + context.Structure.BinaryFiles.Count}");
            sb.AppendLine();

            // List binary files so AI knows they exist
            if (context.Structure.BinaryFiles.Any())
            {
                sb.AppendLine("# NON-TEXT-READABLE FILES (Binary/Media/Executables)");
                sb.AppendLine("These files exist in the project but cannot be read as text:");
                foreach (var binaryFile in context.Structure.BinaryFiles.Take(50)) // Limit to 50
                {
                    sb.AppendLine($"  - {binaryFile}");
                }
                if (context.Structure.BinaryFiles.Count > 50)
                {
                    sb.AppendLine($"  ... and {context.Structure.BinaryFiles.Count - 50} more binary files");
                }
                sb.AppendLine();
            }

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

            // Include semantic search results if available
            if (context.SemanticSearchResults != null && context.SemanticSearchResults.Any())
            {
                sb.AppendLine("# SEMANTIC SEARCH RESULTS");
                sb.AppendLine($"Found {context.SemanticSearchResults.Count} semantically relevant code chunks for your query:");
                sb.AppendLine();

                foreach (var result in context.SemanticSearchResults.Take(10)) // Limit to top 10 for prompt
                {
                    var chunk = result.Chunk;
                    sb.AppendLine($"## {chunk.FilePath} (Relevance: {result.Score:F2})");
                    sb.AppendLine($"**{chunk.Type}**: {chunk.Name} (Lines {chunk.StartLine}-{chunk.EndLine})");
                    if (!string.IsNullOrEmpty(chunk.ParentContext))
                    {
                        sb.AppendLine($"**Context**: {chunk.ParentContext}");
                    }
                    sb.AppendLine("```");
                    sb.AppendLine(chunk.Content);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
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
            sb.AppendLine("You are helping developers write and modify code. Your response will be structured automatically.");
            sb.AppendLine();
            sb.AppendLine("When responding:");
            sb.AppendLine("1. Provide a clear explanation in 'displayText' of what you're doing or answering");
            sb.AppendLine("2. For code changes, include 'fileOperations' with the operations to apply:");
            sb.AppendLine("   - CREATE: Create a new file with content");
            sb.AppendLine("   - UPDATE: Modify an existing file with new content");
            sb.AppendLine("   - DELETE: Remove a file");
            sb.AppendLine("3. For questions without code changes, leave fileOperations empty");
            sb.AppendLine("4. Add optional warnings if there are potential issues");
            sb.AppendLine();
            sb.AppendLine("IMPORTANT RULES:");
            sb.AppendLine("- Use relative file paths from project root");
            sb.AppendLine("- Include FULL file content for CREATE/UPDATE operations (not just snippets)");
            sb.AppendLine("- Be concise but clear in explanations");
            sb.AppendLine("- Follow best practices for the programming language");
            sb.AppendLine("- If you need to modify a file, always include the complete updated content");

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
                // With structured output, the response should be valid JSON directly
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };

                var parsed = JsonSerializer.Deserialize<ParsedAIResponse>(responseContent, options);
                if (parsed != null && !string.IsNullOrEmpty(parsed.DisplayText))
                {
                    _logger.LogInformation("Successfully parsed structured response with {FileOpCount} file operations",
                        parsed.FileOperations?.Count ?? 0);
                    return parsed;
                }

                // Fallback: treat entire response as display text (shouldn't happen with structured output)
                _logger.LogWarning("Structured output returned null or empty displayText, using fallback");
                return new ParsedAIResponse
                {
                    DisplayText = responseContent,
                    FileOperations = new List<FileOperationDto>(),
                    Warnings = new List<string>()
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse structured JSON response: {Response}", responseContent);

                // Fallback: treat entire response as display text
                return new ParsedAIResponse
                {
                    DisplayText = responseContent,
                    FileOperations = new List<FileOperationDto>(),
                    Warnings = new List<string> { "Response format was not structured as expected. This may indicate an API issue." }
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
