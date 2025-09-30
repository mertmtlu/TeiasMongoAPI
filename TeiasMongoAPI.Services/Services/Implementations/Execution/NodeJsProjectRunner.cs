using Microsoft.Extensions.Logging;
using System.Text.Json;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;
using TeiasMongoAPI.Services.Services.Implementations;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.Services.Implementations.Execution
{
    public class NodeJsProjectRunner : BaseProjectLanguageRunner
    {
        public override string Language => "Node.js";
        public override int Priority => 40;

        public NodeJsProjectRunner(ILogger<NodeJsProjectRunner> logger, IBsonToDtoMappingService bsonMapper, IExecutionOutputStreamingService? streamingService = null) : base(logger, bsonMapper, streamingService) { }

        public override async Task<bool> CanHandleProjectAsync(string projectDirectory, CancellationToken cancellationToken = default)
        {
            return FileExists(projectDirectory, "package.json") ||
                   FileExists(projectDirectory, "index.js") ||
                   FileExists(projectDirectory, "app.js") ||
                   FileExists(projectDirectory, "server.js") ||
                   FindFiles(projectDirectory, "*.js").Any() ||
                   FindFiles(projectDirectory, "*.ts").Any();
        }

        public override async Task<ProjectStructureAnalysis> AnalyzeProjectAsync(string projectDirectory, CancellationToken cancellationToken = default)
        {
            var analysis = new ProjectStructureAnalysis
            {
                Language = Language,
                ProjectType = "Node.js Application"
            };

            try
            {
                var jsFiles = FindFiles(projectDirectory, "*.js");
                var tsFiles = FindFiles(projectDirectory, "*.ts");

                analysis.SourceFiles.AddRange(jsFiles.Select(f => Path.GetRelativePath(projectDirectory, f)));
                analysis.SourceFiles.AddRange(tsFiles.Select(f => Path.GetRelativePath(projectDirectory, f)));

                if (tsFiles.Any())
                {
                    analysis.Language = "TypeScript";
                    analysis.ProjectType = "TypeScript Application";
                }

                // Analyze package.json
                if (FileExists(projectDirectory, "package.json"))
                {
                    analysis.ConfigFiles.Add("package.json");
                    analysis.HasBuildFile = true;
                    await AnalyzePackageJsonAsync(projectDirectory, analysis, cancellationToken);
                }

                // Check for other config files
                var configFiles = new[] { "tsconfig.json", "webpack.config.js", "gulpfile.js", ".babelrc", "yarn.lock" };
                foreach (var configFile in configFiles)
                {
                    if (FileExists(projectDirectory, configFile))
                    {
                        analysis.ConfigFiles.Add(configFile);
                    }
                }

                // Find entry points
                analysis.EntryPoints = FindEntryPoints(projectDirectory);
                analysis.MainEntryPoint = analysis.EntryPoints.FirstOrDefault();

                analysis.Metadata["jsFiles"] = jsFiles.Count;
                analysis.Metadata["tsFiles"] = tsFiles.Count;
                analysis.Metadata["hasNodeModules"] = Directory.Exists(Path.Combine(projectDirectory, "node_modules"));

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze Node.js project in {ProjectDirectory}", projectDirectory);
                return analysis;
            }
        }

        public override async Task<ProjectBuildResult> BuildProjectAsync(string projectDirectory, ProjectBuildArgs buildArgs, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Installing Node.js dependencies in {ProjectDirectory}", projectDirectory);

                var result = new ProjectBuildResult { Success = true };

                // Install dependencies
                var packageManager = GetPackageManager(projectDirectory);
                var installCommand = packageManager == "yarn" ? "install" : "install";

                var installResult = await RunProcessAsync(
                    packageManager,
                    installCommand,
                    projectDirectory,
                    buildArgs.BuildEnvironment,
                    buildArgs.BuildTimeoutMinutes,
                    cancellationToken);

                result.Output = installResult.Output;
                result.ErrorOutput = installResult.Error;
                result.Duration = installResult.Duration;

                if (!installResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to install Node.js dependencies";
                    return result;
                }

                // Run build script if available
                var packageJsonPath = Path.Combine(projectDirectory, "package.json");
                if (File.Exists(packageJsonPath))
                {
                    var packageContent = await File.ReadAllTextAsync(packageJsonPath, cancellationToken);
                    var packageDoc = JsonDocument.Parse(packageContent);

                    if (packageDoc.RootElement.TryGetProperty("scripts", out var scripts) &&
                        scripts.TryGetProperty("build", out var buildScript))
                    {
                        var buildResult = await RunProcessAsync(
                            packageManager,
                            packageManager == "yarn" ? "build" : "run build",
                            projectDirectory,
                            buildArgs.BuildEnvironment,
                            buildArgs.BuildTimeoutMinutes,
                            cancellationToken);

                        result.Output += "\n" + buildResult.Output;
                        result.ErrorOutput += "\n" + buildResult.Error;

                        if (!buildResult.Success)
                        {
                            result.Success = false;
                            result.ErrorMessage = "Build script failed";
                            return result;
                        }
                    }
                }

                _logger.LogInformation("Node.js project built successfully");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Build failed for Node.js project in {ProjectDirectory}", projectDirectory);
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
                _logger.LogInformation("Executing Node.js project in {ProjectDirectory}", context.ProjectDirectory);

                var entryPoint = context.ProjectStructure.MainEntryPoint ?? FindBestEntryPoint(context.ProjectDirectory);

                if (string.IsNullOrEmpty(entryPoint))
                {
                    result.Success = false;
                    result.ErrorMessage = "No Node.js entry point found";
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
                    "node",
                    arguments,
                    context.ProjectDirectory,
                    context.Environment,
                    2880, // Timeout managed by ProjectExecutionEngine using appsettings
                    cancellationToken,
                    context.ExecutionId);

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

                _logger.LogInformation("Node.js project execution completed with exit code {ExitCode}", result.ExitCode);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Execution failed for Node.js project in {ProjectDirectory}", context.ProjectDirectory);
                result.Success = false;
                result.ExitCode = -1;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;
                return result;
            }
        }

        private async Task AnalyzePackageJsonAsync(string projectDirectory, ProjectStructureAnalysis analysis, CancellationToken cancellationToken)
        {
            var packageJsonPath = Path.Combine(projectDirectory, "package.json");
            var content = await File.ReadAllTextAsync(packageJsonPath, cancellationToken);

            try
            {
                var packageDoc = JsonDocument.Parse(content);
                var root = packageDoc.RootElement;

                // Get main entry point
                if (root.TryGetProperty("main", out var main))
                {
                    analysis.MainEntryPoint = main.GetString() ?? "index.js";
                }

                // Get dependencies
                if (root.TryGetProperty("dependencies", out var deps))
                {
                    foreach (var dep in deps.EnumerateObject())
                    {
                        analysis.Dependencies.Add(dep.Name);
                    }
                }

                // Determine project type
                if (analysis.Dependencies.Contains("express"))
                {
                    analysis.ProjectType = "Express.js Web Application";
                }
                else if (analysis.Dependencies.Contains("react"))
                {
                    analysis.ProjectType = "React Application";
                }
                else if (analysis.Dependencies.Contains("vue"))
                {
                    analysis.ProjectType = "Vue.js Application";
                }
                else if (analysis.Dependencies.Contains("angular"))
                {
                    analysis.ProjectType = "Angular Application";
                }

                // Get scripts
                if (root.TryGetProperty("scripts", out var scripts))
                {
                    var scriptNames = scripts.EnumerateObject().Select(s => s.Name).ToList();
                    analysis.Metadata["scripts"] = scriptNames;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse package.json in {ProjectDirectory}", projectDirectory);
            }
        }

        private List<string> FindEntryPoints(string projectDirectory)
        {
            var entryPoints = new List<string>();
            var commonEntryPoints = new[] { "index.js", "app.js", "server.js", "main.js", "start.js" };

            foreach (var entryPoint in commonEntryPoints)
            {
                if (File.Exists(Path.Combine(projectDirectory, entryPoint)))
                {
                    entryPoints.Add(entryPoint);
                }
            }

            return entryPoints;
        }

        private string FindBestEntryPoint(string projectDirectory)
        {
            var entryPoints = new[] { "index.js", "app.js", "server.js", "main.js", "start.js" };

            foreach (var entryPoint in entryPoints)
            {
                if (File.Exists(Path.Combine(projectDirectory, entryPoint)))
                {
                    return entryPoint;
                }
            }

            return string.Empty;
        }

        private string GetPackageManager(string projectDirectory)
        {
            if (File.Exists(Path.Combine(projectDirectory, "yarn.lock")))
            {
                return "yarn";
            }
            return "npm";
        }

        private long EstimateMemoryUsage(string? output)
        {
            var outputSize = output?.Length ?? 0;
            return Math.Max(outputSize * 10, 40 * 1024 * 1024); // Minimum 40MB
        }
    }
}