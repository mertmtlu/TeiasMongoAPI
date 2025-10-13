using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;
using TeiasMongoAPI.Services.Services.Implementations;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.Helpers;
using TeiasMongoAPI.Services.Interfaces;
using MongoDB.Bson;

namespace TeiasMongoAPI.Services.Services.Implementations.Execution
{
    public class CSharpProjectRunner : BaseProjectLanguageRunner
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ProjectExecutionSettings _settings;

        public override string Language => "C#";
        public override int Priority => 10;

        public CSharpProjectRunner(
            ILogger<CSharpProjectRunner> logger,
            IUnitOfWork unitOfWork,
            IBsonToDtoMappingService bsonMapper,
            IOptions<ProjectExecutionSettings> settings,
            IExecutionOutputStreamingService? streamingService = null)
            : base(logger, bsonMapper, streamingService)
        {
            _unitOfWork = unitOfWork;
            _settings = settings.Value;
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
                // Convert to absolute path
                var absoluteProjectDirectory = Path.GetFullPath(projectDirectory);

                _logger.LogInformation("Building C# project in {ProjectDirectory}", absoluteProjectDirectory);

                // Generate UIComponent.cs BEFORE attempting to build (to avoid compilation errors)
                var programId = ExtractProgramIdFromDirectory(projectDirectory);
                if (programId != ObjectId.Empty)
                {
                    await GenerateUIComponentFileAsync(projectDirectory, programId, cancellationToken);
                }

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

                var result = new ProjectBuildResult { Success = true };
                var relativeBuildTarget = Path.GetRelativePath(projectDirectory, buildTarget);

                // Get Docker image name if Docker is enabled
                string? dockerImage = null;
                if (_settings.EnableDocker)
                {
                    _settings.DockerImages.TryGetValue("CSharp", out dockerImage);
                }

                // Restore dependencies first if requested
                if (buildArgs.RestoreDependencies)
                {
                    ProcessResult restoreResult;

                    if (_settings.EnableDocker && !string.IsNullOrEmpty(dockerImage))
                    {
                        // Prepare volumes for package persistence
                        Dictionary<string, string>? volumes = null;
                        if (!string.IsNullOrEmpty(buildArgs.PackageVolumeName))
                        {
                            // Step 1: Fix permissions on the volume before restoring (run as root)
                            _logger.LogInformation("Setting ownership of package volume {VolumeName}", buildArgs.PackageVolumeName);
                            var chownResult = await RunDockerProcessAsync(
                                dockerImage,
                                "chown",
                                "-R 1000:1000 /packages",
                                absoluteProjectDirectory,
                                null,
                                new Dictionary<string, string>(),
                                1, // Short timeout
                                cancellationToken,
                                null,
                                false, // No network needed
                                128, // Minimal memory
                                0.5, // Minimal CPU
                                64, // Minimal process limit
                                64, // Minimal temp storage
                                new Dictionary<string, string> { { buildArgs.PackageVolumeName, "/packages" } },
                                "root", // Run as root to change ownership
                                true); // Allow chown capability

                            if (!chownResult.Success)
                            {
                                result.Success = false;
                                result.ErrorMessage = "Failed to set permissions on package cache volume.";
                                _logger.LogError("Failed to chown package volume: {ErrorOutput}", chownResult.Error);
                                return result;
                            }

                            // Step 2: Prepare volumes for restore (mount at .nuget location)
                            volumes = new Dictionary<string, string>
                            {
                                { buildArgs.PackageVolumeName, "/home/executor/.nuget" }
                            };
                        }

                        // Use Docker for dependency restore (needs network for downloading packages)
                        restoreResult = await RunDockerProcessAsync(
                            dockerImage,
                            "dotnet",
                            $"restore /app/{relativeBuildTarget}",
                            absoluteProjectDirectory,
                            null,
                            buildArgs.BuildEnvironment,
                            buildArgs.BuildTimeoutMinutes,
                            cancellationToken,
                            null,
                            true, // Enable network for package downloads
                            _settings.ResourceLimits.MemoryMB,
                            _settings.ResourceLimits.CPUs,
                            _settings.ResourceLimits.ProcessLimit,
                            _settings.ResourceLimits.TempStorageMB,
                            volumes);
                    }
                    else
                    {
                        // Fallback to direct execution (less secure)
                        _logger.LogWarning("Docker is disabled. Using direct execution (not recommended for production).");
                        restoreResult = await RunProcessAsync(
                            "dotnet",
                            $"restore \"{relativeBuildTarget}\"",
                            projectDirectory,
                            buildArgs.BuildEnvironment,
                            buildArgs.BuildTimeoutMinutes,
                            cancellationToken);
                    }

                    result.Output += restoreResult.Output;
                    result.ErrorOutput += restoreResult.Error;
                    result.Duration = restoreResult.Duration;

                    if (!restoreResult.Success)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Failed to restore NuGet packages";
                        _logger.LogError("Docker restore process failed. Exit Code: {ExitCode}. Error Output: {ErrorOutput}",
                            restoreResult.ExitCode, restoreResult.Error);
                        return result;
                    }
                }

