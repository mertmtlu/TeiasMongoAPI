using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;
using TeiasMongoAPI.Services.Interfaces.Execution;
using TeiasMongoAPI.Core.Models.DTOs;
using TeiasMongoAPI.Services.Services.Implementations;

namespace TeiasMongoAPI.Services.Services.Implementations.Execution
{
    public abstract class BaseProjectLanguageRunner : IProjectLanguageRunner
    {
        protected readonly ILogger _logger;
        protected readonly IBsonToDtoMappingService _bsonMapper;

        public abstract string Language { get; }
        public virtual int Priority => 100;

        protected BaseProjectLanguageRunner(ILogger logger, IBsonToDtoMappingService bsonMapper)
        {
            _logger = logger;
            _bsonMapper = bsonMapper;
        }

        public abstract Task<bool> CanHandleProjectAsync(string projectDirectory, CancellationToken cancellationToken = default);
        public abstract Task<ProjectStructureAnalysis> AnalyzeProjectAsync(string projectDirectory, CancellationToken cancellationToken = default);
        public abstract Task<ProjectBuildResult> BuildProjectAsync(string projectDirectory, ProjectBuildArgs buildArgs, CancellationToken cancellationToken = default);
        public abstract Task<ProjectExecutionResult> ExecuteAsync(ProjectExecutionContext context, CancellationToken cancellationToken = default);

        public virtual async Task<ProjectValidationResult> ValidateProjectAsync(string projectDirectory, CancellationToken cancellationToken = default)
        {
            var result = new ProjectValidationResult { IsValid = true };

            try
            {
                // Basic validation
                if (!Directory.Exists(projectDirectory))
                {
                    result.IsValid = false;
                    result.Errors.Add("Project directory does not exist");
                    return result;
                }

                // Language-specific validation
                await ValidateLanguageSpecificAsync(projectDirectory, result, cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Validation failed for project in {ProjectDirectory}", projectDirectory);
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
                return result;
            }
        }

        protected virtual Task ValidateLanguageSpecificAsync(string projectDirectory, ProjectValidationResult result, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        protected async Task<ProcessResult> RunProcessAsync(string executable, string arguments, string workingDirectory,
            Dictionary<string, string>? environment = null, int timeoutMinutes = 2880, CancellationToken cancellationToken = default)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Add environment variables
            if (environment != null)
            {
                foreach (var env in environment)
                {
                    processInfo.Environment[env.Key] = env.Value;
                }
            }

            var output = new StringBuilder();
            var error = new StringBuilder();
            var startTime = DateTime.UtcNow;

            using var process = new Process { StartInfo = processInfo };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    error.AppendLine(e.Data);
                }
            };

            try
            {
                _logger.LogDebug("Running: {Executable} {Arguments} in {WorkingDirectory}", executable, arguments, workingDirectory);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken); //TODO: CHECK HERE!!

                // Process timeout or cancellation
                if (!process.HasExited)
                {
                    process.Kill(true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }

                var endTime = DateTime.UtcNow;
                var duration = endTime - startTime;

                return new ProcessResult
                {
                    ExitCode = process.ExitCode,
                    Output = output.ToString(),
                    Error = error.ToString(),
                    Duration = duration,
                    Success = process.ExitCode == 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Process execution failed: {Executable} {Arguments}", executable, arguments);
                return new ProcessResult
                {
                    ExitCode = -1,
                    Error = ex.Message,
                    Duration = DateTime.UtcNow - startTime,
                    Success = false
                };
            }
        }

        protected List<string> FindFiles(string directory, string pattern, SearchOption searchOption = SearchOption.AllDirectories)
        {
            try
            {
                return Directory.GetFiles(directory, pattern, searchOption).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to find files with pattern {Pattern} in {Directory}", pattern, directory);
                return new List<string>();
            }
        }

        protected bool FileExists(string directory, string fileName)
        {
            return File.Exists(Path.Combine(directory, fileName));
        }

        protected async Task<string> ReadFileIfExistsAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (File.Exists(filePath))
            {
                return await File.ReadAllTextAsync(filePath, cancellationToken);
            }
            return string.Empty;
        }

