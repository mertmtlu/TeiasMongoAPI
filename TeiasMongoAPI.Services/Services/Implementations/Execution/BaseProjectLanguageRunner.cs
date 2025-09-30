using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;
using TeiasMongoAPI.Services.Interfaces.Execution;
using TeiasMongoAPI.Core.Models.DTOs;
using TeiasMongoAPI.Services.Services.Implementations;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.Services.Implementations.Execution
{
    public abstract class BaseProjectLanguageRunner : IProjectLanguageRunner
    {
        protected readonly ILogger _logger;
        protected readonly IBsonToDtoMappingService _bsonMapper;
        protected readonly IExecutionOutputStreamingService? _streamingService;

        public abstract string Language { get; }
        public virtual int Priority => 100;

        protected BaseProjectLanguageRunner(ILogger logger, IBsonToDtoMappingService bsonMapper, IExecutionOutputStreamingService? streamingService = null)
        {
            _logger = logger;
            _bsonMapper = bsonMapper;
            _streamingService = streamingService;
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
            Dictionary<string, string>? environment = null, int timeoutMinutes = 2880, CancellationToken cancellationToken = default,
            string? executionId = null)
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

            // COMPREHENSIVE LOGGING: Process execution start
            LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "Starting", startTime, cancellationToken.IsCancellationRequested);

            using var process = new Process { StartInfo = processInfo };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    //_logger.LogInformation(">>> [PYTHON STDOUT]: {Data}", e.Data);
                    output.AppendLine(e.Data);

                    // LIVE STREAMING: Stream stdout output in real-time
                    if (!string.IsNullOrEmpty(executionId) && _streamingService != null)
                    {
                        try
                        {
                            // Fire and forget - don't wait for streaming to complete
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _streamingService.StreamOutputAsync(executionId, e.Data, DateTime.UtcNow);
                                }
                                catch (Exception streamEx)
                                {
                                    _logger.LogTrace(streamEx, "Failed to stream stdout output for execution {ExecutionId}", executionId);
                                }
                            });
                        }
                        catch
                        {
                            // Ignore streaming errors to prevent breaking process execution
                        }
                    }
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    if (e.Data.ToLower().Contains("error"))
                    {
                        //_logger.LogError(">>> [PYTHON STDERR]: {Data}", e.Data);
                        error.AppendLine(e.Data);

                        // LIVE STREAMING: Stream stderr output in real-time
                        if (!string.IsNullOrEmpty(executionId) && _streamingService != null)
                        {
                            try
                            {
                                // Fire and forget - don't wait for streaming to complete
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await _streamingService.StreamErrorAsync(executionId, e.Data, DateTime.UtcNow);
                                    }
                                    catch (Exception streamEx)
                                    {
                                        _logger.LogTrace(streamEx, "Failed to stream stderr output for execution {ExecutionId}", executionId);
                                    }
                                });
                            }
                            catch
                            {
                                // Ignore streaming errors to prevent breaking process execution
                            }
                        }
                    }
                    else
                    {
                        output.AppendLine(e.Data);

                        // LIVE STREAMING: Stream info output as stdout
                        if (!string.IsNullOrEmpty(executionId) && _streamingService != null)
                        {
                            try
                            {
                                // Fire and forget - don't wait for streaming to complete
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await _streamingService.StreamOutputAsync(executionId, e.Data, DateTime.UtcNow);
                                    }
                                    catch (Exception streamEx)
                                    {
                                        _logger.LogTrace(streamEx, "Failed to stream info output for execution {ExecutionId}", executionId);
                                    }
                                });
                            }
                            catch
                            {
                                // Ignore streaming errors to prevent breaking process execution
                            }
                        }
                    }
                }
            };

            try
            {
                _logger.LogDebug("Running: {Executable} {Arguments} in {WorkingDirectory}", executable, arguments, workingDirectory);

                // Register cancellation callback to ensure process is killed immediately when cancellation is triggered
                cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            _logger.LogWarning("Terminating process {Executable} due to cancellation (timeout or user request)", executable);
                            process.Kill(true); // Kill entire process tree
                        }
                    }
                    catch (Exception killEx)
                    {
                        _logger.LogWarning(killEx, "Failed to kill process {Executable} in cancellation callback", executable);
                    }
                });

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // FIXED: Robust handling for long-running processes
                LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "WaitingForExit", DateTime.UtcNow, cancellationToken.IsCancellationRequested);

                try
                {
                    await process.WaitForExitAsync(cancellationToken);
                    LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "WaitCompleted", DateTime.UtcNow, cancellationToken.IsCancellationRequested, processHasExited: process.HasExited);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    var cancelTime = DateTime.UtcNow;
                    var partialDuration = cancelTime - startTime;

                    LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "Cancelled", cancelTime, true,
                        duration: partialDuration, processHasExited: process.HasExited);

                    // Critical fix: Check if process actually completed despite cancellation
                    if (process.HasExited)
                    {
                        LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "CompletedDespiteCancellation",
                            cancelTime, true, exitCode: process.ExitCode, duration: partialDuration, processHasExited: true);
                        // Process completed naturally - continue to return success/failure based on exit code
                    }
                    else
                    {
                        LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "TerminatingAfterCancellation",
                            cancelTime, true, duration: partialDuration, processHasExited: false);

                        // Process still running - kill it and wait for termination
                        process.Kill(true);
                        await process.WaitForExitAsync(CancellationToken.None);

                        var terminationDuration = DateTime.UtcNow - startTime;
                        LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "TerminatedAfterCancellation",
                            DateTime.UtcNow, true, exitCode: -1, duration: terminationDuration, processHasExited: true);

                        return new ProcessResult
                        {
                            ExitCode = -1,
                            Output = output.ToString(),
                            Error = "Process was cancelled due to timeout or user request",
                            Duration = terminationDuration,
                            Success = false
                        };
                    }
                }

                // Process timeout or other cancellation scenarios
                if (!process.HasExited)
                {
                    LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "UnexpectedNotExited",
                        DateTime.UtcNow, cancellationToken.IsCancellationRequested, processHasExited: false);

                    process.Kill(true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }

                var finalEndTime = DateTime.UtcNow;
                var finalDuration = finalEndTime - startTime;

                var result = new ProcessResult
                {
                    ExitCode = process.ExitCode,
                    Output = output.ToString(),
                    Error = error.ToString(),
                    Duration = finalDuration,
                    Success = process.ExitCode == 0
                };

                LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "Completed",
                    finalEndTime, cancellationToken.IsCancellationRequested, exitCode: result.ExitCode,
                    duration: finalDuration, processHasExited: true);

                return result;
            }
            catch (Exception ex)
            {
                var errorTime = DateTime.UtcNow;
                var errorDuration = errorTime - startTime;

                LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "Failed",
                    errorTime, cancellationToken.IsCancellationRequested, exitCode: -1,
                    duration: errorDuration, processHasExited: process.HasExited);

                _logger.LogError(ex, "Process execution failed: {Executable} {Arguments} after {Duration}",
                    executable, arguments, errorDuration);

                // Ensure process is cleaned up even in case of unexpected errors
                try
                {
                    if (!process.HasExited)
                    {
                        LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "CleanupKillAfterError",
                            DateTime.UtcNow, cancellationToken.IsCancellationRequested, processHasExited: false);

                        process.Kill(true);
                        await process.WaitForExitAsync(CancellationToken.None);
                    }
                }
                catch (Exception killEx)
                {
                    _logger.LogWarning(killEx, "Failed to kill process {Executable} during error cleanup", executable);
                }

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

        /// <summary>
        /// COMPREHENSIVE LOGGING: Detailed logging for long-running execution debugging
        /// </summary>
        private void LogProcessExecution(string executable, string arguments, string workingDirectory,
            int timeoutMinutes, string phase, DateTime timestamp, bool cancellationRequested,
            int? exitCode = null, TimeSpan? duration = null, bool? processHasExited = null)
        {
            var logLevel = phase switch
            {
                "Starting" => LogLevel.Information,
                "WaitingForExit" => LogLevel.Debug,
                "Completed" => LogLevel.Information,
                "Cancelled" => LogLevel.Warning,
                "Failed" => LogLevel.Error,
                "Timeout" => LogLevel.Warning,
                _ => LogLevel.Debug
            };

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["ProcessExecutable"] = executable,
                ["ProcessArguments"] = arguments.Length > 100 ? arguments[..100] + "..." : arguments,
                ["WorkingDirectory"] = workingDirectory,
                ["TimeoutMinutes"] = timeoutMinutes,
                ["Phase"] = phase,
                ["Timestamp"] = timestamp.ToString("O"),
                ["CancellationRequested"] = cancellationRequested
            });

            var message = $"Process execution {phase}: {executable} " +
                         $"(timeout: {timeoutMinutes}min, cancellation: {cancellationRequested}";

            if (exitCode.HasValue)
                message += $", exit code: {exitCode.Value}";

            if (duration.HasValue)
                message += $", duration: {duration.Value.TotalMinutes:F2}min";

            if (processHasExited.HasValue)
                message += $", has exited: {processHasExited.Value}";

            message += ")";

            _logger.Log(logLevel, message);

            // Additional detailed logging for critical phases
            if (phase == "Cancelled" || phase == "Timeout" || (phase == "Completed" && duration?.TotalHours >= 1))
            {
                _logger.LogInformation("LONG_RUNNING_EXECUTION_DETAILS: Process {Executable} {Phase} after {Duration} " +
                                     "with cancellation_requested={CancellationRequested}, process_exited={ProcessExited}, exit_code={ExitCode}",
                    executable, phase, duration?.ToString() ?? "unknown", cancellationRequested, processHasExited, exitCode);
            }
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