                // Build the project
                var buildArguments = $"build /app/{relativeBuildTarget} --configuration {buildArgs.Configuration} --no-restore";
                if (buildArgs.AdditionalArgs.Any())
                {
                    buildArguments += " " + string.Join(" ", buildArgs.AdditionalArgs);
                }

                ProcessResult buildResult;

                if (_settings.EnableDocker && !string.IsNullOrEmpty(dockerImage))
                {
                    // Prepare volumes for package access
                    Dictionary<string, string>? volumes = null;
                    if (!string.IsNullOrEmpty(buildArgs.PackageVolumeName))
                    {
                        volumes = new Dictionary<string, string>
                        {
                            { buildArgs.PackageVolumeName, "/home/executor/.nuget" }
                        };
                    }

                    // Use Docker for build (no network needed, packages already restored)
                    buildResult = await RunDockerProcessAsync(
                        dockerImage,
                        "dotnet",
                        buildArguments,
                        absoluteProjectDirectory,
                        null,
                        buildArgs.BuildEnvironment,
                        buildArgs.BuildTimeoutMinutes,
                        cancellationToken,
                        null,
                        false, // No network needed for build
                        _settings.ResourceLimits.MemoryMB,
                        _settings.ResourceLimits.CPUs,
                        _settings.ResourceLimits.ProcessLimit,
                        _settings.ResourceLimits.TempStorageMB,
                        volumes);
                }
                else
                {
                    // Fallback to direct execution
                    buildResult = await RunProcessAsync(
                        "dotnet",
                        $"build \"{relativeBuildTarget}\" --configuration {buildArgs.Configuration} --no-restore",
                        projectDirectory,
                        buildArgs.BuildEnvironment,
                        buildArgs.BuildTimeoutMinutes,
                        cancellationToken);
                }

                result.Output += buildResult.Output;
                result.ErrorOutput += buildResult.Error;
                result.Duration = buildResult.Duration;

