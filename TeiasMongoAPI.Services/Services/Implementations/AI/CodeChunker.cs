using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Implementation of code chunking service
    /// Intelligently splits code into semantic chunks for embedding
    /// </summary>
    public class CodeChunker : ICodeChunker
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IExecutionService _executionService;
        private readonly ILogger<CodeChunker> _logger;

        public CodeChunker(
            IFileStorageService fileStorageService,
            IExecutionService executionService,
            ILogger<CodeChunker> logger)
        {
            _fileStorageService = fileStorageService;
            _executionService = executionService;
            _logger = logger;
        }

        public async Task<List<CodeChunk>> ChunkProjectAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Chunking project {ProgramId}, version {VersionId}", programId, versionId);

            var structure = await _executionService.AnalyzeProjectStructureAsync(programId, versionId, cancellationToken);
            var allChunks = new List<CodeChunk>();

            foreach (var filePath in structure.SourceFiles)
            {
                try
                {
                    var fileBytes = await _fileStorageService.GetFileContentAsync(programId, versionId, filePath, cancellationToken);
                    var content = Encoding.UTF8.GetString(fileBytes);

                    var chunks = ChunkFile(filePath, content, structure.Language, programId, versionId);
                    allChunks.AddRange(chunks);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to chunk file {FilePath}", filePath);
                }
            }

            _logger.LogInformation("Project chunking complete: {TotalChunks} chunks generated", allChunks.Count);
            return allChunks;
        }

        public List<CodeChunk> ChunkFile(
            string filePath,
            string content,
            string language,
            string programId,
            string versionId)
        {
            language = language.ToLowerInvariant();

            return language switch
            {
                "c#" or "csharp" => ChunkCSharpFile(filePath, content, programId, versionId),
                "python" or "py" => ChunkPythonFile(filePath, content, programId, versionId),
                "javascript" or "typescript" or "js" or "ts" => ChunkJavaScriptFile(filePath, content, programId, versionId),
                _ => ChunkGenericFile(filePath, content, language, programId, versionId)
            };
        }

        public string GetChunkingStrategy(string language)
        {
            language = language.ToLowerInvariant();

            return language switch
            {
                "c#" or "csharp" => "Chunks by class and method boundaries",
                "python" or "py" => "Chunks by class and function definitions",
                "javascript" or "typescript" or "js" or "ts" => "Chunks by class, function, and export boundaries",
                _ => "Chunks by logical code blocks (functions, classes when detectable)"
            };
        }

        #region C# Chunking

        private List<CodeChunk> ChunkCSharpFile(string filePath, string content, string programId, string versionId)
        {
            var chunks = new List<CodeChunk>();
            var lines = content.Split('\n');
            string? currentNamespace = null;
            string? currentClass = null;
            int classStartLine = -1;
            var classContent = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                // Track namespace
                var nsMatch = Regex.Match(trimmed, @"^\s*namespace\s+([\w\.]+)");
                if (nsMatch.Success)
                {
                    currentNamespace = nsMatch.Groups[1].Value;
                }

                // Detect class start
                var classMatch = Regex.Match(trimmed, @"(public|private|protected|internal)?\s*(static|abstract|sealed)?\s*class\s+(\w+)");
                if (classMatch.Success)
                {
                    // Save previous class if exists
                    if (currentClass != null && classContent.Length > 0)
                    {
                        chunks.Add(CreateChunk(filePath, classContent.ToString(), ChunkType.Class,
                            currentClass, classStartLine, i - 1, programId, versionId, currentNamespace));
                    }

                    currentClass = classMatch.Groups[3].Value;
                    classStartLine = i;
                    classContent.Clear();
                }

                if (currentClass != null)
                {
                    classContent.AppendLine(line);
                }

                // Detect method (chunk methods separately within classes)
                var methodMatch = Regex.Match(trimmed, @"(public|private|protected|internal)?\s*(static|virtual|override|async)?\s*[\w<>,\[\]\?]+\s+(\w+)\s*\([^\)]*\)");
                if (methodMatch.Success && currentClass != null && !trimmed.Contains("class "))
                {
                    var methodName = methodMatch.Groups[3].Value;
                    var methodContent = ExtractMethodBody(lines, i);

                    if (methodContent.HasValue)
                    {
                        chunks.Add(CreateChunk(filePath, methodContent.Value.Content, ChunkType.Function,
                            $"{currentClass}.{methodName}", i, methodContent.Value.EndLine, programId, versionId, currentClass));
                    }
                }
            }

            // Add final class if exists
            if (currentClass != null && classContent.Length > 0)
            {
                chunks.Add(CreateChunk(filePath, classContent.ToString(), ChunkType.Class,
                    currentClass, classStartLine, lines.Length - 1, programId, versionId, currentNamespace));
            }

            return chunks;
        }

        #endregion

        #region Python Chunking

        private List<CodeChunk> ChunkPythonFile(string filePath, string content, string programId, string versionId)
        {
            var chunks = new List<CodeChunk>();
            var lines = content.Split('\n');
            string? currentClass = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();

                // Detect class
                var classMatch = Regex.Match(trimmed, @"^class\s+(\w+)");
                if (classMatch.Success)
                {
                    var className = classMatch.Groups[1].Value;
                    var classContent = ExtractPythonBlock(lines, i);

                    if (classContent.HasValue)
                    {
                        chunks.Add(CreateChunk(filePath, classContent.Value.Content, ChunkType.Class,
                            className, i, classContent.Value.EndLine, programId, versionId));
                        currentClass = className;
                    }
                }

                // Detect function/method
                var funcMatch = Regex.Match(trimmed, @"^(async\s+)?def\s+(\w+)");
                if (funcMatch.Success)
                {
                    var funcName = funcMatch.Groups[2].Value;
                    var funcContent = ExtractPythonBlock(lines, i);

                    if (funcContent.HasValue)
                    {
                        chunks.Add(CreateChunk(filePath, funcContent.Value.Content, ChunkType.Function,
                            funcName, i, funcContent.Value.EndLine, programId, versionId, currentClass));
                    }
                }
            }

            return chunks;
        }

        #endregion

        #region JavaScript/TypeScript Chunking

        private List<CodeChunk> ChunkJavaScriptFile(string filePath, string content, string programId, string versionId)
        {
            var chunks = new List<CodeChunk>();
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();

                // Detect class
                var classMatch = Regex.Match(trimmed, @"(export\s+)?(class|interface)\s+(\w+)");
                if (classMatch.Success)
                {
                    var name = classMatch.Groups[3].Value;
                    var type = classMatch.Groups[2].Value == "interface" ? ChunkType.Interface : ChunkType.Class;
                    var blockContent = ExtractBracedBlock(lines, i);

                    if (blockContent.HasValue)
                    {
                        chunks.Add(CreateChunk(filePath, blockContent.Value.Content, type,
                            name, i, blockContent.Value.EndLine, programId, versionId));
                    }
                }

                // Detect function
                var funcMatch = Regex.Match(trimmed, @"(export\s+)?(async\s+)?(function\s+)?(\w+)\s*[=:]?\s*(?:async\s*)?\(");
                if (funcMatch.Success && !trimmed.Contains("class "))
                {
                    var funcName = funcMatch.Groups[4].Value;
                    var blockContent = ExtractBracedBlock(lines, i);

                    if (blockContent.HasValue)
                    {
                        chunks.Add(CreateChunk(filePath, blockContent.Value.Content, ChunkType.Function,
                            funcName, i, blockContent.Value.EndLine, programId, versionId));
                    }
                }
            }

            return chunks;
        }

        #endregion

        #region Generic Chunking

        private List<CodeChunk> ChunkGenericFile(string filePath, string content, string language, string programId, string versionId)
        {
            // Fallback: chunk by logical blocks (functions with braces)
            var chunks = new List<CodeChunk>();
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var funcMatch = Regex.Match(lines[i], @"(\w+)\s*\([^\)]*\)\s*\{?");
                if (funcMatch.Success)
                {
                    var funcName = funcMatch.Groups[1].Value;
                    var blockContent = ExtractBracedBlock(lines, i);

                    if (blockContent.HasValue)
                    {
                        chunks.Add(CreateChunk(filePath, blockContent.Value.Content, ChunkType.Function,
                            funcName, i, blockContent.Value.EndLine, programId, versionId));
                    }
                }
            }

            // If no chunks found, chunk the entire file
            if (!chunks.Any())
            {
                chunks.Add(CreateChunk(filePath, content, ChunkType.CodeBlock,
                    Path.GetFileName(filePath), 0, lines.Length - 1, programId, versionId));
            }

            return chunks;
        }

        #endregion

        #region Helper Methods

        private CodeChunk CreateChunk(
            string filePath,
            string content,
            ChunkType type,
            string name,
            int startLine,
            int endLine,
            string programId,
            string versionId,
            string? parentContext = null)
        {
            return new CodeChunk
            {
                Id = GenerateChunkId(programId, versionId, filePath, startLine),
                ProgramId = programId,
                VersionId = versionId,
                FilePath = filePath,
                Type = type,
                Name = name,
                Content = content,
                StartLine = startLine,
                EndLine = endLine,
                ParentContext = parentContext,
                ContentHash = ComputeHash(content)
            };
        }

        private string GenerateChunkId(string programId, string versionId, string filePath, int startLine)
        {
            return $"{programId}_{versionId}_{filePath}_{startLine}".Replace('/', '_').Replace('\\', '_');
        }

        private string ComputeHash(string content)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        private (string Content, int EndLine)? ExtractMethodBody(string[] lines, int startLine)
        {
            // Simple brace matching for C# methods
            return ExtractBracedBlock(lines, startLine);
        }

        private (string Content, int EndLine)? ExtractBracedBlock(string[] lines, int startLine)
        {
            var content = new StringBuilder();
            int braceCount = 0;
            bool foundOpenBrace = false;

            for (int i = startLine; i < lines.Length; i++)
            {
                var line = lines[i];
                content.AppendLine(line);

                foreach (var ch in line)
                {
                    if (ch == '{')
                    {
                        braceCount++;
                        foundOpenBrace = true;
                    }
                    else if (ch == '}')
                    {
                        braceCount--;

                        if (foundOpenBrace && braceCount == 0)
                        {
                            return (content.ToString(), i);
                        }
                    }
                }

                // Handle single-line methods (no braces)
                if (i == startLine && line.TrimEnd().EndsWith(';'))
                {
                    return (content.ToString(), i);
                }
            }

            return foundOpenBrace && braceCount == 0 ? (content.ToString(), lines.Length - 1) : null;
        }

        private (string Content, int EndLine)? ExtractPythonBlock(string[] lines, int startLine)
        {
            var content = new StringBuilder();
            var baseIndent = GetIndentation(lines[startLine]);
            content.AppendLine(lines[startLine]);

            for (int i = startLine + 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                // Empty lines and comments belong to the block
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                {
                    content.AppendLine(line);
                    continue;
                }

                var currentIndent = GetIndentation(line);

                // If indentation is less than or equal to base, block has ended
                if (currentIndent <= baseIndent)
                {
                    return (content.ToString(), i - 1);
                }

                content.AppendLine(line);
            }

            return (content.ToString(), lines.Length - 1);
        }

        private int GetIndentation(string line)
        {
            int count = 0;
            foreach (var ch in line)
            {
                if (ch == ' ') count++;
                else if (ch == '\t') count += 4; // Treat tab as 4 spaces
                else break;
            }
            return count;
        }

        #endregion
    }
}