        /// <summary>
        /// FIXED: Safe parameter processing with proper BSON to JSON conversion
        /// </summary>
        protected async Task ProcessInputFilesFromParametersAsync(ProjectExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                // CRITICAL FIX: Use DTO mapping service instead of direct JsonSerializer
                var contextDto = _bsonMapper.MapToExecutionContextDto(
                    context.Parameters, 
                    context.ExecutionId, 
                    context.ProjectDirectory);

                if (contextDto.InputFiles != null && contextDto.InputFiles.Any())
                {
                    // Create input directory in project
                    var inputDir = Path.Combine(context.ProjectDirectory, "input");
                    Directory.CreateDirectory(inputDir);

                    foreach (var inputFile in contextDto.InputFiles)
                    {
                        if (!string.IsNullOrEmpty(inputFile.Content))
                        {
                            var filePath = Path.Combine(inputDir, inputFile.Name);
                            var content = Convert.FromBase64String(inputFile.Content);
                            await File.WriteAllBytesAsync(filePath, content, cancellationToken);

                            _logger.LogDebug("Created input file: {FilePath} ({Size} bytes)",
                                filePath, content.Length);
                        }
                    }

                    _logger.LogInformation("Processed {FileCount} input files for execution {ExecutionId}",
                        contextDto.InputFiles.Count, context.ExecutionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process input files from parameters for execution {ExecutionId}", context.ExecutionId);
                // Don't fail the execution, just log the warning
            }
        }

        /// <summary>
        /// FIXED: Safe parameter processing for external scripts
        /// </summary>
        protected object ProcessParametersForExecution(object parameters, string projectDirectory)
        {
            try
            {
                // CRITICAL FIX: Convert to JSON-safe DTO before serialization
                var contextDto = _bsonMapper.MapToExecutionContextDto(parameters, Guid.NewGuid().ToString(), projectDirectory);
                var parametersDict = contextDto.Parameters;

                if (parametersDict == null)
                {
                    return parameters;
                }

                // Process input files using DTO service
                if (contextDto.InputFiles != null && contextDto.InputFiles.Any())
                {
                    var filePaths = contextDto.InputFiles.Select(f => $"./input/{f.Name}").ToList();
                    var fileNames = contextDto.InputFiles.Select(f => f.Name).ToList();

                    parametersDict["inputFiles"] = filePaths;
                    parametersDict["inputFileNames"] = fileNames;
                    parametersDict["inputDirectory"] = "./input";
                    parametersDict["hasInputFiles"] = true;
                }
                else
                {
                    parametersDict["hasInputFiles"] = false;
                }

                // Process table elements - flatten nested table data
                var processedParameters = new Dictionary<string, object>();

                foreach (var kvp in parametersDict)
                {
                    if (kvp.Key.StartsWith("file_input")) continue;
                    if (kvp.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                    {
                        // Check if this might be a table element (nested object structure)
                        var nestedDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());

                        if (nestedDict != null && IsTableElement(nestedDict))
                        {
                            // This is a table element - flatten it
                            foreach (var cellKvp in nestedDict)
                            {
                                if (cellKvp.Key == "content") continue;
                                var cellKey = $"{kvp.Key}_{cellKvp.Key}";
                                processedParameters[cellKey] = cellKvp.Value;
                            }

                            // Also keep the original table structure for backward compatibility
                            processedParameters[kvp.Key] = nestedDict;
                        }
                        else
                        {
                            // Regular nested object, keep as is
                            processedParameters[kvp.Key] = nestedDict;
                        }
                    }
                    else
                    {
                        // Regular parameter, keep as is
                        processedParameters[kvp.Key] = kvp.Value;
                    }
                }

                return processedParameters;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process parameters for execution");
                return parameters; // Return original if processing fails
            }
        }

        private bool IsTableElement(Dictionary<string, object> dict)
        {
            // Heuristic to determine if this is a table element:
            // 1. All keys should be cell IDs (like "a1", "b1") or custom names
            // 2. Values should be simple types (strings, numbers, booleans)
            // 3. Should have multiple entries (tables typically have multiple cells)

            if (dict.Count == 0)
                return false;

            // Check if all values are simple types (not nested objects)
            foreach (var kvp in dict)
            {
                if (kvp.Value is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.Object || jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        return false; // Complex nested structure, probably not a table
                    }
                }
                else if (kvp.Value is Dictionary<string, object> || kvp.Value is List<object>)
                {
                    return false; // Complex nested structure, probably not a table
                }
            }

            // Additional heuristics could be added here:
            // - Check for cell ID patterns (a1, b1, etc.)
            // - Check for common table-like key patterns
            // - Check against known table element names from component configuration

            return true; // Likely a table element
        }
        protected class InputFileData
        {
            public string Name { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty; // Base64 encoded
            public string ContentType { get; set; } = string.Empty;
            public long Size { get; set; }
        }

        protected class ProcessResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
            public TimeSpan Duration { get; set; }
            public bool Success { get; set; }
        }
    }
}