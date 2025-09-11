using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;
using TeiasMongoAPI.Services.Services.Implementations;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.Helpers;
using MongoDB.Bson;

namespace TeiasMongoAPI.Services.Services.Implementations.Execution
{
    public class CSharpProjectRunner : BaseProjectLanguageRunner
    {
        private readonly IUnitOfWork _unitOfWork;

        public override string Language => "C#";
        public override int Priority => 10;

        public CSharpProjectRunner(ILogger<CSharpProjectRunner> logger, IUnitOfWork unitOfWork, IBsonToDtoMappingService bsonMapper) : base(logger, bsonMapper) 
        {
            _unitOfWork = unitOfWork;
        }

        public override async Task<bool> CanHandleProjectAsync(string projectDirectory, CancellationToken cancellationToken = default)
        {
            // Look for .csproj, .sln files or .cs files
            return FileExists(projectDirectory, "*.csproj") ||
                   FileExists(projectDirectory, "*.sln") ||
                   FindFiles(projectDirectory, "*.csproj").Any() ||
                   FindFiles(projectDirectory, "*.sln").Any() ||
                   FindFiles(projectDirectory, "*.cs").Any();
        }

        public override async Task<ProjectStructureAnalysis> AnalyzeProjectAsync(string projectDirectory, CancellationToken cancellationToken = default)
        {
            var analysis = new ProjectStructureAnalysis
            {
                Language = Language,
                ProjectType = "C# Application"
            };

            try
            {
                // Find project files
                var csprojFiles = FindFiles(projectDirectory, "*.csproj");
                var slnFiles = FindFiles(projectDirectory, "*.sln");
                var csFiles = FindFiles(projectDirectory, "*.cs");

                analysis.ConfigFiles.AddRange(csprojFiles.Select(f => Path.GetRelativePath(projectDirectory, f)));
                analysis.ConfigFiles.AddRange(slnFiles.Select(f => Path.GetRelativePath(projectDirectory, f)));
                analysis.SourceFiles.AddRange(csFiles.Select(f => Path.GetRelativePath(projectDirectory, f)));

                analysis.HasBuildFile = csprojFiles.Any() || slnFiles.Any();

                // Analyze main project file
                var mainProjectFile = csprojFiles.FirstOrDefault() ?? slnFiles.FirstOrDefault();
                if (mainProjectFile != null)
                {
                    await AnalyzeProjectFileAsync(mainProjectFile, analysis, cancellationToken);
                }

                // Find entry points
                analysis.EntryPoints = await FindEntryPointsAsync(projectDirectory, csFiles, cancellationToken);
                analysis.MainEntryPoint = analysis.EntryPoints.FirstOrDefault();

                // Additional metadata
                analysis.Metadata["framework"] = GetTargetFramework(mainProjectFile);
                analysis.Metadata["projectFiles"] = csprojFiles.Count;
                analysis.Metadata["sourceFiles"] = csFiles.Count;

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze C# project in {ProjectDirectory}", projectDirectory);
                return analysis;
            }
        }