                if (!buildResult.Success)
                {
                    result.Success = false;
                    result.ErrorMessage = "Build failed";
                    result.Warnings = ParseBuildWarnings(buildResult.Output + buildResult.Error);
                    _logger.LogError("Docker build process failed. Exit Code: {ExitCode}. Error Output: {ErrorOutput}",
                        buildResult.ExitCode, buildResult.Error);
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
                // Convert to absolute path
                var absoluteProjectDirectory = Path.GetFullPath(context.ProjectDirectory);

                _logger.LogInformation("Executing C# project in {ProjectDirectory}", absoluteProjectDirectory);

                // Process input files from parameters (using base class method)
                await ProcessInputFilesFromParametersAsync(context, cancellationToken);

                // Note: UIComponent.cs is generated during build phase

                // Determine execution command and arguments
                string command;
                string arguments;

                var runnableProject = FindRunnableProject(context.ProjectDirectory);
                if (!string.IsNullOrEmpty(runnableProject))
                {
                    var relativeProjectPath = Path.GetRelativePath(context.ProjectDirectory, runnableProject);
                    command = "dotnet";
                    arguments = $"run --project /app/{relativeProjectPath} --no-build --no-restore";
                }
                else
                {
                    command = "dotnet";
                    arguments = "run --no-build --no-restore";
                    _logger.LogWarning("No .csproj file found to specify for 'dotnet run'. Using default project.");
                }

                // Add parameters if any
                if (context.Parameters != null && context.Parameters.ToString() != "{}")
                {
                    var processedParams = ProcessParametersForExecution(context.Parameters, context.ProjectDirectory);
                    var paramJson = JsonSerializer.Serialize(processedParams);
                    arguments += $" -- '{paramJson}'";
                }

                ProcessResult processResult;

                if (_settings.EnableDocker && _settings.DockerImages.TryGetValue("CSharp", out var dockerImage))
                {
                    // ============================================================================
                    // DOCKER-BASED EXECUTION WITH TIER-AWARE DISPATCHING
                    // ============================================================================
                    var outputDir = Path.Combine(Path.GetDirectoryName(absoluteProjectDirectory) ?? "", "outputs");
                    Directory.CreateDirectory(outputDir);

                    // Prepare volumes for package access
                    Dictionary<string, string>? volumes = null;
                    if (!string.IsNullOrEmpty(context.PackageVolumeName))
                    {
                        volumes = new Dictionary<string, string>
                        {
                            { context.PackageVolumeName, "/home/executor/.nuget" }
                        };
                    }

                    // ============================================================================
                    // TIERED EXECUTION: Worker-Level Tier Dispatching
                    // ============================================================================
                    if (_settings.TieredExecution.EnableTieredExecution && !string.IsNullOrEmpty(context.ExecutionTier))
                    {
                        _logger.LogInformation(
                            "Execution {ExecutionId}: Tiered execution enabled. Dispatching to tier: '{Tier}' (Job profile: '{Profile}')",
                            context.ExecutionId, context.ExecutionTier, context.JobProfile ?? "Not specified");

                        // RAM Tier: tmpfs-based execution with iterative OOM recovery
                        if (context.ExecutionTier.Equals("RAM", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation(
                                "Execution {ExecutionId}: Dispatching to RAM tier execution (tmpfs-based with OOM recovery)",
                                context.ExecutionId);

                            processResult = await ExecuteWithRamTierAsync(
                                dockerImage,
                                command,
                                arguments,
                                absoluteProjectDirectory,
                                outputDir,
                                context.Environment,
                                2880,
                                context.ExecutionId,
                                cancellationToken,
                                volumes);

                            _logger.LogInformation(
                                "Execution {ExecutionId}: RAM tier execution completed. Success: {Success}, Exit Code: {ExitCode}",
                                context.ExecutionId, processResult.Success, processResult.ExitCode);
                        }
                        // Disk Tier: Persistent volume-based execution
                        else if (context.ExecutionTier.Equals("Disk", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation(
                                "Execution {ExecutionId}: Dispatching to Disk tier execution (persistent volume-based)",
                                context.ExecutionId);

                            processResult = await ExecuteWithDiskTierAsync(
                                dockerImage,
                                command,
                                arguments,
                                absoluteProjectDirectory,
                                outputDir,
                                context.Environment,
                                2880,
                                context.ExecutionId,
                                cancellationToken,
                                volumes);

                            _logger.LogInformation(
                                "Execution {ExecutionId}: Disk tier execution completed. Success: {Success}, Exit Code: {ExitCode}",
                                context.ExecutionId, processResult.Success, processResult.ExitCode);
                        }
                        // Unknown Tier: Fall back to standard execution
                        else
                        {
                            _logger.LogWarning(
                                "Execution {ExecutionId}: Unknown execution tier '{Tier}' specified. " +
                                "Valid tiers are 'RAM' or 'Disk'. Falling back to standard execution for safety.",
                                context.ExecutionId, context.ExecutionTier);

                            processResult = await RunDockerProcessAsync(
                                dockerImage,
                                command,
                                arguments,
                                absoluteProjectDirectory,
                                outputDir,
                                context.Environment,
                                2880,
                                cancellationToken,
                                context.ExecutionId,
                                _settings.EnableNetworkAccess,
                                _settings.ResourceLimits.MemoryMB,
                                _settings.ResourceLimits.CPUs,
                                _settings.ResourceLimits.ProcessLimit,
                                _settings.ResourceLimits.TempStorageMB,
                                volumes);
                        }
                    }
                    else
                    {
                        // ========================================================================
                        // STANDARD EXECUTION (Backward Compatible)
                        // ========================================================================
                        if (_settings.TieredExecution.EnableTieredExecution)
                        {
                            _logger.LogDebug(
                                "Execution {ExecutionId}: Tiered execution is enabled but no tier specified. Using standard execution.",
                                context.ExecutionId);
                        }

                        processResult = await RunDockerProcessAsync(
                            dockerImage,
                            command,
                            arguments,
                            absoluteProjectDirectory,
                            outputDir,
                            context.Environment,
                            2880,
                            cancellationToken,
                            context.ExecutionId,
                            _settings.EnableNetworkAccess,
                            _settings.ResourceLimits.MemoryMB,
                            _settings.ResourceLimits.CPUs,
                            _settings.ResourceLimits.ProcessLimit,
                            _settings.ResourceLimits.TempStorageMB,
                            volumes);
                    }
                }
                else
                {
                    // Fallback to direct execution (less secure)
                    _logger.LogWarning("Docker is disabled. Using direct execution (not recommended for production).");

                    // Adjust arguments for direct execution
                    var directArgs = arguments.Replace("/app/", "");
                    processResult = await RunProcessAsync(
                        command,
                        directArgs,
                        context.ProjectDirectory,
                        context.Environment,
                        2880,
                        cancellationToken,
                        context.ExecutionId);
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

        #region Tiered Execution Methods

        /// <summary>
        /// Execute using RAM tier with tmpfs and iterative relaunch on OOM
        /// </summary>
        private async Task<ProcessResult> ExecuteWithRamTierAsync(
            string dockerImage,
            string command,
            string arguments,
            string projectDirectory,
            string outputDir,
            Dictionary<string, string> environment,
            int timeoutMinutes,
            string executionId,
            CancellationToken cancellationToken,
            Dictionary<string, string>? volumes = null)
        {
            if (!_settings.TieredExecution.EnableTieredExecution)
            {
                throw new InvalidOperationException("Tiered execution is not enabled");
            }

            var ramSettings = _settings.TieredExecution.RamPool;
            int currentTmpfsSizeMB = ramSettings.TmpfsBaseSizeMB;
            int attempt = 0;

            while (attempt < ramSettings.IterativeRelaunch.MaxRetries)
            {
                attempt++;
                _logger.LogInformation(
                    "Execution {ExecutionId}: RAM tier attempt {Attempt}/{MaxRetries} with tmpfs size {TmpfsSizeMB}MB",
                    executionId, attempt, ramSettings.IterativeRelaunch.MaxRetries, currentTmpfsSizeMB);

                // Run with tmpfs for RAM tier
                var processResult = await RunDockerProcessAsync(
                    dockerImage,
                    command,
                    arguments,
                    projectDirectory,
                    outputDir,
                    environment,
                    timeoutMinutes,
                    cancellationToken,
                    executionId,
                    _settings.EnableNetworkAccess,
                    _settings.ResourceLimits.MemoryMB,
                    _settings.ResourceLimits.CPUs,
                    _settings.ResourceLimits.ProcessLimit,
                    currentTmpfsSizeMB, // Use currentTmpfsSizeMB for tmpfs
                    volumes);

                // Check if OOM occurred
                if (ramSettings.EnableIterativeRelaunch && !processResult.Success)
                {
                    bool isOOM = false;
                    foreach (var pattern in ramSettings.IterativeRelaunch.TriggerPatterns)
                    {
                        if (processResult.Error.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                            processResult.Output.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            isOOM = true;
                            break;
                        }
                    }

                    if (isOOM && attempt < ramSettings.IterativeRelaunch.MaxRetries)
                    {
                        // Calculate new tmpfs size
                        int newTmpfsSizeMB = (int)(currentTmpfsSizeMB * ramSettings.IterativeRelaunch.MultiplierFactor);
                        newTmpfsSizeMB = Math.Min(newTmpfsSizeMB, ramSettings.IterativeRelaunch.MaxSizeMB);

                        if (newTmpfsSizeMB > currentTmpfsSizeMB)
                        {
                            _logger.LogWarning(
                                "Execution {ExecutionId}: OOM detected, retrying with increased tmpfs size: {OldSize}MB -> {NewSize}MB",
                                executionId, currentTmpfsSizeMB, newTmpfsSizeMB);

                            currentTmpfsSizeMB = newTmpfsSizeMB;
                            continue; // Retry with larger tmpfs
                        }
                        else
                        {
                            _logger.LogError(
                                "Execution {ExecutionId}: OOM detected but already at max tmpfs size ({MaxSize}MB)",
                                executionId, ramSettings.IterativeRelaunch.MaxSizeMB);
                            return processResult; // Give up
                        }
                    }
                }

                // Success or non-OOM failure
                return processResult;
            }

            // Max retries reached
            _logger.LogError(
                "Execution {ExecutionId}: Max retries ({MaxRetries}) reached for RAM tier execution",
                executionId, ramSettings.IterativeRelaunch.MaxRetries);

            return new ProcessResult
            {
                Success = false,
                ExitCode = -1,
                Error = $"Execution failed after {ramSettings.IterativeRelaunch.MaxRetries} OOM retries",
                Duration = TimeSpan.Zero
            };
        }

        /// <summary>
        /// Execute using Disk tier with persistent volume
        /// </summary>
        private async Task<ProcessResult> ExecuteWithDiskTierAsync(
            string dockerImage,
            string command,
            string arguments,
            string projectDirectory,
            string outputDir,
            Dictionary<string, string> environment,
            int timeoutMinutes,
            string executionId,
            CancellationToken cancellationToken,
            Dictionary<string, string>? volumes = null)
        {
            if (!_settings.TieredExecution.EnableTieredExecution)
            {
                throw new InvalidOperationException("Tiered execution is not enabled");
            }

            var diskSettings = _settings.TieredExecution.DiskPool;
            string volumePath = diskSettings.DiskVolumePath;

            // Create execution-specific volume directory
            string executionVolumePath = Path.Combine(volumePath, executionId);
            Directory.CreateDirectory(executionVolumePath);

            _logger.LogInformation(
                "Execution {ExecutionId}: Using Disk tier with volume at {VolumePath}",
                executionId, executionVolumePath);

            try
            {
                // Merge execution volume with existing volumes
                var mergedVolumes = volumes != null
                    ? new Dictionary<string, string>(volumes)
                    : new Dictionary<string, string>();

                mergedVolumes[executionVolumePath] = "/execution_volume";

                // Run with persistent disk volume
                var processResult = await RunDockerProcessAsync(
                    dockerImage,
                    command,
                    arguments,
                    projectDirectory,
                    outputDir,
                    environment,
                    timeoutMinutes,
                    cancellationToken,
                    executionId,
                    _settings.EnableNetworkAccess,
                    _settings.ResourceLimits.MemoryMB,
                    _settings.ResourceLimits.CPUs,
                    _settings.ResourceLimits.ProcessLimit,
                    _settings.ResourceLimits.TempStorageMB,
                    mergedVolumes);

                // Cleanup volume if configured
                if (!diskSettings.EnableVolumeReuse)
                {
                    try
                    {
                        Directory.Delete(executionVolumePath, recursive: true);
                        _logger.LogInformation(
                            "Execution {ExecutionId}: Deleted volume directory {VolumePath}",
                            executionId, executionVolumePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to delete volume directory {VolumePath} for execution {ExecutionId}",
                            executionVolumePath, executionId);
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "Execution {ExecutionId}: Volume reuse enabled, keeping volume at {VolumePath}",
                        executionId, executionVolumePath);
                }

                return processResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to execute with Disk tier for execution {ExecutionId}",
                    executionId);

                return new ProcessResult
                {
                    Success = false,
                    ExitCode = -1,
                    Error = $"Disk tier execution failed: {ex.Message}",
                    Duration = TimeSpan.Zero
                };
            }
        }

        #endregion

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
            return ExtractProgramIdFromDirectory(context.ProjectDirectory);
        }

        private ObjectId ExtractProgramIdFromDirectory(string projectDirectory)
        {
            try
            {
                // Extract program ID from the project directory path
                // Path format: ./storage/{programId}/{versionId}/execution/{executionId}/project
                var projectPath = projectDirectory.Replace("\\", "/");
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

                _logger.LogWarning("Could not extract program ID from project directory path: {ProjectDirectory}", projectDirectory);
                return ObjectId.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract program ID from directory path");
                return ObjectId.Empty;
            }
        }
    }
}