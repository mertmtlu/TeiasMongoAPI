using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Implementation of intent classification for user prompts
    /// Uses AI-powered analysis with fallback to pattern matching
    /// </summary>
    public class IntentClassifier : IIntentClassifier
    {
        private readonly ILogger<IntentClassifier> _logger;
        private readonly ILLMClient _llmClient;
        private readonly ISemanticSearchService? _semanticSearchService;

        public IntentClassifier(
            ILogger<IntentClassifier> logger,
            ILLMClient llmClient,
            ISemanticSearchService? semanticSearchService = null)
        {
            _logger = logger;
            _llmClient = llmClient;
            _semanticSearchService = semanticSearchService;
        }

        public async Task<UserIntent> ClassifyIntentAsync(
            string userPrompt,
            ProjectStructureAnalysis projectStructure,
            List<string>? currentlyOpenFiles = null,
            string? cursorContext = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Use AI-powered intent classification
                var intent = await ClassifyIntentWithAIAsync(userPrompt, projectStructure, cancellationToken);

                // Add currently open files to required files (they provide important context)
                if (currentlyOpenFiles != null && currentlyOpenFiles.Any())
                {
                    intent.RequiredFiles = intent.RequiredFiles
                        .Union(currentlyOpenFiles)
                        .Distinct()
                        .ToList();
                }

                _logger.LogInformation(
                    "AI classified intent: {IntentType}, Scope: {FileScope}, Complexity: {Complexity}, Confidence: {Confidence:F2}",
                    intent.Type, intent.FileScope, intent.Complexity, intent.Confidence);

                if (!string.IsNullOrEmpty(intent.ScopeReasoning))
                {
                    _logger.LogDebug("Scope reasoning: {Reasoning}", intent.ScopeReasoning);
                }

                return intent;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI intent classification failed, falling back to heuristic-based classification");

                // Fallback to old keyword-based classification
                return await ClassifyIntentWithHeuristicsAsync(
                    userPrompt, projectStructure, currentlyOpenFiles, cursorContext, cancellationToken);
            }
        }

        /// <summary>
        /// AI-powered intent classification using LLM with structured output
        /// </summary>
        private async Task<UserIntent> ClassifyIntentWithAIAsync(
            string userPrompt,
            ProjectStructureAnalysis projectStructure,
            CancellationToken cancellationToken)
        {
            var systemPrompt = @"You are an intent classifier for a code assistant AI.

Your job is to analyze the user's request and determine:
1. What they want to do (create, update, fix, explain, etc.)
2. The SCOPE of files they want to work with
3. Any specific files or concepts mentioned

Be precise about scope:
- 'Specific': User mentions exact file(s) - 'update Login.cs', 'fix AuthController.cs'
- 'Related': User wants files related to a concept - 'authentication system', 'payment processing'
- 'AllFiles': User wants entire codebase - 'all files', 'the whole project', 'everything', 'entire codebase'
- 'Pattern': User wants files matching a pattern - 'all C# files', 'all .cs files', 'test files'

If scope is 'Related', extract the key concept/feature/system they're referring to.

Analyze the user request carefully and provide accurate classification.";

            var messages = new List<LLMMessage>
            {
                new LLMMessage
                {
                    Role = "user",
                    Content = $"Classify this user request:\n\n\"{userPrompt}\""
                }
            };

            var schema = GeminiSchemaBuilder.BuildIntentClassificationSchema();

            var response = await _llmClient.ChatCompletionAsync(
                systemPrompt,
                messages,
                temperature: 0.3, // Low temperature for consistent classification
                responseSchema: schema,
                cancellationToken: cancellationToken);

            // Parse the structured response
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            var classified = System.Text.Json.JsonSerializer.Deserialize<IntentClassificationResponse>(
                response.Content, options);

            if (classified == null)
            {
                throw new InvalidOperationException("Failed to parse AI intent classification response");
            }

            // Convert to UserIntent
            var intent = new UserIntent
            {
                Type = classified.IntentType,
                FileScope = classified.FileScope,
                RelatedConcept = classified.RelatedConcept,
                ScopeReasoning = classified.ScopeReasoning,
                Complexity = classified.Complexity,
                Confidence = classified.Confidence,
                RequiredFiles = new List<string>()
            };

            // Add mentioned files to required files
            if (classified.MentionedFiles != null && classified.MentionedFiles.Any())
            {
                intent.RequiredFiles.AddRange(classified.MentionedFiles);
            }

            return intent;
        }

        /// <summary>
        /// Fallback heuristic-based intent classification (original implementation)
        /// Used when AI classification fails
        /// </summary>
        private Task<UserIntent> ClassifyIntentWithHeuristicsAsync(
            string userPrompt,
            ProjectStructureAnalysis projectStructure,
            List<string>? currentlyOpenFiles,
            string? cursorContext,
            CancellationToken cancellationToken)
        {
            var prompt = userPrompt.ToLowerInvariant();
            var intent = new UserIntent
            {
                Confidence = 0.7, // Default confidence for heuristics
                FileScope = FileSelectionScope.Specific // Default scope
            };

            // Classify intent type based on keywords and patterns
            if (IsFileCreationIntent(prompt, out var targetFile))
            {
                intent.Type = IntentType.FileCreation;
                intent.TargetFile = targetFile;
                intent.Complexity = Models.AI.ComplexityLevel.Low;
                intent.RequiredFiles = GetRelevantFilesForCreation(targetFile, projectStructure, currentlyOpenFiles);
            }
            else if (IsBugFixIntent(prompt))
            {
                intent.Type = IntentType.BugFix;
                intent.Complexity = Models.AI.ComplexityLevel.Medium;
                intent.RequiredFiles = GetRelevantFilesForBugFix(prompt, projectStructure, currentlyOpenFiles);
            }
            else if (IsUpdateIntent(prompt, out targetFile))
            {
                intent.Type = IntentType.FileUpdate;
                intent.TargetFile = targetFile;
                intent.Complexity = Models.AI.ComplexityLevel.Medium;
                intent.RequiredFiles = GetRelevantFilesForUpdate(targetFile, projectStructure, currentlyOpenFiles);
            }
            else if (IsQuestionIntent(prompt))
            {
                intent.Type = IntentType.Question;
                intent.Complexity = Models.AI.ComplexityLevel.Low;
                intent.RequiredFiles = GetRelevantFilesForQuestion(prompt, projectStructure, currentlyOpenFiles);
            }
            else if (IsRefactoringIntent(prompt))
            {
                intent.Type = IntentType.Refactoring;
                intent.Complexity = Models.AI.ComplexityLevel.High;
                intent.RequiredFiles = GetRelevantFilesForRefactoring(prompt, projectStructure, currentlyOpenFiles);
            }
            else if (IsFeatureAdditionIntent(prompt))
            {
                intent.Type = IntentType.FeatureAddition;
                intent.Complexity = Models.AI.ComplexityLevel.High;
                intent.RequiredFiles = GetRelevantFilesForFeature(prompt, projectStructure, currentlyOpenFiles);
            }
            else
            {
                intent.Type = IntentType.Other;
                intent.Complexity = Models.AI.ComplexityLevel.Medium;
                intent.RequiredFiles = GetDefaultRelevantFiles(projectStructure, currentlyOpenFiles);
            }

            // If currently open files exist, prioritize them
            if (currentlyOpenFiles != null && currentlyOpenFiles.Any())
            {
                intent.RequiredFiles = intent.RequiredFiles
                    .Union(currentlyOpenFiles)
                    .Distinct()
                    .ToList();
            }

            _logger.LogInformation("Heuristic classified intent: {IntentType}, Complexity: {Complexity}, Required files: {FileCount}",
                intent.Type, intent.Complexity, intent.RequiredFiles.Count);

            return Task.FromResult(intent);
        }

        public List<string> SelectFilesForIntent(
            UserIntent intent,
            ProjectStructureAnalysis projectStructure,
            int maxTokens)
        {
            var selectedFiles = new List<string>();
            var estimatedTokens = 0;
            var avgTokensPerFile = 500; // Conservative estimate

            // Prioritize files based on importance
            var prioritizedFiles = PrioritizeFiles(intent.RequiredFiles, projectStructure);

            foreach (var file in prioritizedFiles)
            {
                if (estimatedTokens + avgTokensPerFile > maxTokens)
                {
                    _logger.LogWarning("Token budget exceeded. Skipping {File}", file);
                    break;
                }

                selectedFiles.Add(file);
                estimatedTokens += avgTokensPerFile;
            }

            return selectedFiles;
        }

        public async Task<List<string>> SelectFilesBasedOnScopeAsync(
            UserIntent intent,
            ProjectStructureAnalysis projectStructure,
            string programId,
            string versionId,
            int maxTokens,
            bool useSemanticSearch = false,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Selecting files for scope: {Scope}, Intent: {Intent}",
                intent.FileScope, intent.Type);

            List<string> selectedFiles;

            switch (intent.FileScope)
            {
                case FileSelectionScope.Specific:
                    // Use mentioned files or fall back to required files
                    selectedFiles = intent.RequiredFiles.Any()
                        ? intent.RequiredFiles
                        : new List<string>();
                    _logger.LogInformation("Specific scope: {Count} files", selectedFiles.Count);
                    break;

                case FileSelectionScope.AllFiles:
                    // User wants all files - return all source + config files
                    selectedFiles = projectStructure.SourceFiles
                        .Concat(projectStructure.ConfigFiles)
                        .ToList();
                    _logger.LogInformation("AllFiles scope: {Count} files", selectedFiles.Count);
                    break;

                case FileSelectionScope.Pattern:
                    // Apply pattern matching
                    selectedFiles = ApplyFilePattern(
                        intent.RequiredFiles,
                        intent.RelatedConcept,
                        projectStructure);
                    _logger.LogInformation("Pattern scope: {Count} files", selectedFiles.Count);
                    break;

                case FileSelectionScope.Related:
                    // Try semantic search first (most accurate)
                    if (useSemanticSearch && _semanticSearchService != null && !string.IsNullOrEmpty(intent.RelatedConcept))
                    {
                        selectedFiles = await FindRelatedFilesWithSemanticSearchAsync(
                            programId, versionId, intent.RelatedConcept, cancellationToken);

                        _logger.LogInformation("Related scope (semantic search): {Count} files for concept '{Concept}'",
                            selectedFiles.Count, intent.RelatedConcept);
                    }
                    // Fallback: use heuristic search
                    else if (!string.IsNullOrEmpty(intent.RelatedConcept))
                    {
                        selectedFiles = FindRelatedFilesWithHeuristics(
                            intent.RelatedConcept, projectStructure);

                        _logger.LogInformation("Related scope (heuristics): {Count} files for concept '{Concept}'",
                            selectedFiles.Count, intent.RelatedConcept);
                    }
                    else
                    {
                        // No concept specified, fall back to required files
                        selectedFiles = intent.RequiredFiles;
                        _logger.LogWarning("Related scope but no concept specified, using required files: {Count}",
                            selectedFiles.Count);
                    }
                    break;

                default:
                    selectedFiles = intent.RequiredFiles;
                    break;
            }

            // Apply token budget constraint
            var prioritizedFiles = PrioritizeFiles(selectedFiles, projectStructure);
            var finalFiles = ApplyTokenBudget(prioritizedFiles, maxTokens);

            _logger.LogInformation("File selection complete: {Final} files selected from {Total} candidates",
                finalFiles.Count, selectedFiles.Count);

            return finalFiles;
        }

        #region Intent Detection Methods

        private bool IsFileCreationIntent(string prompt, out string? targetFile)
        {
            targetFile = null;
            var keywords = new[] { "create", "add", "new file", "make a file", "generate" };

            if (keywords.Any(k => prompt.Contains(k)))
            {
                // Try to extract file name
                var filePattern = @"(?:create|add|new|make|generate)\s+(?:a\s+)?(?:file\s+)?(?:called\s+|named\s+)?([a-zA-Z0-9_\-\.\/]+\.[a-zA-Z]+)";
                var match = Regex.Match(prompt, filePattern);
                if (match.Success)
                {
                    targetFile = match.Groups[1].Value;
                }
                return true;
            }

            return false;
        }

        private bool IsBugFixIntent(string prompt)
        {
            var keywords = new[] { "fix", "bug", "error", "issue", "problem", "broken", "debug", "resolve" };
            return keywords.Any(k => prompt.Contains(k));
        }

        private bool IsUpdateIntent(string prompt, out string? targetFile)
        {
            targetFile = null;
            var keywords = new[] { "update", "modify", "change", "edit", "alter" };

            if (keywords.Any(k => prompt.Contains(k)))
            {
                // Try to extract file name
                var filePattern = @"(?:update|modify|change|edit|alter)\s+(?:the\s+)?([a-zA-Z0-9_\-\.\/]+\.[a-zA-Z]+)";
                var match = Regex.Match(prompt, filePattern);
                if (match.Success)
                {
                    targetFile = match.Groups[1].Value;
                }
                return true;
            }

            return false;
        }

        private bool IsQuestionIntent(string prompt)
        {
            var questionWords = new[] { "what", "how", "why", "when", "where", "who", "explain", "tell me", "show me", "?" };
            return questionWords.Any(k => prompt.Contains(k));
        }

        private bool IsRefactoringIntent(string prompt)
        {
            var keywords = new[] { "refactor", "restructure", "reorganize", "clean up", "improve structure" };
            return keywords.Any(k => prompt.Contains(k));
        }

        private bool IsFeatureAdditionIntent(string prompt)
        {
            var keywords = new[] { "add feature", "implement", "new feature", "functionality" };
            return keywords.Any(k => prompt.Contains(k));
        }

        #endregion

        #region File Selection Methods

        private List<string> GetRelevantFilesForCreation(string? targetFile, ProjectStructureAnalysis structure, List<string>? openFiles)
        {
            var files = new List<string>();

            // Add entry points to understand project structure
            files.AddRange(structure.EntryPoints.Take(1));

            // Add currently open files
            if (openFiles != null)
            {
                files.AddRange(openFiles.Take(2));
            }

            return files.Distinct().ToList();
        }

        private List<string> GetRelevantFilesForBugFix(string prompt, ProjectStructureAnalysis structure, List<string>? openFiles)
        {
            var files = new List<string>();

            // Try to find mentioned file names in the prompt (check all file types)
            var allProjectFiles = structure.SourceFiles
                .Concat(structure.ConfigFiles)
                .Concat(structure.BinaryFiles)
                .ToList();
            foreach (var projectFile in allProjectFiles)
            {
                var fileName = Path.GetFileName(projectFile);
                if (prompt.Contains(fileName.ToLowerInvariant()))
                {
                    files.Add(projectFile);
                }
            }

            // Add currently open files (user is likely looking at the bug)
            if (openFiles != null)
            {
                files.AddRange(openFiles);
            }

            // If no files found, add entry points
            if (!files.Any())
            {
                files.AddRange(structure.EntryPoints.Take(2));
            }

            return files.Distinct().ToList();
        }

        private List<string> GetRelevantFilesForUpdate(string? targetFile, ProjectStructureAnalysis structure, List<string>? openFiles)
        {
            var files = new List<string>();

            if (!string.IsNullOrEmpty(targetFile))
            {
                // Find the target file (check all file types)
                var allProjectFiles = structure.SourceFiles
                    .Concat(structure.ConfigFiles)
                    .Concat(structure.BinaryFiles)
                    .ToList();

                var matchingFile = allProjectFiles.FirstOrDefault(f =>
                    f.EndsWith(targetFile, StringComparison.OrdinalIgnoreCase) ||
                    f.Contains(targetFile, StringComparison.OrdinalIgnoreCase));

                if (matchingFile != null)
                {
                    files.Add(matchingFile);
                }
            }

            // Add currently open files
            if (openFiles != null)
            {
                files.AddRange(openFiles);
            }

            return files.Distinct().ToList();
        }

        private List<string> GetRelevantFilesForQuestion(string prompt, ProjectStructureAnalysis structure, List<string>? openFiles)
        {
            var files = new List<string>();

            // Try to find mentioned files
            foreach (var sourceFile in structure.SourceFiles)
            {
                var fileName = Path.GetFileName(sourceFile);
                if (prompt.Contains(fileName.ToLowerInvariant()))
                {
                    files.Add(sourceFile);
                }
            }

            // Add open files
            if (openFiles != null)
            {
                files.AddRange(openFiles.Take(3));
            }

            // Add entry points for context
            if (!files.Any())
            {
                files.AddRange(structure.EntryPoints.Take(1));
            }

            return files.Distinct().ToList();
        }

        private List<string> GetRelevantFilesForRefactoring(string prompt, ProjectStructureAnalysis structure, List<string>? openFiles)
        {
            var files = new List<string>();

            // For refactoring, we might need more context
            if (openFiles != null)
            {
                files.AddRange(openFiles);
            }

            // Add entry points
            files.AddRange(structure.EntryPoints.Take(2));

            // Look for mentioned files
            foreach (var sourceFile in structure.SourceFiles.Take(10))
            {
                var fileName = Path.GetFileName(sourceFile);
                if (prompt.Contains(fileName.ToLowerInvariant()))
                {
                    files.Add(sourceFile);
                }
            }

            return files.Distinct().ToList();
        }

        private List<string> GetRelevantFilesForFeature(string prompt, ProjectStructureAnalysis structure, List<string>? openFiles)
        {
            var files = new List<string>();

            // Add entry points
            files.AddRange(structure.EntryPoints.Take(2));

            // Add open files
            if (openFiles != null)
            {
                files.AddRange(openFiles);
            }

            // Add config files for understanding project setup
            files.AddRange(structure.ConfigFiles.Take(1));

            return files.Distinct().ToList();
        }

        private List<string> GetDefaultRelevantFiles(ProjectStructureAnalysis structure, List<string>? openFiles)
        {
            var files = new List<string>();

            // Add entry points
            files.AddRange(structure.EntryPoints.Take(1));

            // Add open files
            if (openFiles != null)
            {
                files.AddRange(openFiles.Take(2));
            }

            return files.Distinct().ToList();
        }

        private List<string> PrioritizeFiles(List<string> files, ProjectStructureAnalysis structure)
        {
            return files
                .OrderByDescending(f => structure.EntryPoints.Contains(f) ? 3 : 0) // Entry points first
                .ThenByDescending(f => structure.ConfigFiles.Contains(f) ? 2 : 0) // Then config files
                .ThenBy(f => f.Length) // Then shorter paths (likely more important)
                .ToList();
        }

        /// <summary>
        /// Find files related to a concept using semantic search
        /// </summary>
        private async Task<List<string>> FindRelatedFilesWithSemanticSearchAsync(
            string programId,
            string versionId,
            string concept,
            CancellationToken cancellationToken)
        {
            try
            {
                if (_semanticSearchService == null)
                {
                    _logger.LogWarning("Semantic search service not available");
                    return new List<string>();
                }

                _logger.LogInformation("Performing semantic search for concept: '{Concept}'", concept);

                // Search for top 15 relevant chunks
                var searchResults = await _semanticSearchService.SearchCodeAsync(
                    programId,
                    versionId,
                    concept,
                    topK: 15,
                    cancellationToken: cancellationToken);

                // Extract unique file paths from chunks
                var relatedFiles = searchResults
                    .Select(r => r.Chunk.FilePath)
                    .Distinct()
                    .ToList();

                _logger.LogInformation("Semantic search found {Count} files for concept '{Concept}'",
                    relatedFiles.Count, concept);

                return relatedFiles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Semantic search failed for concept '{Concept}'", concept);
                return new List<string>();
            }
        }

        /// <summary>
        /// Find files related to a concept using heuristic keyword matching
        /// Fallback when semantic search is not available
        /// </summary>
        private List<string> FindRelatedFilesWithHeuristics(
            string concept,
            ProjectStructureAnalysis projectStructure)
        {
            var relatedFiles = new List<string>();

            // Extract keywords from concept (split by space, remove common words)
            var commonWords = new[] { "the", "a", "an", "in", "on", "at", "to", "for", "of", "and", "or", "system", "code", "files" };
            var keywords = concept
                .ToLowerInvariant()
                .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Length > 2 && !commonWords.Contains(word))
                .ToList();

            _logger.LogDebug("Extracted keywords from concept '{Concept}': {Keywords}",
                concept, string.Join(", ", keywords));

            if (!keywords.Any())
            {
                _logger.LogWarning("No keywords extracted from concept '{Concept}'", concept);
                return relatedFiles;
            }

            // Search in file paths and names
            var allFiles = projectStructure.SourceFiles
                .Concat(projectStructure.ConfigFiles)
                .ToList();

            foreach (var file in allFiles)
            {
                var fileName = Path.GetFileName(file).ToLowerInvariant();
                var filePath = file.ToLowerInvariant();

                // Check if any keyword appears in path or filename
                if (keywords.Any(keyword =>
                    fileName.Contains(keyword) ||
                    filePath.Contains(keyword)))
                {
                    relatedFiles.Add(file);
                }
            }

            _logger.LogInformation("Heuristic search found {Count} files matching keywords from '{Concept}'",
                relatedFiles.Count, concept);

            // If we found too few files, be more lenient (partial matches)
            if (relatedFiles.Count < 3)
            {
                foreach (var file in allFiles)
                {
                    if (relatedFiles.Contains(file)) continue;

                    var fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();

                    // Check for partial matches
                    if (keywords.Any(keyword =>
                        fileName.Contains(keyword) ||
                        keyword.Contains(fileName)))
                    {
                        relatedFiles.Add(file);
                    }
                }

                _logger.LogDebug("Extended search found {Total} files total", relatedFiles.Count);
            }

            return relatedFiles.Distinct().ToList();
        }

        /// <summary>
        /// Apply file pattern matching (e.g., "all .cs files", "test files")
        /// </summary>
        private List<string> ApplyFilePattern(
            List<string> mentionedPatterns,
            string? conceptPattern,
            ProjectStructureAnalysis projectStructure)
        {
            var matchedFiles = new List<string>();
            var allFiles = projectStructure.SourceFiles
                .Concat(projectStructure.ConfigFiles)
                .ToList();

            // Combine mentioned patterns and concept pattern
            var patterns = new List<string>();
            if (mentionedPatterns != null && mentionedPatterns.Any())
                patterns.AddRange(mentionedPatterns);
            if (!string.IsNullOrEmpty(conceptPattern))
                patterns.Add(conceptPattern);

            foreach (var pattern in patterns)
            {
                var patternLower = pattern.ToLowerInvariant();

                // Check for extension patterns (e.g., ".cs", ".js", "*.py")
                if (patternLower.Contains("."))
                {
                    var extension = patternLower.Replace("*", "").Trim();
                    matchedFiles.AddRange(allFiles.Where(f => f.EndsWith(extension, StringComparison.OrdinalIgnoreCase)));
                }
                // Check for keyword patterns (e.g., "test", "config", "service")
                else
                {
                    matchedFiles.AddRange(allFiles.Where(f =>
                        f.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
                }
            }

            _logger.LogInformation("Pattern matching found {Count} files for patterns: {Patterns}",
                matchedFiles.Distinct().Count(), string.Join(", ", patterns));

            return matchedFiles.Distinct().ToList();
        }

        /// <summary>
        /// Apply token budget to file list
        /// </summary>
        private List<string> ApplyTokenBudget(List<string> files, int maxTokens)
        {
            var selectedFiles = new List<string>();
            var estimatedTokens = 0;
            var avgTokensPerFile = 500; // Conservative estimate

            foreach (var file in files)
            {
                if (estimatedTokens + avgTokensPerFile > maxTokens)
                {
                    _logger.LogDebug("Token budget exceeded. Selected {Count}/{Total} files",
                        selectedFiles.Count, files.Count);
                    break;
                }

                selectedFiles.Add(file);
                estimatedTokens += avgTokensPerFile;
            }

            return selectedFiles;
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Response structure for AI intent classification
        /// </summary>
        private class IntentClassificationResponse
        {
            public IntentType IntentType { get; set; }
            public FileSelectionScope FileScope { get; set; }
            public string? RelatedConcept { get; set; }
            public List<string>? MentionedFiles { get; set; }
            public string ScopeReasoning { get; set; } = string.Empty;
            public Models.AI.ComplexityLevel Complexity { get; set; }
            public double Confidence { get; set; }
        }

        #endregion
    }
}
