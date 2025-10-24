using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using TeiasMongoAPI.Services.Configuration;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Smart code chunker that creates semantic chunks optimized for vector search
    /// Uses symbol extraction from CodeIndexer and intelligent chunking strategies
    /// </summary>
    public class SmartCodeChunker : ICodeChunker
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IExecutionService _executionService;
        private readonly ICodeIndexer _codeIndexer;
        private readonly EmbeddingSettings _settings;
        private readonly ILogger<SmartCodeChunker> _logger;

        public SmartCodeChunker(
            IFileStorageService fileStorageService,
            IExecutionService executionService,
            ICodeIndexer codeIndexer,
            IOptions<EmbeddingSettings> settings,
            ILogger<SmartCodeChunker> logger)
        {
            _fileStorageService = fileStorageService;
            _executionService = executionService;
            _codeIndexer = codeIndexer;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<List<CodeChunk>> ChunkProjectAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Chunking project {ProgramId} version {VersionId}", programId, versionId);

            var chunks = new List<CodeChunk>();

            try
            {
                // Get project structure
                var structure = await _executionService.AnalyzeProjectStructureForAIAsync(
                    programId, versionId, cancellationToken);

                _logger.LogInformation("Found {FileCount} source files to chunk", structure.SourceFiles.Count);

                List<string> allFiles = structure.SourceFiles.Concat(structure.ConfigFiles).ToList();

                // Process each source file
                foreach (var filePath in allFiles)
                {
                    try
                    {
                        // Read file content
                        var fileBytes = await _fileStorageService.GetFileContentAsync(
                            programId, versionId, filePath, cancellationToken);
                        var content = Encoding.UTF8.GetString(fileBytes);

                        // Chunk the file
                        var fileChunks = await ChunkFileAsync(
                            programId, versionId, filePath, content, structure.Language, cancellationToken);

                        chunks.AddRange(fileChunks);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to chunk file {FilePath}", filePath);
                    }
                }

                _logger.LogInformation("Created {ChunkCount} chunks from {FileCount} files",
                    chunks.Count, structure.SourceFiles.Count);

                return chunks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to chunk project {ProgramId}", programId);
                throw;
            }
        }

        public async Task<List<CodeChunk>> ChunkFileAsync(
            string programId,
            string versionId,
            string filePath,
            string content,
            string language,
            CancellationToken cancellationToken = default)
        {
            var chunks = new List<CodeChunk>();

            try
            {
                // Determine chunking strategy
                var strategy = DetermineStrategy(filePath, content, language);
                _logger.LogDebug("Using {Strategy} strategy for {FilePath}", strategy, filePath);

                switch (strategy)
                {
                    case ChunkingStrategy.FunctionLevel:
                        chunks = await ChunkByFunction(programId, versionId, filePath, content, language);
                        break;

                    case ChunkingStrategy.ClassLevel:
                        chunks = await ChunkByClass(programId, versionId, filePath, content, language);
                        break;

                    case ChunkingStrategy.SlidingWindow:
                        chunks = ChunkBySlidingWindow(programId, versionId, filePath, content, language);
                        break;

                    case ChunkingStrategy.WholeFile:
                        chunks = new List<CodeChunk> { CreateWholeFileChunk(programId, versionId, filePath, content, language) };
                        break;

                    case ChunkingStrategy.BlockLevel:
                        chunks = ChunkByBlock(programId, versionId, filePath, content, language);
                        break;
                }

                _logger.LogDebug("Created {ChunkCount} chunks from {FilePath}", chunks.Count, filePath);

                return chunks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to chunk file {FilePath}", filePath);
                return new List<CodeChunk>();
            }
        }

        public ChunkingStrategy DetermineStrategy(string filePath, string content, string language)
        {
            var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();
            var lineCount = content.Split('\n').Length;
            var tokenEstimate = content.Length / 4; // Rough token estimate

            // Small files - keep whole
            if (tokenEstimate < _settings.ChunkSize / 2)
            {
                return ChunkingStrategy.WholeFile;
            }

            // Configuration files - keep whole or use sliding window
            if (fileExtension is ".json" or ".xml" or ".yaml" or ".yml" or ".toml" or ".ini")
            {
                return tokenEstimate < _settings.ChunkSize
                    ? ChunkingStrategy.WholeFile
                    : ChunkingStrategy.SlidingWindow;
            }

            // Extract symbols to determine structure
            var symbols = _codeIndexer.ExtractSymbols(filePath, content, language);
            var functions = symbols.Where(s => s.Type == SymbolType.Method).ToList();
            var classes = symbols.Where(s => s.Type == SymbolType.Class).ToList();

            // If we have clear functions, use function-level chunking
            if (functions.Count > 0)
            {
                return ChunkingStrategy.FunctionLevel;
            }

            // If we have classes but no clear functions, use class-level
            if (classes.Count > 0)
            {
                return ChunkingStrategy.ClassLevel;
            }

            // For scripts and unstructured code
            if (language.ToLowerInvariant() is "python" or "javascript" or "typescript")
            {
                return ChunkingStrategy.BlockLevel;
            }

            // Fallback to sliding window for everything else
            return ChunkingStrategy.SlidingWindow;
        }

        #region Chunking Strategy Implementations

        private async Task<List<CodeChunk>> ChunkByFunction(
            string programId,
            string versionId,
            string filePath,
            string content,
            string language)
        {
            var chunks = new List<CodeChunk>();
            var symbols = _codeIndexer.ExtractSymbols(filePath, content, language);
            var lines = content.Split('\n');

            // Group methods by parent (class)
            var methods = symbols.Where(s => s.Type == SymbolType.Method).ToList();

            foreach (var method in methods)
            {
                // Extract method content (simple heuristic: from declaration to next method or end)
                var startLine = method.LineNumber - 1; // 0-indexed
                var endLine = FindMethodEndLine(lines, startLine, language);

                var methodContent = string.Join("\n", lines.Skip(startLine).Take(endLine - startLine + 1));

                // Create chunk
                var chunk = new CodeChunk
                {
                    Id = GenerateChunkId(programId, versionId, filePath, method.Name, startLine),
                    ProgramId = programId,
                    VersionId = versionId,
                    FilePath = filePath,
                    Type = ChunkType.Function,
                    Name = method.Name,
                    Content = methodContent,
                    StartLine = startLine + 1,
                    EndLine = endLine + 1,
                    Language = language,
                    ParentContext = method.Parent,
                    ContentHash = ComputeHash(methodContent),
                    Metadata = new Dictionary<string, object>
                    {
                        ["signature"] = method.Signature,
                        ["visibility"] = method.Visibility ?? "unknown",
                        ["returnType"] = method.ReturnType ?? "void"
                    }
                };

                chunks.Add(chunk);
            }

            return await Task.FromResult(chunks);
        }

        private async Task<List<CodeChunk>> ChunkByClass(
            string programId,
            string versionId,
            string filePath,
            string content,
            string language)
        {
            var chunks = new List<CodeChunk>();
            var symbols = _codeIndexer.ExtractSymbols(filePath, content, language);
            var lines = content.Split('\n');

            var classes = symbols.Where(s => s.Type == SymbolType.Class || s.Type == SymbolType.Interface).ToList();

            foreach (var cls in classes)
            {
                var startLine = cls.LineNumber - 1;
                var endLine = FindClassEndLine(lines, startLine, language);

                var classContent = string.Join("\n", lines.Skip(startLine).Take(endLine - startLine + 1));

                var chunk = new CodeChunk
                {
                    Id = GenerateChunkId(programId, versionId, filePath, cls.Name, startLine),
                    ProgramId = programId,
                    VersionId = versionId,
                    FilePath = filePath,
                    Type = cls.Type == SymbolType.Interface ? ChunkType.Interface : ChunkType.Class,
                    Name = cls.Name,
                    Content = classContent,
                    StartLine = startLine + 1,
                    EndLine = endLine + 1,
                    Language = language,
                    ParentContext = cls.Parent,
                    ContentHash = ComputeHash(classContent),
                    Metadata = new Dictionary<string, object>
                    {
                        ["signature"] = cls.Signature,
                        ["visibility"] = cls.Visibility ?? "unknown"
                    }
                };

                chunks.Add(chunk);
            }

            return await Task.FromResult(chunks);
        }

        private List<CodeChunk> ChunkBySlidingWindow(
            string programId,
            string versionId,
            string filePath,
            string content,
            string language)
        {
            var chunks = new List<CodeChunk>();
            var lines = content.Split('\n');

            // Estimate lines per chunk based on token size
            var avgCharsPerLine = content.Length / Math.Max(1, lines.Length);
            var linesPerChunk = Math.Max(10, (_settings.ChunkSize * 4) / Math.Max(1, avgCharsPerLine));
            var overlapLines = Math.Max(2, (_settings.ChunkOverlap * 4) / Math.Max(1, avgCharsPerLine));

            for (int i = 0; i < lines.Length; i += (linesPerChunk - overlapLines))
            {
                var chunkLines = lines.Skip(i).Take(linesPerChunk).ToList();
                if (chunkLines.Count == 0) break;

                var chunkContent = string.Join("\n", chunkLines);
                var startLine = i + 1;
                var endLine = i + chunkLines.Count;

                var chunk = new CodeChunk
                {
                    Id = GenerateChunkId(programId, versionId, filePath, $"window_{i}", i),
                    ProgramId = programId,
                    VersionId = versionId,
                    FilePath = filePath,
                    Type = ChunkType.CodeBlock,
                    Name = $"Block at line {startLine}",
                    Content = chunkContent,
                    StartLine = startLine,
                    EndLine = endLine,
                    Language = language,
                    ContentHash = ComputeHash(chunkContent),
                    Metadata = new Dictionary<string, object>
                    {
                        ["chunkIndex"] = i / (linesPerChunk - overlapLines),
                        ["isPartial"] = true
                    }
                };

                chunks.Add(chunk);
            }

            return chunks;
        }

        private CodeChunk CreateWholeFileChunk(
            string programId,
            string versionId,
            string filePath,
            string content,
            string language)
        {
            var lines = content.Split('\n');

            return new CodeChunk
            {
                Id = GenerateChunkId(programId, versionId, filePath, "whole_file", 0),
                ProgramId = programId,
                VersionId = versionId,
                FilePath = filePath,
                Type = DetermineFileChunkType(filePath),
                Name = Path.GetFileName(filePath),
                Content = content,
                StartLine = 1,
                EndLine = lines.Length,
                Language = language,
                ContentHash = ComputeHash(content),
                Metadata = new Dictionary<string, object>
                {
                    ["isWholeFile"] = true
                }
            };
        }

        private List<CodeChunk> ChunkByBlock(
            string programId,
            string versionId,
            string filePath,
            string content,
            string language)
        {
            // For now, use sliding window strategy for block-level
            // Can be enhanced with language-specific block detection
            return ChunkBySlidingWindow(programId, versionId, filePath, content, language);
        }

        #endregion

        #region Helper Methods

        private int FindMethodEndLine(string[] lines, int startLine, string language)
        {
            // Simple heuristic: find matching closing brace
            // This is a simplified version - can be enhanced with proper parsing
            int braceCount = 0;
            bool foundStart = false;

            for (int i = startLine; i < lines.Length; i++)
            {
                var line = lines[i];

                // Count braces
                foreach (char c in line)
                {
                    if (c == '{')
                    {
                        braceCount++;
                        foundStart = true;
                    }
                    else if (c == '}')
                    {
                        braceCount--;
                        if (foundStart && braceCount == 0)
                        {
                            return i;
                        }
                    }
                }
            }

            // Fallback: next 50 lines or end of file
            return Math.Min(startLine + 50, lines.Length - 1);
        }

        private int FindClassEndLine(string[] lines, int startLine, string language)
        {
            // Similar to FindMethodEndLine but for classes
            return FindMethodEndLine(lines, startLine, language);
        }

        private ChunkType DetermineFileChunkType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".json" or ".xml" or ".yaml" or ".yml" or ".toml" or ".ini" or ".config" => ChunkType.Config,
                ".md" or ".txt" or ".rst" => ChunkType.Documentation,
                _ => ChunkType.CodeBlock
            };
        }

        private string GenerateChunkId(string programId, string versionId, string filePath, string name, int line)
        {
            var input = $"{programId}_{versionId}_{filePath}_{name}_{line}";
            var hash = ComputeHash(input);
            return $"chunk_{hash.Substring(0, 16)}";
        }

        private string ComputeHash(string content)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hashBytes = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        #endregion
    }
}
