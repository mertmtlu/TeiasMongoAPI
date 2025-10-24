using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Implementation of intent classification for user prompts
    /// Uses pattern matching and heuristics to determine user intent
    /// </summary>
    public class IntentClassifier : IIntentClassifier
    {
        private readonly ILogger<IntentClassifier> _logger;

        public IntentClassifier(ILogger<IntentClassifier> logger)
        {
            _logger = logger;
        }

        public Task<UserIntent> ClassifyIntentAsync(
            string userPrompt,
            ProjectStructureAnalysis projectStructure,
            List<string>? currentlyOpenFiles = null,
            string? cursorContext = null,
            CancellationToken cancellationToken = default)
        {
            var prompt = userPrompt.ToLowerInvariant();
            var intent = new UserIntent
            {
                Confidence = 0.7 // Default confidence
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

            _logger.LogInformation("Classified intent: {IntentType}, Complexity: {Complexity}, Required files: {FileCount}",
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
            var allProjectFiles = structure.SourceFiles.Concat(structure.ConfigFiles).Concat(structure.BinaryFiles);
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
                var allProjectFiles = structure.SourceFiles.Concat(structure.ConfigFiles).Concat(structure.BinaryFiles);
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

        #endregion
    }
}