        public override async Task<ProjectBuildResult> BuildProjectAsync(string projectDirectory, ProjectBuildArgs buildArgs, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Building C# project in {ProjectDirectory}", projectDirectory);

                // Find the main project file to build
                var buildTarget = FindBuildTarget(projectDirectory);
                if (string.IsNullOrEmpty(buildTarget))
                {
                    return new ProjectBuildResult
                    {
                        Success = false,
                        ErrorMessage = "No .csproj or .sln file found to build"
                    };
                }

                // Restore dependencies first if requested
                if (buildArgs.RestoreDependencies)
                {
                    var restoreResult = await RunProcessAsync(
                        "dotnet",
                        "restore",
                        projectDirectory,
                        buildArgs.BuildEnvironment,
                        buildArgs.BuildTimeoutMinutes,
                        cancellationToken);

                    if (!restoreResult.Success)
                    {
                        return new ProjectBuildResult
                        {
                            Success = false,
                            Output = restoreResult.Output,
                            ErrorOutput = restoreResult.Error,
                            ErrorMessage = "Package restore failed",
                            Duration = restoreResult.Duration
                        };
                    }
                }

                // Build the project
                var relativeBuildTarget = Path.GetRelativePath(projectDirectory, buildTarget);

                // Build the project using the relative path
                var buildArguments = $"build \"{relativeBuildTarget}\" --configuration {buildArgs.Configuration}";
                if (buildArgs.AdditionalArgs.Any())
                {
                    buildArguments += " " + string.Join(" ", buildArgs.AdditionalArgs);
                }

                var buildResult = await RunProcessAsync(
                    "dotnet",
                    buildArguments,
                    projectDirectory,
                    buildArgs.BuildEnvironment,
                    buildArgs.BuildTimeoutMinutes,
                    cancellationToken);

                var result = new ProjectBuildResult
                {
                    Success = buildResult.Success,
                    Output = buildResult.Output,
                    ErrorOutput = buildResult.Error,
                    Duration = buildResult.Duration
                };

                if (!buildResult.Success)
                {
                    result.ErrorMessage = "Build failed";
                    result.Warnings = ParseBuildWarnings(buildResult.Output + buildResult.Error);
                }
                else
                {
                    result.GeneratedFiles = FindGeneratedFiles(projectDirectory, buildArgs.Configuration);
                    _logger.LogInformation("C# project built successfully with {FileCount} generated files", result.GeneratedFiles.Count);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Build failed for C# project in {ProjectDirectory}", projectDirectory);
                return new ProjectBuildResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task GenerateUIComponentFileAsync(string projectDirectory, ObjectId programId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Generating UIComponent.cs for program {ProgramId} in {ProjectDirectory}", programId, projectDirectory);

                var latestComponent = await _unitOfWork.UiComponents.GetLatestActiveByProgramAsync(programId, cancellationToken);

                if (latestComponent == null)
                {
                    _logger.LogInformation("No active UI component found for program {ProgramId}, skipping UIComponent.cs generation", programId);
                    return;
                }

                // Find the runnable project to determine where to place UIComponent.cs
                var runnableProject = FindRunnableProject(projectDirectory);
                string targetDirectory;

                if (!string.IsNullOrEmpty(runnableProject))
                {
                    // Place UIComponent.cs in the same directory as the runnable .csproj
                    targetDirectory = Path.GetDirectoryName(runnableProject) ?? projectDirectory;
                    _logger.LogInformation("Generating UIComponent.cs in runnable project directory: {TargetDirectory}", targetDirectory);
                }
                else
                {
                    // Fallback: place in the main project directory
                    targetDirectory = projectDirectory;
                    _logger.LogWarning("No runnable project found, generating UIComponent.cs in project root: {TargetDirectory}", targetDirectory);
                }

                var csharpCode = UIComponentCSharpGenerator.GenerateUIComponentClass(latestComponent);
                var uiComponentPath = Path.Combine(targetDirectory, "UIComponent.cs");

                await File.WriteAllTextAsync(uiComponentPath, csharpCode, cancellationToken);

                _logger.LogInformation("Successfully generated UIComponent.cs for component '{ComponentName}' ({ComponentId}) at {FilePath}",
                    latestComponent.Name, latestComponent._ID, uiComponentPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate UIComponent.cs for program {ProgramId} in {ProjectDirectory}", programId, projectDirectory);
                // Don't fail the execution, just log the error
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
                _logger.LogInformation("Executing C# project in {ProjectDirectory}", context.ProjectDirectory);

                // Generate UIComponent.cs file based on the latest active UI component for the program
                var programId = ExtractProgramIdFromContext(context);
                if (programId != ObjectId.Empty)
                {
                    await GenerateUIComponentFileAsync(context.ProjectDirectory, programId, cancellationToken);
                }

                // This part is fine, it looks for an already-compiled executable
                var executable = FindExecutable(context.ProjectDirectory);
                if (string.IsNullOrEmpty(executable))
                {
                    // Try to run directly with dotnet run
                    executable = "dotnet";
                    var runArgs = "run";

                    // DO NOT use FindBuildTarget here. Use our new helper to find the .csproj to RUN.
                    var runnableProject = FindRunnableProject(context.ProjectDirectory);
                    if (!string.IsNullOrEmpty(runnableProject))
                    {
                        var relativeProjectPath = Path.GetRelativePath(context.ProjectDirectory, runnableProject);
                        runArgs += $" --project \"{relativeProjectPath}\"";
                    }
                    else
                    {
                        // If we can't find a project, dotnet run might still work if the context is unambiguous,
                        // but we should log a warning.
                        _logger.LogWarning("No .csproj file found to specify for 'dotnet run'. The command may fail if the project to run is ambiguous.");
                    }

                    // Add parameters if any
                    if (context.Parameters != null && context.Parameters.ToString() != "{}")
                    {
                        var paramJson = JsonSerializer.Serialize(context.Parameters);
                        runArgs += $" -- {paramJson}";
                    }

                    var processResult = await RunProcessAsync(
                        executable,
                        runArgs,
                        context.ProjectDirectory,
                        context.Environment,
                        context.ResourceLimits.MaxExecutionTimeMinutes,
                        cancellationToken);

                    result.Success = processResult.Success;
                    result.ExitCode = processResult.ExitCode;
                    result.Output = processResult.Output;
                    result.ErrorOutput = processResult.Error;
                }
                else
                {
                    // Run the compiled executable directly (this logic is unchanged and correct)
                    var processResult = await RunProcessAsync(
                        executable,
                        "", // Arguments would be passed here
                        context.ProjectDirectory,
                        context.Environment,
                        context.ResourceLimits.MaxExecutionTimeMinutes,
                        cancellationToken);

                    result.Success = processResult.Success;
                    result.ExitCode = processResult.ExitCode;
                    result.Output = processResult.Output;
                    result.ErrorOutput = processResult.Error;
                }

                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;

                // Resource usage (simulated for now)
                result.ResourceUsage = new ProjectResourceUsage
                {
                    CpuTimeSeconds = result.Duration?.TotalSeconds ?? 0,
                    PeakMemoryBytes = EstimateMemoryUsage(result.Output),
                    OutputSizeBytes = (result.Output?.Length ?? 0) + (result.ErrorOutput?.Length ?? 0)
                };

                _logger.LogInformation("C# project execution completed with exit code {ExitCode}", result.ExitCode);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Execution failed for C# project in {ProjectDirectory}", context.ProjectDirectory);
                result.Success = false;
                result.ExitCode = -1;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;
                return result;
            }
        }

        private string FindRunnableProject(string projectDirectory)
        {
            var csprojFiles = FindFiles(projectDirectory, "*.csproj");

            if (!csprojFiles.Any())
            {
                return string.Empty;
            }

            if (csprojFiles.Count == 1)
            {
                return csprojFiles.First();
            }

            // Multiple projects found, try to find the executable one
            foreach (var projectFile in csprojFiles)
            {
                try
                {
                    var content = File.ReadAllText(projectFile);
                    // Look for <OutputType>Exe</OutputType> or <OutputType>WinExe</OutputType>
                    if (Regex.IsMatch(content, @"<OutputType>\s*(Win)?Exe\s*</OutputType>", RegexOptions.IgnoreCase))
                    {
                        _logger.LogInformation("Found runnable project '{ProjectFile}' in multi-project solution.", Path.GetFileName(projectFile));
                        return projectFile;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read or parse project file {ProjectFile} to determine output type.", projectFile);
                }
            }

            // Fallback: return the first project. dotnet run will fail if it's a library, which is correct behavior.
            _logger.LogWarning("Could not determine a single runnable project. Falling back to the first project found: {ProjectFile}", Path.GetFileName(csprojFiles.First()));
            return csprojFiles.First();
        }

        protected override async Task ValidateLanguageSpecificAsync(string projectDirectory, ProjectValidationResult result, CancellationToken cancellationToken)
        {
            // Check for .NET SDK
            var dotnetCheck = await RunProcessAsync("dotnet", "--version", projectDirectory, cancellationToken: cancellationToken);
            if (!dotnetCheck.Success)
            {
                result.Errors.Add(".NET SDK is not installed or not accessible");
                result.IsValid = false;
                return;
            }

            // Validate project files
            var csprojFiles = FindFiles(projectDirectory, "*.csproj");
            if (csprojFiles.Any())
            {
                foreach (var csprojFile in csprojFiles)
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(csprojFile, cancellationToken);
                        // Basic XML validation
                        if (!content.Contains("<Project") || !content.Contains("</Project>"))
                        {
                            result.Warnings.Add($"Invalid project file format: {Path.GetFileName(csprojFile)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Cannot read project file {Path.GetFileName(csprojFile)}: {ex.Message}");
                    }
                }
            }

            // Check for source files
            var csFiles = FindFiles(projectDirectory, "*.cs");
            if (!csFiles.Any())
            {
                result.Warnings.Add("No C# source files found");
            }
        }

        private async Task AnalyzeProjectFileAsync(string projectFile, ProjectStructureAnalysis analysis, CancellationToken cancellationToken)
        {
            try
            {
                var content = await File.ReadAllTextAsync(projectFile, cancellationToken);

                // Extract target framework
                var frameworkMatch = Regex.Match(content, @"<TargetFramework.*?>(.*?)</TargetFramework.*?>", RegexOptions.IgnoreCase);
                if (frameworkMatch.Success)
                {
                    analysis.Metadata["targetFramework"] = frameworkMatch.Groups[1].Value;
                }

                // Extract package references
                var packageMatches = Regex.Matches(content, @"<PackageReference.*?Include=""(.*?)"".*?>", RegexOptions.IgnoreCase);
                foreach (Match match in packageMatches)
                {
                    analysis.Dependencies.Add(match.Groups[1].Value);
                }

                // Determine project type
                if (content.Contains("Microsoft.AspNetCore"))
                {
                    analysis.ProjectType = "ASP.NET Core Web Application";
                }
                else if (content.Contains("Microsoft.NET.Sdk.Web"))
                {
                    analysis.ProjectType = "Web Application";
                }
                else if (content.Contains("WinExe"))
                {
                    analysis.ProjectType = "Windows Application";
                }
                else if (content.Contains("Exe"))
                {
                    analysis.ProjectType = "Console Application";
                }
                else if (content.Contains("Library"))
                {
                    analysis.ProjectType = "Class Library";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze project file {ProjectFile}", projectFile);
            }
        }

        private async Task<List<string>> FindEntryPointsAsync(string projectDirectory, List<string> csFiles, CancellationToken cancellationToken)
        {
            var entryPoints = new List<string>();

            foreach (var csFile in csFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(csFile, cancellationToken);

                    // Look for Main method
                    if (Regex.IsMatch(content, @"static\s+(?:async\s+)?(?:void|int|Task(?:<int>)?)\s+Main\s*\(", RegexOptions.IgnoreCase))
                    {
                        entryPoints.Add(Path.GetRelativePath(projectDirectory, csFile));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to analyze source file {CsFile}", csFile);
                }
            }

            return entryPoints;
        }

        private string FindBuildTarget(string projectDirectory)
        {
            // Prefer .sln files over .csproj files
            var slnFiles = FindFiles(projectDirectory, "*.sln");
            if (slnFiles.Any())
            {
                return slnFiles.First();
            }

            var csprojFiles = FindFiles(projectDirectory, "*.csproj");
            return csprojFiles.FirstOrDefault() ?? string.Empty;
        }

        private string? FindExecutable(string projectDirectory)
        {
            // Look in bin directories for compiled executables
            var binDirs = new[] { "bin/Debug", "bin/Release", "bin" };

            foreach (var binDir in binDirs)
            {
                var fullBinPath = Path.Combine(projectDirectory, binDir);
                if (Directory.Exists(fullBinPath))
                {
                    var exeFiles = Directory.GetFiles(fullBinPath, "*.exe", SearchOption.AllDirectories);
                    if (exeFiles.Any())
                    {
                        return exeFiles.First();
                    }

                    var dllFiles = Directory.GetFiles(fullBinPath, "*.dll", SearchOption.AllDirectories);
                    if (dllFiles.Any())
                    {
                        // For .NET Core apps, we might have a .dll as the main executable
                        return dllFiles.First();
                    }
                }
            }

            return null;
        }

        private string GetTargetFramework(string? projectFile)
        {
            if (string.IsNullOrEmpty(projectFile) || !File.Exists(projectFile))
                return "Unknown";

            try
            {
                var content = File.ReadAllText(projectFile);
                var match = Regex.Match(content, @"<TargetFramework.*?>(.*?)</TargetFramework.*?>", RegexOptions.IgnoreCase);
                return match.Success ? match.Groups[1].Value : "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private List<string> FindGeneratedFiles(string projectDirectory, string configuration)
        {
            var generatedFiles = new List<string>();
            var outputDir = Path.Combine(projectDirectory, "bin", configuration);

            if (Directory.Exists(outputDir))
            {
                var files = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
                generatedFiles.AddRange(files.Select(f => Path.GetRelativePath(projectDirectory, f)));
            }

            return generatedFiles;
        }

        private List<ProjectBuildWarning> ParseBuildWarnings(string buildOutput)
        {
            var warnings = new List<ProjectBuildWarning>();

            // Parse MSBuild warning format
            var warningPattern = @"(?<file>.*?)\((?<line>\d+),\d+\):\s*warning\s*(?<code>\w+):\s*(?<message>.*?)$";
            var matches = Regex.Matches(buildOutput, warningPattern, RegexOptions.Multiline);

            foreach (Match match in matches)
            {
                warnings.Add(new ProjectBuildWarning
                {
                    Message = match.Groups["message"].Value.Trim(),
                    File = match.Groups["file"].Value,
                    Line = int.TryParse(match.Groups["line"].Value, out var line) ? line : null,
                    Severity = "Warning"
                });
            }

            return warnings;
        }

        private long EstimateMemoryUsage(string? output)
        {
            // Simple estimation based on output size
            var outputSize = output?.Length ?? 0;
            return Math.Max(outputSize * 10, 50 * 1024 * 1024); // Minimum 50MB
        }

        private ObjectId ExtractProgramIdFromContext(ProjectExecutionContext context)
        {
            try
            {
                // Extract program ID from the execution directory path
                // Path format: ./storage/{programId}/{versionId}/execution/{executionId}/project
                var projectPath = context.ProjectDirectory.Replace("\\", "/");
                var pathParts = projectPath.Split('/');

                // Find the storage directory index
                var storageIndex = Array.FindIndex(pathParts, p => p.Equals("storage", StringComparison.OrdinalIgnoreCase));
                if (storageIndex >= 0 && storageIndex + 1 < pathParts.Length)
                {
                    var programIdString = pathParts[storageIndex + 1];
                    if (ObjectId.TryParse(programIdString, out var programId))
                    {
                        return programId;
                    }
                }

                _logger.LogWarning("Could not extract program ID from project directory path: {ProjectDirectory}", context.ProjectDirectory);
                return ObjectId.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract program ID from context");
                return ObjectId.Empty;
            }
        }
    }
}