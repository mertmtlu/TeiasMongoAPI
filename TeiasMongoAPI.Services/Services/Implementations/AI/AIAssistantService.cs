using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.Configuration;
using TeiasMongoAPI.Services.DTOs.Request.AI;
using TeiasMongoAPI.Services.DTOs.Response.AI;
using TeiasMongoAPI.Services.Helpers;
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
        private readonly IResponseEvaluator _responseEvaluator;
        private readonly LLMOptions _llmOptions;
        private readonly ILogger<AIAssistantService> _logger;
        private readonly UIComponentContextGenerator _uiComponentContextGenerator;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IUnitOfWork _unitOfWork;

        public AIAssistantService(
            ILLMClient llmClient,
            IIntentClassifier intentClassifier,
            IExecutionService executionService,
            IFileStorageService fileStorageService,
            IProgramService programService,
            IUiComponentService uiComponentService,
            ICodeIndexer codeIndexer,
            IResponseEvaluator responseEvaluator,
            IOptions<LLMOptions> llmOptions,
            ILogger<AIAssistantService> logger,
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            ISemanticSearchService? semanticSearchService = null
            )
        {
            _llmClient = llmClient;
            _intentClassifier = intentClassifier;
            _executionService = executionService;
            _fileStorageService = fileStorageService;
            _programService = programService;
            _uiComponentService = uiComponentService;
            _codeIndexer = codeIndexer;
            _semanticSearchService = semanticSearchService;
            _responseEvaluator = responseEvaluator;
            _llmOptions = llmOptions.Value;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _uiComponentContextGenerator = new UIComponentContextGenerator(
                uiComponentService,
                _loggerFactory.CreateLogger<UIComponentContextGenerator>());
            _unitOfWork = unitOfWork;
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

                // Step 5: Call LLM with evaluation and retry logic
                var (aiResponse, totalTokens, retryCount, evaluationResult) = await GetAIResponseWithEvaluationAsync(
                    systemPrompt,
                    conversationMessages,
                    context,
                    request,
                    cancellationToken);

                _logger.LogInformation("Final response received. Tokens: {TotalTokens}, Retries: {RetryCount}",
                    totalTokens, retryCount);

                // Step 6: Build updated conversation history
                var updatedHistory = BuildUpdatedHistory(request.ConversationHistory, request.UserPrompt, aiResponse);

                // Step 7: Use AI-generated follow-ups (fallback to template-based if none provided)
                var suggestedFollowUps = aiResponse.SuggestedFollowUps?.Any() == true
                    ? aiResponse.SuggestedFollowUps
                    : GenerateSuggestedFollowUps(context.Intent, aiResponse.FileOperations);

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
                        TokensUsed = totalTokens,
                        ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds,
                        FilesAnalyzed = context.FileContents.Keys.ToList(),
                        ConfidenceLevel = DetermineConfidenceLevel(context.Intent, aiResponse.FileOperations.Count),
                        NeedsMoreContext = context.Intent?.Confidence < 0.6,
                        EvaluationScore = evaluationResult?.ConfidenceScore,
                        EvaluationFailed = evaluationResult?.IsAcceptable == false,
                        RetryCount = retryCount,
                        EvaluationIssues = evaluationResult?.Issues?.Count > 0 ? evaluationResult.Issues : null
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

        /// <summary>
        /// Gets AI response with evaluation and retry logic
        /// </summary>
        private async Task<(ParsedAIResponse aiResponse, int totalTokens, int retryCount, ResponseEvaluationResult? evaluationResult)>
            GetAIResponseWithEvaluationAsync(
                string systemPrompt,
                List<LLMMessage> conversationMessages,
                ProjectContext context,
                AIConversationRequestDto request,
                CancellationToken cancellationToken)
        {
            var responseSchema = GeminiSchemaBuilder.BuildAIAssistantResponseSchema();
            var baseTemperature = request.Preferences?.Verbosity == "concise" ? 0.5 :
                                  request.Preferences?.Verbosity == "detailed" ? 0.9 :
                                  _llmOptions.Temperature;

            int retryCount = 0;
            int maxEvaluationRetries = _llmOptions.EnableResponseEvaluation ? _llmOptions.MaxEvaluationRetries : 0;
            ResponseEvaluationResult? lastEvaluationResult = null;
            ParsedAIResponse? bestResponse = null;
            int bestResponseTokens = 0;
            double bestScore = 0.0;

            for (int attempt = 0; attempt <= maxEvaluationRetries; attempt++)
            {
                // Adjust temperature for retries
                var currentTemperature = baseTemperature + (attempt * _llmOptions.EvaluationRetryTemperatureIncrement);

                // Adjust system prompt for retries
                var currentSystemPrompt = systemPrompt;
                if (attempt > 0)
                {
                    currentSystemPrompt += "\n\nIMPORTANT: Please provide a more detailed and comprehensive response that fully addresses the user's request.";
                    _logger.LogInformation("Retry attempt {Attempt} with adjusted temperature {Temperature}", attempt, currentTemperature);
                }

                // Call LLM
                var llmResponse = await _llmClient.ChatCompletionAsync(
                    currentSystemPrompt,
                    conversationMessages,
                    currentTemperature,
                    responseSchema: responseSchema,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("LLM response received. Tokens: {TotalTokens}", llmResponse.TotalTokens);

                if (llmResponse.IsBlocked)
                {
                    _logger.LogWarning("LLM response was blocked. Reason: {BlockReason}. Aborting evaluation and returning friendly message.", llmResponse.BlockReason);
                    
                    // Create a ParsedAIResponse with the user-friendly error message from the client
                    var blockedResponse = new ParsedAIResponse
                    {
                        DisplayText = llmResponse.Content, // This contains the safe error message like "I'm unable to..."
                        FileOperations = new List<FileOperationDto>(),
                        Warnings = new List<string> { $"Request was blocked by the API. Reason: {llmResponse.BlockReason}" }
                    };

                    // Return immediately from the function. There is no need to retry a blocked request.
                    return (blockedResponse, llmResponse.TotalTokens, retryCount, null);
                }

                // Parse response
                var aiResponse = ParseLLMResponse(llmResponse.Content, context);

                // Evaluate response if enabled
                ResponseEvaluationResult? evaluationResult = null;
                if (_llmOptions.EnableResponseEvaluation)
                {
                    var responseDto = new AIConversationResponseDto
                    {
                        DisplayText = aiResponse.DisplayText,
                        FileOperations = aiResponse.FileOperations,
                        Warnings = aiResponse.Warnings
                    };

                    evaluationResult = await _responseEvaluator.EvaluateResponseAsync(
                        request.UserPrompt,
                        responseDto,
                        context: $"Project: {context.Structure?.Language} - Intent: {context.Intent?.Type}",
                        cancellationToken: cancellationToken);

                    lastEvaluationResult = evaluationResult;

                    // Track best response
                    if (evaluationResult.ConfidenceScore > bestScore)
                    {
                        bestScore = evaluationResult.ConfidenceScore;
                        bestResponse = aiResponse;
                        bestResponseTokens = llmResponse.TotalTokens;
                    }

                    // If evaluation passes, return immediately
                    if (evaluationResult.IsAcceptable)
                    {
                        _logger.LogInformation("Response evaluation passed on attempt {Attempt}. Score: {Score}",
                            attempt, evaluationResult.ConfidenceScore);
                        return (aiResponse, llmResponse.TotalTokens, retryCount, evaluationResult);
                    }

                    // If not acceptable and we have retries left, continue
                    if (attempt < maxEvaluationRetries)
                    {
                        retryCount++;
                        _logger.LogWarning("Response evaluation failed on attempt {Attempt}. Score: {Score}. Issues: {Issues}. Retrying...",
                            attempt, evaluationResult.ConfidenceScore, string.Join(", ", evaluationResult.Issues));
                        continue;
                    }
                }
                else
                {
                    // Evaluation disabled, return immediately
                    return (aiResponse, llmResponse.TotalTokens, 0, null);
                }
            }

            // All retries exhausted, return best response we got
            _logger.LogWarning("All evaluation retries exhausted. Returning best response with score: {Score}", bestScore);
            return (bestResponse ?? ParseLLMResponse("{}", context), bestResponseTokens, retryCount, lastEvaluationResult);
        }

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
                BinaryFiles = structureDto.BinaryFiles,
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

            // Step 4: Select files within budget using new scope-based selection
            var filesToRead = await _intentClassifier.SelectFilesBasedOnScopeAsync(
                intent,
                structure,
                programId,
                versionId,
                maxTokensForFiles,
                preferences?.UseSemanticSearch ?? false,
                cancellationToken);

            // Step 4.5: Add explicitly mentioned files from user prompt
            var allProjectFiles = structure.SourceFiles
                .Concat(structure.ConfigFiles)
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

            // Step 6: Add UI Component generator documentation to system prompt based on intent
            // Include info about how UI components work in the system
            // Let the LLM decide via intent classification whether UI components are needed
            if (intent?.IncludeUIComponents == true)
            {
                var latestComponent = await _unitOfWork.UiComponents.GetLatestActiveByProgramAsync(ObjectId.Parse(programId), cancellationToken);

                if (latestComponent != null)
                {
                    var uiComponentSystemInfo = GetUIModule(programId, structure.Language, latestComponent, cancellationToken);

                    fileContents["[SYSTEM] UI Component System Documentation"] = uiComponentSystemInfo;
                    estimatedTokens += _llmClient.EstimateTokenCount(uiComponentSystemInfo);
                    _logger.LogInformation("Added UI Component system documentation (LLM decided: intent={IntentType})", intent.Type);
                }
                else
                {
                    _logger.LogDebug("Intent requested UI components but none found for program {ProgramId}", programId);
                }
            }
            else
            {
                _logger.LogDebug("Skipping UI Component documentation (LLM decision: IncludeUIComponents={Include})", intent?.IncludeUIComponents);
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

        private string GetUIModule(string programId, string language, UiComponent latestComponent, CancellationToken cancellationToken)
        {
            switch (language)
            {
                case "python":
                    return UIComponentPythonGenerator.GenerateUIComponentPython(latestComponent);
                case "C#":
                    return UIComponentCSharpGenerator.GenerateUIComponentClass(latestComponent);
                default:
                    return "";
            }
        }

        private string BuildSystemPrompt(ProjectContext context, List<OpenFileContext>? openFiles = null)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are an expert programming assistant helping developers with their code projects.");
            sb.AppendLine();
            sb.AppendLine("# EXECUTION ENVIRONMENT");
            sb.AppendLine("You are operating within an API platform that can create, manage, and execute code projects:");
            sb.AppendLine("- Projects are executed in isolated Docker containers");
            sb.AppendLine("- Python projects: Dependencies are installed automatically via 'pip install -r requirements.txt'");
            sb.AppendLine("- C# projects: Dependencies are restored automatically via 'dotnet restore'");
            sb.AppendLine("- Users do NOT need to install dependencies manually - the platform handles this");
            sb.AppendLine("- When adding new dependencies:");
            sb.AppendLine("  * Python: Add to requirements.txt file");
            sb.AppendLine("  * C#: Add to .csproj file or use PackageReference");
            sb.AppendLine("- The platform will automatically install dependencies before running the project");
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
            sb.AppendLine("5. Generate 3-5 contextual 'suggestedFollowUps' - natural next steps or questions based on what you just did:");
            sb.AppendLine("   - Make them specific to the current conversation and code changes");
            sb.AppendLine("   - Suggest testing, documentation, error handling, or related improvements");
            sb.AppendLine("   - For questions, suggest deeper dives, examples, or clarifications");
            sb.AppendLine("   - Keep them concise and actionable (e.g., 'Add unit tests for the login function')");
            sb.AppendLine();
            sb.AppendLine("IMPORTANT RULES:");
            sb.AppendLine("- Use relative file paths from project root");
            sb.AppendLine("- When specifying file paths always use forward slash (/)");
            sb.AppendLine("- Include FULL file content for CREATE/UPDATE operations (not just snippets)");
            sb.AppendLine("- Be concise but clear in explanations");
            sb.AppendLine("- Follow best practices for the programming language");
            sb.AppendLine("- If you need to modify a file, always include the complete updated content");
            sb.AppendLine();
            sb.AppendLine("FORMATTING REQUIREMENTS FOR displayText:");
            sb.AppendLine("- ALWAYS use newline characters (\\n) to separate items in lists");
            sb.AppendLine("- When listing items, put each item on a new line");
            sb.AppendLine("- When showing multiple items, use proper line breaks for readability");
            sb.AppendLine("- Format example for lists:");
            sb.AppendLine("  'Here are the items:\\n- Item1\\n- Item2\\n- Item3'");
            sb.AppendLine("- Never concatenate list items without newlines");
            sb.AppendLine("- Use markdown formatting when appropriate (bullet points, headers, code blocks)");
            sb.AppendLine();
            sb.AppendLine("FORMATTING REQUIREMENTS:");
            sb.AppendLine("- ALWAYS use newline characters (\\n) to separate lines.");
            sb.AppendLine("- ALWAYS use tab characters (\\t) for indentation.");

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

            // Check if the user prompt is already the last message in history
            var lastMessage = updatedHistory.LastOrDefault();
            var isPromptAlreadyInHistory = lastMessage != null &&
                                          lastMessage.Role == "user" &&
                                          lastMessage.Content == userPrompt;

            // Only add user message if it's not already there
            if (!isPromptAlreadyInHistory)
            {
                updatedHistory.Add(new ConversationMessage
                {
                    Role = "user",
                    Content = userPrompt,
                    Timestamp = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogDebug("User prompt already exists in history, skipping duplicate addition");
            }

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
            public List<string> SuggestedFollowUps { get; set; } = new();
        }

        #endregion
    }
}
