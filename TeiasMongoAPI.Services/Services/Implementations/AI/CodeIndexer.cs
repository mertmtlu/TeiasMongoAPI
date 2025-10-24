using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Models.AI;

namespace TeiasMongoAPI.Services.Services.Implementations.AI
{
    /// <summary>
    /// Implementation of code indexing service
    /// Parses source files and extracts symbols (classes, methods, etc.) without implementations
    /// </summary>
    public class CodeIndexer : ICodeIndexer
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IExecutionService _executionService;
        private readonly ILogger<CodeIndexer> _logger;

        public CodeIndexer(
            IFileStorageService fileStorageService,
            IExecutionService executionService,
            ILogger<CodeIndexer> logger)
        {
            _fileStorageService = fileStorageService;
            _executionService = executionService;
            _logger = logger;
        }

        public async Task<ProjectIndex> GenerateProjectIndexAsync(
            string programId,
            string versionId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Generating project index for program {ProgramId}, version {VersionId}",
                programId, versionId);

            // Get project structure
            // Use AI-specific method to work with unapproved/draft versions
            var structure = await _executionService.AnalyzeProjectStructureForAIAsync(programId, versionId, cancellationToken);

            List<string> allFiles = structure.SourceFiles.Concat(structure.ConfigFiles).ToList();

            var index = new ProjectIndex
            {
                ProgramId = programId,
                VersionId = versionId,
                Language = structure.Language,
                AllFiles = allFiles  // Include both source and config files
            };

            var totalSymbols = 0;

            // Process each source file
            foreach (var filePath in allFiles)
            {
                try
                {
                    // Read file content
                    var fileBytes = await _fileStorageService.GetFileContentAsync(programId, versionId, filePath, cancellationToken);
                    var content = Encoding.UTF8.GetString(fileBytes);

                    // Extract symbols
                    var symbols = ExtractSymbols(filePath, content, structure.Language);

                    // Always add file to index, even if no symbols (e.g., config files)
                    index.FileSymbols[filePath] = symbols;
                    totalSymbols += symbols.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to index file {FilePath}", filePath);
                }
            }

            index.TotalSymbols = totalSymbols;
            index.EstimatedTokens = EstimateIndexTokens(index);

            _logger.LogInformation("Project index generated: {FileCount} files, {SymbolCount} symbols, ~{Tokens} tokens",
                index.FileSymbols.Count, totalSymbols, index.EstimatedTokens);

