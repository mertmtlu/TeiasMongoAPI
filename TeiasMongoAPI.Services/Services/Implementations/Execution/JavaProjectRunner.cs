using Microsoft.Extensions.Logging;
using System.Text.Json;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;
using TeiasMongoAPI.Services.Services.Implementations;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.Services.Implementations.Execution
{
    public class JavaProjectRunner : BaseProjectLanguageRunner
    {
        public override string Language => "Java";
        public override int Priority => 30;

        public JavaProjectRunner(ILogger<JavaProjectRunner> logger, IBsonToDtoMappingService bsonMapper, IExecutionOutputStreamingService? streamingService = null) : base(logger, bsonMapper, streamingService) { }

        public override async Task<bool> CanHandleProjectAsync(string projectDirectory, CancellationToken cancellationToken = default)
        {
            return FileExists(projectDirectory, "pom.xml") ||
                   FileExists(projectDirectory, "build.gradle") ||
                   FileExists(projectDirectory, "gradle.properties") ||
                   FindFiles(projectDirectory, "*.java").Any();
        }

        public override async Task<ProjectStructureAnalysis> AnalyzeProjectAsync(string projectDirectory, CancellationToken cancellationToken = default)
        {
            var analysis = new ProjectStructureAnalysis
            {
                Language = Language,
                ProjectType = "Java Application"
            };

            try
            {
                var javaFiles = FindFiles(projectDirectory, "*.java");
                analysis.SourceFiles.AddRange(javaFiles.Select(f => Path.GetRelativePath(projectDirectory, f)));

                // Check for Maven
                if (FileExists(projectDirectory, "pom.xml"))
                {
                    analysis.ConfigFiles.Add("pom.xml");
                    analysis.HasBuildFile = true;
                    analysis.ProjectType = "Maven Java Application";
                    await AnalyzeMavenProjectAsync(projectDirectory, analysis, cancellationToken);
                }
                // Check for Gradle
                else if (FileExists(projectDirectory, "build.gradle") || FileExists(projectDirectory, "build.gradle.kts"))
                {
                    analysis.ConfigFiles.Add("build.gradle");
                    analysis.HasBuildFile = true;
                    analysis.ProjectType = "Gradle Java Application";
                    await AnalyzeGradleProjectAsync(projectDirectory, analysis, cancellationToken);
                }

                // Find main methods
                analysis.EntryPoints = await FindMainMethodsAsync(projectDirectory, javaFiles, cancellationToken);
                analysis.MainEntryPoint = analysis.EntryPoints.FirstOrDefault();

                analysis.Metadata["javaFiles"] = javaFiles.Count;
                analysis.Metadata["buildTool"] = analysis.HasBuildFile ?
                    (FileExists(projectDirectory, "pom.xml") ? "Maven" : "Gradle") : "None";

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze Java project in {ProjectDirectory}", projectDirectory);
                return analysis;
            }
        }

        public override async Task<ProjectBuildResult> BuildProjectAsync(string projectDirectory, ProjectBuildArgs buildArgs, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Building Java project in {ProjectDirectory}", projectDirectory);

                if (FileExists(projectDirectory, "pom.xml"))
                {
                    return await BuildMavenProjectAsync(projectDirectory, buildArgs, cancellationToken);
                }
                else if (FileExists(projectDirectory, "build.gradle") || FileExists(projectDirectory, "build.gradle.kts"))
                {
                    return await BuildGradleProjectAsync(projectDirectory, buildArgs, cancellationToken);
                }
                else
                {
                    return await BuildPlainJavaProjectAsync(projectDirectory, buildArgs, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Build failed for Java project in {ProjectDirectory}", projectDirectory);
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
                _logger.LogInformation("Executing Java project in {ProjectDirectory}", context.ProjectDirectory);

                ProcessResult processResult;

                if (FileExists(context.ProjectDirectory, "pom.xml"))
                {
                    processResult = await ExecuteMavenProjectAsync(context, cancellationToken);
                }
                else if (FileExists(context.ProjectDirectory, "build.gradle"))
                {
                    processResult = await ExecuteGradleProjectAsync(context, cancellationToken);
                }
                else
                {
                    processResult = await ExecutePlainJavaProjectAsync(context, cancellationToken);
                }

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

                _logger.LogInformation("Java project execution completed with exit code {ExitCode}", result.ExitCode);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Execution failed for Java project in {ProjectDirectory}", context.ProjectDirectory);
                result.Success = false;
                result.ExitCode = -1;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;
                return result;
            }
        }

        private async Task<ProjectBuildResult> BuildMavenProjectAsync(string projectDirectory, ProjectBuildArgs buildArgs, CancellationToken cancellationToken)
        {
            var buildArgs_ = buildArgs.AdditionalArgs.Any() ? string.Join(" ", buildArgs.AdditionalArgs) : "";
            var compileResult = await RunProcessAsync(
                "mvn",
                $"clean compile {buildArgs_}",
                projectDirectory,
                buildArgs.BuildEnvironment,
                buildArgs.BuildTimeoutMinutes,
                cancellationToken);

            return new ProjectBuildResult
            {
                Success = compileResult.Success,
                Output = compileResult.Output,
                ErrorOutput = compileResult.Error,
                Duration = compileResult.Duration,
                ErrorMessage = compileResult.Success ? null : "Maven build failed"
            };
        }

        private async Task<ProjectBuildResult> BuildGradleProjectAsync(string projectDirectory, ProjectBuildArgs buildArgs, CancellationToken cancellationToken)
        {
            var gradle = IsWindows() ? "gradlew.bat" : "./gradlew";
            if (!File.Exists(Path.Combine(projectDirectory, gradle.TrimStart('.', '/'))))
            {
                gradle = "gradle";
            }

            var buildResult = await RunProcessAsync(
                gradle,
                "build",
                projectDirectory,
                buildArgs.BuildEnvironment,
                buildArgs.BuildTimeoutMinutes,
                cancellationToken);

            return new ProjectBuildResult
            {
                Success = buildResult.Success,
                Output = buildResult.Output,
                ErrorOutput = buildResult.Error,
                Duration = buildResult.Duration,
                ErrorMessage = buildResult.Success ? null : "Gradle build failed"
            };
        }

        private async Task<ProjectBuildResult> BuildPlainJavaProjectAsync(string projectDirectory, ProjectBuildArgs buildArgs, CancellationToken cancellationToken)
        {
            // Simple javac compilation
            var javaFiles = FindFiles(projectDirectory, "*.java");
            if (!javaFiles.Any())
            {
                return new ProjectBuildResult
                {
                    Success = false,
                    ErrorMessage = "No Java source files found"
                };
            }

            var srcDir = Path.Combine(projectDirectory, "src");
            var binDir = Path.Combine(projectDirectory, "bin");
            Directory.CreateDirectory(binDir);

            var sourceFiles = string.Join(" ", javaFiles.Select(f => $"\"{f}\""));
            var compileResult = await RunProcessAsync(
                "javac",
                $"-d \"{binDir}\" {sourceFiles}",
                projectDirectory,
                buildArgs.BuildEnvironment,
                buildArgs.BuildTimeoutMinutes,
                cancellationToken);

            return new ProjectBuildResult
            {
                Success = compileResult.Success,
                Output = compileResult.Output,
                ErrorOutput = compileResult.Error,
                Duration = compileResult.Duration,
                ErrorMessage = compileResult.Success ? null : "Java compilation failed"
            };
        }

        private async Task<ProcessResult> ExecuteMavenProjectAsync(ProjectExecutionContext context, CancellationToken cancellationToken)
        {
            return await RunProcessAsync(
                "mvn",
                "exec:java",
                context.ProjectDirectory,
                context.Environment,
                2880, // Timeout managed by ProjectExecutionEngine using appsettings
                cancellationToken,
                context.ExecutionId);
        }

        private async Task<ProcessResult> ExecuteGradleProjectAsync(ProjectExecutionContext context, CancellationToken cancellationToken)
        {
            var gradle = IsWindows() ? "gradlew.bat" : "./gradlew";
            if (!File.Exists(Path.Combine(context.ProjectDirectory, gradle.TrimStart('.', '/'))))
            {
                gradle = "gradle";
            }

            return await RunProcessAsync(
                gradle,
                "run",
                context.ProjectDirectory,
                context.Environment,
                2880, // Timeout managed by ProjectExecutionEngine using appsettings
                cancellationToken,
                context.ExecutionId);
        }

        private async Task<ProcessResult> ExecutePlainJavaProjectAsync(ProjectExecutionContext context, CancellationToken cancellationToken)
        {
            var mainClass = FindMainClass(context.ProjectDirectory);
            if (string.IsNullOrEmpty(mainClass))
            {
                return new ProcessResult
                {
                    Success = false,
                    ExitCode = -1,
                    Error = "No main class found"
                };
            }

            var binDir = Path.Combine(context.ProjectDirectory, "bin");
            return await RunProcessAsync(
                "java",
                $"-cp \"{binDir}\" {mainClass}",
                context.ProjectDirectory,
                context.Environment,
                2880, // Timeout managed by ProjectExecutionEngine using appsettings
                cancellationToken,
                context.ExecutionId);
        }

        private async Task AnalyzeMavenProjectAsync(string projectDirectory, ProjectStructureAnalysis analysis, CancellationToken cancellationToken)
        {
            // Parse pom.xml for dependencies (simplified)
            var pomPath = Path.Combine(projectDirectory, "pom.xml");
            var content = await File.ReadAllTextAsync(pomPath, cancellationToken);

            // This is a simplified dependency extraction - would need proper XML parsing in production
            if (content.Contains("spring-boot"))
            {
                analysis.ProjectType = "Spring Boot Application";
                analysis.Dependencies.Add("Spring Boot");
            }
        }

        private async Task AnalyzeGradleProjectAsync(string projectDirectory, ProjectStructureAnalysis analysis, CancellationToken cancellationToken)
        {
            var buildGradlePath = Path.Combine(projectDirectory, "build.gradle");
            if (File.Exists(buildGradlePath))
            {
                var content = await File.ReadAllTextAsync(buildGradlePath, cancellationToken);
                if (content.Contains("spring-boot"))
                {
                    analysis.ProjectType = "Spring Boot Application";
                    analysis.Dependencies.Add("Spring Boot");
                }
            }
        }

        private async Task<List<string>> FindMainMethodsAsync(string projectDirectory, List<string> javaFiles, CancellationToken cancellationToken)
        {
            var mainMethods = new List<string>();

            foreach (var javaFile in javaFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(javaFile, cancellationToken);
                    if (content.Contains("public static void main(String"))
                    {
                        mainMethods.Add(Path.GetRelativePath(projectDirectory, javaFile));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to analyze Java file {JavaFile}", javaFile);
                }
            }

            return mainMethods;
        }

        private string FindMainClass(string projectDirectory)
        {
            var binDir = Path.Combine(projectDirectory, "bin");
            if (!Directory.Exists(binDir))
                return string.Empty;

            var classFiles = Directory.GetFiles(binDir, "*.class", SearchOption.AllDirectories);

            // Look for Main.class or classes with main method (simplified approach)
            var mainClass = classFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals("Main", StringComparison.OrdinalIgnoreCase));
            if (mainClass != null)
            {
                var relativePath = Path.GetRelativePath(binDir, mainClass);
                return Path.ChangeExtension(relativePath, null).Replace(Path.DirectorySeparatorChar, '.');
            }

            return string.Empty;
        }

        private bool IsWindows()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        private long EstimateMemoryUsage(string? output)
        {
            var outputSize = output?.Length ?? 0;
            return Math.Max(outputSize * 12, 100 * 1024 * 1024); // Minimum 100MB for JVM
        }
    }
}