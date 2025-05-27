using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;

namespace TeiasMongoAPI.Services.Services.Implementations.Execution
{
    public class PythonProjectRunner : BaseProjectLanguageRunner
    {
        public override string Language => "Python";
        public override int Priority => 20;

        public PythonProjectRunner(ILogger<PythonProjectRunner> logger) : base(logger) { }

        public override async Task<bool> CanHandleProjectAsync(string projectDirectory, CancellationToken cancellationToken = default)
        {
            return FileExists(projectDirectory, "requirements.txt") ||
                   FileExists(projectDirectory, "setup.py") ||
                   FileExists(projectDirectory, "pyproject.toml") ||
                   FileExists(projectDirectory, "main.py") ||
                   FileExists(projectDirectory, "__main__.py") ||
                   FindFiles(projectDirectory, "*.py").Any();
        }

        public override async Task<ProjectStructureAnalysis> AnalyzeProjectAsync(string projectDirectory, CancellationToken cancellationToken = default)
        {
            var analysis = new ProjectStructureAnalysis
            {
                Language = Language,
                ProjectType = "Python Application"
            };

            try
            {
                // Find Python files
                var pyFiles = FindFiles(projectDirectory, "*.py");
                analysis.SourceFiles.AddRange(pyFiles.Select(f => Path.GetRelativePath(projectDirectory, f)));

                // Find configuration files
                var configFiles = new[] { "requirements.txt", "setup.py", "pyproject.toml", "Pipfile", "poetry.lock" };
                foreach (var configFile in configFiles)
                {
                    if (FileExists(projectDirectory, configFile))
                    {
                        analysis.ConfigFiles.Add(configFile);
                        analysis.HasBuildFile = true;
                    }
                }

                // Analyze dependencies
                await AnalyzeDependenciesAsync(projectDirectory, analysis, cancellationToken);

                // Find entry points
                analysis.EntryPoints = FindEntryPoints(projectDirectory, pyFiles);
                analysis.MainEntryPoint = analysis.EntryPoints.FirstOrDefault();

                // Determine project type
                if (pyFiles.Any(f => f.Contains("django") || f.Contains("manage.py")))
                {
                    analysis.ProjectType = "Django Web Application";
                }
                else if (pyFiles.Any(f => f.Contains("flask") || f.Contains("app.py")))
                {
                    analysis.ProjectType = "Flask Web Application";
                }
                else if (pyFiles.Any(f => f.Contains("fastapi")))
                {
                    analysis.ProjectType = "FastAPI Application";
                }
                else if (FileExists(projectDirectory, "setup.py"))
                {
                    analysis.ProjectType = "Python Package";
                }

                analysis.Metadata["pythonFiles"] = pyFiles.Count;
                analysis.Metadata["hasVirtualEnv"] = Directory.Exists(Path.Combine(projectDirectory, "venv")) ||
                                                     Directory.Exists(Path.Combine(projectDirectory, ".venv"));

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze Python project in {ProjectDirectory}", projectDirectory);
                return analysis;
            }
        }

        public override async Task<ProjectBuildResult> BuildProjectAsync(string projectDirectory, ProjectBuildArgs buildArgs, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Installing Python dependencies in {ProjectDirectory}", projectDirectory);

                var result = new ProjectBuildResult { Success = true };

                // Install dependencies if requirements.txt exists
                if (FileExists(projectDirectory, "requirements.txt"))
                {
                    var installResult = await RunProcessAsync(
                        "pip",
                        "install -r requirements.txt",
                        projectDirectory,
                        buildArgs.BuildEnvironment,
                        buildArgs.BuildTimeoutMinutes,
                        cancellationToken);

                    result.Output += installResult.Output;
                    result.ErrorOutput += installResult.Error;
                    result.Duration = installResult.Duration;

                    if (!installResult.Success)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Failed to install Python dependencies";
                        return result;
                    }
                }

                // Handle setup.py installation
                if (FileExists(projectDirectory, "setup.py"))
                {
                    var setupResult = await RunProcessAsync(
                        "python",
                        "setup.py develop",
                        projectDirectory,
                        buildArgs.BuildEnvironment,
                        buildArgs.BuildTimeoutMinutes,
                        cancellationToken);

                    result.Output += setupResult.Output;
                    result.ErrorOutput += setupResult.Error;

                    if (!setupResult.Success)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Failed to install package in development mode";
                        return result;
                    }
                }

                _logger.LogInformation("Python dependencies installed successfully");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Build failed for Python project in {ProjectDirectory}", projectDirectory);
                return new ProjectBuildResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public override async Task<ProjectExecutionResult> ExecuteAsync(ProjectExecutionContext context, CancellationToken cancellationToken = default)
        {
            var result = new ProjectExecutionResult
            {
                ExecutionId = context.ExecutionId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Executing Python project in {ProjectDirectory}", context.ProjectDirectory);

                var entryPoint = context.ProjectStructure.MainEntryPoint ?? "main.py";
                if (!File.Exists(Path.Combine(context.ProjectDirectory, entryPoint)))
                {
                    entryPoint = FindBestEntryPoint(context.ProjectDirectory);
                }

                if (string.IsNullOrEmpty(entryPoint))
                {
                    result.Success = false;
                    result.ErrorMessage = "No Python entry point found";
                    return result;
                }

                var arguments = entryPoint;

                // Add parameters if any
                if (context.Parameters != null && context.Parameters.ToString() != "{}")
                {
                    var paramJson = JsonSerializer.Serialize(context.Parameters);
                    arguments += $" '{paramJson}'";
                }

                var processResult = await RunProcessAsync(
                    "python",
                    arguments,
                    context.ProjectDirectory,
                    context.Environment,
                    context.ResourceLimits.MaxExecutionTimeMinutes,
                    cancellationToken);

                result.Success = processResult.Success;
                result.ExitCode = processResult.ExitCode;
                result.Output = processResult.Output;
                result.ErrorOutput = processResult.Error;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;

                result.ResourceUsage = new ProjectResourceUsage
                {
                    CpuTimeSeconds = result.Duration?.TotalSeconds ?? 0,
                    PeakMemoryBytes = EstimateMemoryUsage(result.Output),
                    OutputSizeBytes = (result.Output?.Length ?? 0) + (result.ErrorOutput?.Length ?? 0)
                };

                _logger.LogInformation("Python project execution completed with exit code {ExitCode}", result.ExitCode);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Execution failed for Python project in {ProjectDirectory}", context.ProjectDirectory);
                result.Success = false;
                result.ExitCode = -1;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;
                return result;
            }
        }

