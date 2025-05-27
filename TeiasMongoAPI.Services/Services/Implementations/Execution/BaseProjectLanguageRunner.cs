using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;
using TeiasMongoAPI.Services.Interfaces.Execution;

namespace TeiasMongoAPI.Services.Services.Implementations.Execution
{
    public abstract class BaseProjectLanguageRunner : IProjectLanguageRunner
    {
        protected readonly ILogger _logger;

        public abstract string Language { get; }
        public virtual int Priority => 100;

        protected BaseProjectLanguageRunner(ILogger logger)
        {
            _logger = logger;
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
            Dictionary<string, string>? environment = null, int timeoutMinutes = 30, CancellationToken cancellationToken = default)
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