            return index;
        }

        public string FormatIndexForPrompt(ProjectIndex index)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# PROJECT INDEX ({index.Language})");
            sb.AppendLine($"Total Files: {index.AllFiles.Count}, Total Symbols: {index.TotalSymbols}");
            sb.AppendLine();

            // Group files by directory for better organization
            var filesByDirectory = index.FileSymbols
                .GroupBy(kvp => Path.GetDirectoryName(kvp.Key) ?? "")
                .OrderBy(g => g.Key);

            foreach (var dirGroup in filesByDirectory)
            {
                if (!string.IsNullOrEmpty(dirGroup.Key))
                {
                    sb.AppendLine($"## Directory: {dirGroup.Key}");
                }

                foreach (var (filePath, symbols) in dirGroup.OrderBy(kvp => kvp.Key))
                {
                    sb.AppendLine($"### File: {Path.GetFileName(filePath)}");

                    if (!symbols.Any())
                    {
                        sb.AppendLine("(no symbols)");
                        continue;
                    }

                    // Group symbols by type
                    var namespaces = symbols.Where(s => s.Type == SymbolType.Namespace).ToList();
                    var classes = symbols.Where(s => s.Type == SymbolType.Class).ToList();
                    var interfaces = symbols.Where(s => s.Type == SymbolType.Interface).ToList();
                    var methods = symbols.Where(s => s.Type == SymbolType.Method).ToList();
                    var properties = symbols.Where(s => s.Type == SymbolType.Property).ToList();

                    // Namespaces
                    foreach (var symbol in namespaces)
                    {
                        sb.AppendLine($"  namespace {symbol.Name}");
                    }

                    // Classes
                    foreach (var symbol in classes)
                    {
                        sb.AppendLine($"  {FormatSymbolSignature(symbol)}");
                    }

                    // Interfaces
                    foreach (var symbol in interfaces)
                    {
                        sb.AppendLine($"  {FormatSymbolSignature(symbol)}");
                    }

                    // Methods (show parent class if available)
                    var methodsByParent = methods.GroupBy(m => m.Parent ?? "");
                    foreach (var group in methodsByParent)
                    {
                        if (!string.IsNullOrEmpty(group.Key))
                        {
                            sb.AppendLine($"    // Methods in {group.Key}:");
                        }
                        foreach (var method in group)
                        {
                            sb.AppendLine($"    {FormatSymbolSignature(method)}");
                        }
                    }

                    // Properties
                    if (properties.Any())
                    {
                        var propsByParent = properties.GroupBy(p => p.Parent ?? "");
                        foreach (var group in propsByParent)
                        {
                            if (!string.IsNullOrEmpty(group.Key))
                            {
                                sb.AppendLine($"    // Properties in {group.Key}:");
                            }
                            foreach (var prop in group)
                            {
                                sb.AppendLine($"    {FormatSymbolSignature(prop)}");
                            }
                        }
                    }

                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        public List<CodeSymbol> ExtractSymbols(string filePath, string content, string language)
        {
            language = language.ToLowerInvariant();

            return language switch
            {
                "c#" or "csharp" => ExtractCSharpSymbols(content),
                "python" or "py" => ExtractPythonSymbols(content),
                "javascript" or "typescript" or "js" or "ts" => ExtractJavaScriptSymbols(content),
                _ => ExtractGenericSymbols(content, language)
            };
        }

        #region C# Symbol Extraction

        private List<CodeSymbol> ExtractCSharpSymbols(string content)
        {
            var symbols = new List<CodeSymbol>();
            var lines = content.Split('\n');
            string? currentNamespace = null;
            string? currentClass = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                var lineNumber = i + 1;

                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                // Namespace
                var namespaceMatch = Regex.Match(line, @"^\s*namespace\s+([\w\.]+)");
                if (namespaceMatch.Success)
                {
                    currentNamespace = namespaceMatch.Groups[1].Value;
                    symbols.Add(new CodeSymbol
                    {
                        Type = SymbolType.Namespace,
                        Name = currentNamespace,
                        Signature = $"namespace {currentNamespace}",
                        LineNumber = lineNumber
                    });
                    continue;
                }

                // Class
                var classMatch = Regex.Match(line, @"^\s*(public|private|protected|internal)?\s*(static|abstract|sealed)?\s*class\s+(\w+)");
                if (classMatch.Success)
                {
                    currentClass = classMatch.Groups[3].Value;
                    symbols.Add(new CodeSymbol
                    {
                        Type = SymbolType.Class,
                        Name = currentClass,
                        Signature = line.Replace("{", "").Trim(),
                        Visibility = classMatch.Groups[1].Success ? classMatch.Groups[1].Value : "internal",
                        Parent = currentNamespace,
                        LineNumber = lineNumber
                    });
                    continue;
                }

                // Interface
                var interfaceMatch = Regex.Match(line, @"^\s*(public|private|protected|internal)?\s*interface\s+(\w+)");
                if (interfaceMatch.Success)
                {
                    var interfaceName = interfaceMatch.Groups[2].Value;
                    symbols.Add(new CodeSymbol
                    {
                        Type = SymbolType.Interface,
                        Name = interfaceName,
                        Signature = line.Replace("{", "").Trim(),
                        Visibility = interfaceMatch.Groups[1].Success ? interfaceMatch.Groups[1].Value : "internal",
                        Parent = currentNamespace,
                        LineNumber = lineNumber
                    });
                    continue;
                }

                // Method
                var methodMatch = Regex.Match(line, @"^\s*(public|private|protected|internal)?\s*(static|virtual|override|async)?\s*([\w<>,\[\]\?]+)\s+(\w+)\s*\(([^\)]*)\)");
                if (methodMatch.Success && currentClass != null)
                {
                    var returnType = methodMatch.Groups[3].Value;
                    var methodName = methodMatch.Groups[4].Value;
                    var parameters = methodMatch.Groups[5].Value;

                    symbols.Add(new CodeSymbol
                    {
                        Type = SymbolType.Method,
                        Name = methodName,
                        Signature = line.Replace("{", "").Trim().TrimEnd(';'),
                        Visibility = methodMatch.Groups[1].Success ? methodMatch.Groups[1].Value : "private",
                        ReturnType = returnType,
                        Parameters = ParseParameters(parameters),
                        Parent = currentClass,
                        LineNumber = lineNumber
                    });
                    continue;
                }

                // Property
                var propertyMatch = Regex.Match(line, @"^\s*(public|private|protected|internal)?\s*(static|virtual|override)?\s*([\w<>,\[\]\?]+)\s+(\w+)\s*\{\s*(get|set)");
                if (propertyMatch.Success && currentClass != null)
                {
                    var propType = propertyMatch.Groups[3].Value;
                    var propName = propertyMatch.Groups[4].Value;

                    symbols.Add(new CodeSymbol
                    {
                        Type = SymbolType.Property,
                        Name = propName,
                        Signature = $"{propType} {propName}",
                        Visibility = propertyMatch.Groups[1].Success ? propertyMatch.Groups[1].Value : "private",
                        ReturnType = propType,
                        Parent = currentClass,
                        LineNumber = lineNumber
                    });
                }
            }

            return symbols;
        }

        #endregion

        #region Python Symbol Extraction

        private List<CodeSymbol> ExtractPythonSymbols(string content)
        {
            var symbols = new List<CodeSymbol>();
            var lines = content.Split('\n');
            string? currentClass = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmedLine = line.Trim();
                var lineNumber = i + 1;

                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                // Class
                var classMatch = Regex.Match(trimmedLine, @"^class\s+(\w+)(\([^\)]*\))?:");
                if (classMatch.Success)
                {
                    currentClass = classMatch.Groups[1].Value;
                    symbols.Add(new CodeSymbol
                    {
                        Type = SymbolType.Class,
                        Name = currentClass,
                        Signature = trimmedLine.TrimEnd(':'),
                        LineNumber = lineNumber
                    });
                    continue;
                }

                // Function/Method
                var funcMatch = Regex.Match(trimmedLine, @"^(async\s+)?def\s+(\w+)\s*\(([^\)]*)\)(\s*->\s*(.+))?:");
                if (funcMatch.Success)
                {
                    var funcName = funcMatch.Groups[2].Value;
                    var parameters = funcMatch.Groups[3].Value;
                    var returnType = funcMatch.Groups[5].Success ? funcMatch.Groups[5].Value.Trim() : null;

                    symbols.Add(new CodeSymbol
                    {
                        Type = SymbolType.Method,
                        Name = funcName,
                        Signature = trimmedLine.TrimEnd(':'),
                        ReturnType = returnType,
                        Parameters = ParsePythonParameters(parameters),
                        Parent = currentClass,
                        LineNumber = lineNumber
                    });
                }
            }

            return symbols;
        }

        #endregion

        #region JavaScript/TypeScript Symbol Extraction

        private List<CodeSymbol> ExtractJavaScriptSymbols(string content)
        {
            var symbols = new List<CodeSymbol>();
            var lines = content.Split('\n');
            string? currentClass = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                var lineNumber = i + 1;

                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line.StartsWith("/*"))
                    continue;

                // Class
                var classMatch = Regex.Match(line, @"^\s*(export\s+)?(class|interface)\s+(\w+)");
                if (classMatch.Success)
                {
                    currentClass = classMatch.Groups[3].Value;
                    var symbolType = classMatch.Groups[2].Value == "interface" ? SymbolType.Interface : SymbolType.Class;

                    symbols.Add(new CodeSymbol
                    {
                        Type = symbolType,
                        Name = currentClass,
                        Signature = line.Replace("{", "").Trim(),
                        LineNumber = lineNumber
                    });
                    continue;
                }

                // Function (arrow or traditional)
                var funcMatch = Regex.Match(line, @"^\s*(export\s+)?(async\s+)?(function\s+)?(\w+)\s*[=:]?\s*(?:async\s*)?\(([^\)]*)\)(\s*:\s*(\w+))?");
                if (funcMatch.Success)
                {
                    var funcName = funcMatch.Groups[4].Value;
                    var parameters = funcMatch.Groups[5].Value;
                    var returnType = funcMatch.Groups[7].Success ? funcMatch.Groups[7].Value : null;

                    symbols.Add(new CodeSymbol
                    {
                        Type = SymbolType.Method,
                        Name = funcName,
                        Signature = line.Replace("{", "").Replace("=>", "").Trim().TrimEnd(';'),
                        ReturnType = returnType,
                        Parameters = ParseParameters(parameters),
                        Parent = currentClass,
                        LineNumber = lineNumber
                    });
                }
            }

            return symbols;
        }

        #endregion

        #region Generic Symbol Extraction

        private List<CodeSymbol> ExtractGenericSymbols(string content, string language)
        {
            // Fallback: just extract function-like patterns
            var symbols = new List<CodeSymbol>();
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                var lineNumber = i + 1;

                // Try to find function definitions
                var funcMatch = Regex.Match(line, @"(\w+)\s*\([^\)]*\)");
                if (funcMatch.Success && !line.Contains("//") && !line.Contains("#"))
                {
                    symbols.Add(new CodeSymbol
                    {
                        Type = SymbolType.Method,
                        Name = funcMatch.Groups[1].Value,
                        Signature = line.Trim().TrimEnd(';', '{'),
                        LineNumber = lineNumber
                    });
                }
            }

            return symbols;
        }

        #endregion

        #region Helper Methods

        private string FormatSymbolSignature(CodeSymbol symbol)
        {
            var sig = new StringBuilder();

            if (!string.IsNullOrEmpty(symbol.Visibility))
            {
                sig.Append($"{symbol.Visibility} ");
            }

            sig.Append(symbol.Signature);

            if (symbol.Documentation != null)
            {
                sig.Append($" // {symbol.Documentation}");
            }

            return sig.ToString();
        }

        private List<string> ParseParameters(string paramString)
        {
            if (string.IsNullOrWhiteSpace(paramString))
                return new List<string>();

            return paramString
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();
        }

        private List<string> ParsePythonParameters(string paramString)
        {
            if (string.IsNullOrWhiteSpace(paramString))
                return new List<string>();

            return paramString
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p) && p != "self" && p != "cls")
                .ToList();
        }

        private int EstimateIndexTokens(ProjectIndex index)
        {
            // Rough estimate: ~10 tokens per symbol on average
            return index.TotalSymbols * 10 + 500; // 500 for overhead
        }

        #endregion
    }
}