        protected override async Task ValidateLanguageSpecificAsync(string projectDirectory, ProjectValidationResult result, CancellationToken cancellationToken)
        {
            // Check for Python installation
            var pythonCheck = await RunProcessAsync("python", "--version", projectDirectory, cancellationToken: cancellationToken);
            if (!pythonCheck.Success)
            {
                result.Errors.Add("Python is not installed or not accessible");
                result.IsValid = false;
                return;
            }

            // Check pip
            var pipCheck = await RunProcessAsync("pip", "--version", projectDirectory, cancellationToken: cancellationToken);
            if (!pipCheck.Success)
            {
                result.Warnings.Add("pip is not accessible - dependency installation may fail");
            }

            // Validate Python files syntax
            var pyFiles = FindFiles(projectDirectory, "*.py");
            foreach (var pyFile in pyFiles.Take(10)) // Limit to first 10 files for performance
            {
                var syntaxCheck = await RunProcessAsync("python", $"-m py_compile \"{pyFile}\"", projectDirectory, cancellationToken: cancellationToken);
                if (!syntaxCheck.Success)
                {
                    result.Warnings.Add($"Syntax error in {Path.GetFileName(pyFile)}");
                }
            }
        }

        private async Task AnalyzeDependenciesAsync(string projectDirectory, ProjectStructureAnalysis analysis, CancellationToken cancellationToken)
        {
            // Parse requirements.txt
            var requirementsFile = Path.Combine(projectDirectory, "requirements.txt");
            if (File.Exists(requirementsFile))
            {
                var content = await File.ReadAllLinesAsync(requirementsFile, cancellationToken);
                foreach (var line in content)
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                    {
                        var packageName = trimmed.Split(new[] { '=', '>', '<', '!' }, StringSplitOptions.RemoveEmptyEntries)[0];
                        analysis.Dependencies.Add(packageName);
                    }
                }
            }

            // Parse setup.py for dependencies
            var setupFile = Path.Combine(projectDirectory, "setup.py");
            if (File.Exists(setupFile))
            {
                var content = await File.ReadAllTextAsync(setupFile, cancellationToken);
                var installRequiresMatch = Regex.Match(content, @"install_requires\s*=\s*\[(.*?)\]", RegexOptions.Singleline);
                if (installRequiresMatch.Success)
                {
                    var requirements = installRequiresMatch.Groups[1].Value;
                    var packageMatches = Regex.Matches(requirements, @"['""]([^'""]+)['""]");
                    foreach (Match match in packageMatches)
                    {
                        var packageName = match.Groups[1].Value.Split(new[] { '=', '>', '<', '!' }, StringSplitOptions.RemoveEmptyEntries)[0];
                        if (!analysis.Dependencies.Contains(packageName))
                        {
                            analysis.Dependencies.Add(packageName);
                        }
                    }
                }
            }
        }

        private List<string> FindEntryPoints(string projectDirectory, List<string> pyFiles)
        {
            var entryPoints = new List<string>();

            // Common entry point names
            var commonEntryPoints = new[] { "main.py", "__main__.py", "app.py", "run.py", "start.py" };

            foreach (var entryPoint in commonEntryPoints)
            {
                if (pyFiles.Any(f => f.EndsWith(entryPoint)))
                {
                    entryPoints.Add(entryPoint);
                }
            }

            // Look for files with if __name__ == "__main__":
            foreach (var pyFile in pyFiles)
            {
                try
                {
                    var content = File.ReadAllText(pyFile);
                    if (content.Contains("if __name__ == \"__main__\":") || content.Contains("if __name__ == '__main__':"))
                    {
                        var relativePath = Path.GetRelativePath(projectDirectory, pyFile);
                        if (!entryPoints.Contains(relativePath))
                        {
                            entryPoints.Add(relativePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read Python file {PyFile}", pyFile);
                }
            }

            return entryPoints;
        }

        private string FindBestEntryPoint(string projectDirectory)
        {
            var entryPoints = new[] { "main.py", "__main__.py", "app.py", "run.py", "start.py" };

            foreach (var entryPoint in entryPoints)
            {
                if (File.Exists(Path.Combine(projectDirectory, entryPoint)))
                {
                    return entryPoint;
                }
            }

            return string.Empty;
        }

        private long EstimateMemoryUsage(string? output)
        {
            var outputSize = output?.Length ?? 0;
            return Math.Max(outputSize * 8, 30 * 1024 * 1024); // Minimum 30MB
        }
    }
}