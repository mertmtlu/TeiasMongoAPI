using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.Helpers;
using TeiasMongoAPI.Services.Interfaces;
using MongoDB.Bson;
using TeiasMongoAPI.Services.Services.Implementations;

namespace TeiasMongoAPI.Services.Services.Implementations.Execution
{
    public class PythonProjectRunner : BaseProjectLanguageRunner
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ProjectExecutionSettings _settings;

        public override string Language => "Python";
        public override int Priority => 20;

        public PythonProjectRunner(
            ILogger<PythonProjectRunner> logger,
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
                // FIX: Convert the relative path to an absolute path
                var absoluteProjectDirectory = Path.GetFullPath(projectDirectory);

                _logger.LogInformation("Installing Python dependencies in {ProjectDirectory}", absoluteProjectDirectory);

                var result = new ProjectBuildResult { Success = true };

                // Install dependencies if requirements.txt exists
                if (FileExists(projectDirectory, "requirements.txt"))
                {
                    ProcessResult installResult;

                    if (_settings.EnableDocker && _settings.DockerImages.TryGetValue("Python", out var dockerImage))
                    {
                        // Prepare volumes for package persistence
                        Dictionary<string, string>? volumes = null;
                        if (!string.IsNullOrEmpty(buildArgs.PackageVolumeName))
                        {
                            // Step 1: Fix permissions on the volume before installing (run as root)
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

                            // Step 2: Prepare volumes for pip install (mount at final location)
                            volumes = new Dictionary<string, string>
                            {
                                { buildArgs.PackageVolumeName, "/home/executor/.local" }
                            };
                        }

                        // Step 2: Use Docker for dependency installation (needs network for downloading packages)
                        installResult = await RunDockerProcessAsync(
                            dockerImage,
                            "pip",
                            "install --user -r /app/requirements.txt",
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
                        installResult = await RunProcessAsync(
                            "pip",
                            "install -r requirements.txt",
                            projectDirectory,
                            buildArgs.BuildEnvironment,
                            buildArgs.BuildTimeoutMinutes,
                            cancellationToken);
                    }

                    result.Output += installResult.Output;
                    result.ErrorOutput += installResult.Error;
                    result.Duration = installResult.Duration;

                    if (!installResult.Success)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Failed to install Python dependencies";
                        _logger.LogError("Docker build process failed. Exit Code: {ExitCode}. Error Output: {ErrorOutput}", installResult.ExitCode, installResult.Error);
                        return result;
                    }
                }

                // Handle setup.py installation
                if (FileExists(projectDirectory, "setup.py"))
                {
                    ProcessResult setupResult;

                    if (_settings.EnableDocker && _settings.DockerImages.TryGetValue("Python", out var dockerImage))
                    {
                        // Prepare volumes for package persistence
                        Dictionary<string, string>? volumes = null;
                        if (!string.IsNullOrEmpty(buildArgs.PackageVolumeName))
                        {
                            // Fix permissions on the volume (only if not already done above)
                            // Note: If requirements.txt was processed, permissions are already fixed
                            if (!FileExists(projectDirectory, "requirements.txt"))
                            {
                                _logger.LogInformation("Setting ownership of package volume {VolumeName}", buildArgs.PackageVolumeName);
                                var chownResult = await RunDockerProcessAsync(
                                    dockerImage,
                                    "chown",
                                    "-R 1000:1000 /packages",
                                    absoluteProjectDirectory,
                                    null,
                                    new Dictionary<string, string>(),
                                    1,
                                    cancellationToken,
                                    null,
                                    false,
                                    128,
                                    0.5,
                                    64,
                                    64,
                                    new Dictionary<string, string> { { buildArgs.PackageVolumeName, "/packages" } },
                                    "root",
                                    true); // Allow chown capability

                                if (!chownResult.Success)
                                {
                                    result.Success = false;
                                    result.ErrorMessage = "Failed to set permissions on package cache volume.";
                                    _logger.LogError("Failed to chown package volume: {ErrorOutput}", chownResult.Error);
                                    return result;
                                }
                            }

                            volumes = new Dictionary<string, string>
                            {
                                { buildArgs.PackageVolumeName, "/home/executor/.local" }
                            };
                        }

                        setupResult = await RunDockerProcessAsync(
                            dockerImage,
                            "python",
                            "/app/setup.py develop",
                            absoluteProjectDirectory,
                            null,
                            buildArgs.BuildEnvironment,
                            buildArgs.BuildTimeoutMinutes,
                            cancellationToken,
                            null,
                            true,
                            _settings.ResourceLimits.MemoryMB,
                            _settings.ResourceLimits.CPUs,
                            _settings.ResourceLimits.ProcessLimit,
                            _settings.ResourceLimits.TempStorageMB,
                            volumes);
                    }
                    else
                    {
                        setupResult = await RunProcessAsync(
                            "python",
                            "setup.py develop",
                            projectDirectory,
                            buildArgs.BuildEnvironment,
                            buildArgs.BuildTimeoutMinutes,
                            cancellationToken);
                    }

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

        public async Task GenerateUIComponentFileAsync(string projectDirectory, ObjectId programId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Generating UIComponent.py for program {ProgramId} in {ProjectDirectory}", programId, projectDirectory);

                var latestComponent = await _unitOfWork.UiComponents.GetLatestActiveByProgramAsync(programId, cancellationToken);

                if (latestComponent == null)
                {
                    _logger.LogInformation("No active UI component found for program {ProgramId}, skipping UIComponent.py generation", programId);
                    return;
                }

                var pythonCode = UIComponentPythonGenerator.GenerateUIComponentPython(latestComponent);
                var uiComponentPath = Path.Combine(projectDirectory, "UIComponent.py");

                await File.WriteAllTextAsync(uiComponentPath, pythonCode, cancellationToken);

                _logger.LogInformation("Successfully generated UIComponent.py for component '{ComponentName}' ({ComponentId})",
                    latestComponent.Name, latestComponent._ID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate UIComponent.py for program {ProgramId} in {ProjectDirectory}", programId, projectDirectory);
                // Don't fail the execution, just log the error
            }
        }

        public async Task GenerateWorkflowInputsFileAsync(string projectDirectory, Dictionary<string, string> environment, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if WorkflowInputs content is provided via environment variable
                if (environment.TryGetValue("WORKFLOW_INPUTS_CONTENT", out var workflowInputsContent) && !string.IsNullOrEmpty(workflowInputsContent))
                {
                    _logger.LogInformation("Generating WorkflowInputs.py for project in {ProjectDirectory}", projectDirectory);

                    var workflowInputsPath = Path.Combine(projectDirectory, "WorkflowInputs.py");
                    await File.WriteAllTextAsync(workflowInputsPath, workflowInputsContent, cancellationToken);

                    _logger.LogInformation("Successfully generated WorkflowInputs.py in {ProjectDirectory}", projectDirectory);
                }
                else
                {
                    _logger.LogDebug("No WorkflowInputs content found in environment variables, skipping WorkflowInputs.py generation");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate WorkflowInputs.py in {ProjectDirectory}", projectDirectory);
                // Don't fail the execution, just log the error
            }
        }

        // Simplified ExecuteAsync method in PythonProjectRunner.cs (if using base class methods):

        public override async Task<ProjectExecutionResult> ExecuteAsync(ProjectExecutionContext context, CancellationToken cancellationToken = default)
        {
            var result = new ProjectExecutionResult
            {
                ExecutionId = context.ExecutionId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // FIX: Convert the relative path to an absolute path
                var absoluteProjectDirectory = Path.GetFullPath(context.ProjectDirectory);

                _logger.LogInformation("Executing Python project in {ProjectDirectory}", absoluteProjectDirectory);

                // Process input files from parameters (using base class method)
                await ProcessInputFilesFromParametersAsync(context, cancellationToken);

                // Generate UIComponent.py file based on the latest active UI component for the program
                var programId = ExtractProgramIdFromContext(context);
                if (programId != ObjectId.Empty)
                {
                    await GenerateUIComponentFileAsync(absoluteProjectDirectory, programId, cancellationToken);
                }

                // Generate WorkflowInputs.py file if content is provided via environment variables
                await GenerateWorkflowInputsFileAsync(absoluteProjectDirectory, context.Environment, cancellationToken);

                var entryPoint = context.ProjectStructure.MainEntryPoint ?? "main.py";
                if (!File.Exists(Path.Combine(absoluteProjectDirectory, entryPoint)))
                {
                    entryPoint = FindBestEntryPoint(absoluteProjectDirectory);
                }

                if (string.IsNullOrEmpty(entryPoint))
                {
                    result.Success = false;
                    result.ErrorMessage = "No Python entry point found";
                    return result;
                }

                var arguments = entryPoint;

                // Add parameters (using base class method)
                if (context.Parameters != null && context.Parameters.ToString() != "{}")
                {
                    var processedParams = ProcessParametersForExecution(context.Parameters, absoluteProjectDirectory);
                    //var paramJson = JsonSerializer.Serialize(processedParams);

                    if (context.Parameters is BsonDocument doc)
                    {
                        var paramJson = JsonSerializer.Serialize(processedParams);
                        arguments += $" '{paramJson.ToString()}'";
                    }
                    else
                    {
                        var objectParameters = JsonSerializer.Serialize( processedParams);
                        arguments += $" '{objectParameters.ToString()}'";
                    }
                }

                ProcessResult processResult;

                if (_settings.EnableDocker && _settings.DockerImages.TryGetValue("Python", out var dockerImage))
                {
                    // ============================================================================
                    // DOCKER-BASED EXECUTION WITH TIER-AWARE DISPATCHING
                    // ============================================================================
                    // This section prepares for Docker-based execution and dispatches to the
                    // appropriate tier-specific execution method based on the ExecutionTier
                    // value set by the ExecutionService dispatcher.
                    // ============================================================================

                    // Use Docker for secure sandboxed execution
                    var outputDir = Path.Combine(Path.GetDirectoryName(absoluteProjectDirectory) ?? "", "outputs");
                    Directory.CreateDirectory(outputDir);

                    // Prepare volumes for package access
                    Dictionary<string, string>? volumes = null;
                    if (!string.IsNullOrEmpty(context.PackageVolumeName))
                    {
                        volumes = new Dictionary<string, string>
                        {
                            { context.PackageVolumeName, "/home/executor/.local" }
                        };

                        // THE FIX: Tell Python where to find the packages installed in the volume
                        context.Environment["PYTHONPATH"] = "/home/executor/.local/lib/python3.12/site-packages";
                    }

                    // ============================================================================
                    // TIERED EXECUTION: Worker-Level Tier Dispatching
                    // ============================================================================
                    // This logic connects the dispatcher's decision (ExecutionService) to the
                    // worker's concrete action (PythonProjectRunner).
                    //
                    // Flow:
                    // 1. Check if tiered execution is enabled AND a tier was specified
                    // 2. If tier is "RAM": Use ExecuteWithRamTierAsync() with tmpfs and OOM recovery
                    // 3. If tier is "Disk": Use ExecuteWithDiskTierAsync() with persistent volumes
                    // 4. If tier is unknown: Log warning and fall back to standard execution
                    // 5. If tiered execution disabled/no tier: Use standard execution (backward compatible)
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
                                "python",
                                $"/app/{arguments}",
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
                                "python",
                                $"/app/{arguments}",
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
                        // Unknown Tier: Fall back to standard execution for safety
                        else
                        {
                            _logger.LogWarning(
                                "Execution {ExecutionId}: Unknown execution tier '{Tier}' specified. " +
                                "Valid tiers are 'RAM' or 'Disk'. Falling back to standard execution for safety.",
                                context.ExecutionId, context.ExecutionTier);

                            processResult = await RunDockerProcessAsync(
                                dockerImage,
                                "python",
                                $"/app/{arguments}",
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

                            _logger.LogInformation(
                                "Execution {ExecutionId}: Fallback standard execution completed. Success: {Success}, Exit Code: {ExitCode}",
                                context.ExecutionId, processResult.Success, processResult.ExitCode);
                        }
                    }
                    else
                    {
                        // ========================================================================
                        // STANDARD EXECUTION (Backward Compatible)
                        // ========================================================================
                        // This path is taken when:
                        // - Tiered execution is disabled globally, OR
                        // - No tier was specified in the context (context.ExecutionTier is null/empty)
                        //
                        // This ensures backward compatibility with existing code and configurations
                        // that don't use tiered execution.
                        // ========================================================================

                        if (_settings.TieredExecution.EnableTieredExecution)
                        {
                            _logger.LogDebug(
                                "Execution {ExecutionId}: Tiered execution is enabled but no tier specified. Using standard execution.",
                                context.ExecutionId);
                        }
                        else
                        {
                            _logger.LogDebug(
                                "Execution {ExecutionId}: Tiered execution is disabled. Using standard execution.",
                                context.ExecutionId);
                        }

                        processResult = await RunDockerProcessAsync(
                            dockerImage,
                            "python",
                            $"/app/{arguments}",
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

                        _logger.LogInformation(
                            "Execution {ExecutionId}: Standard execution completed. Success: {Success}, Exit Code: {ExitCode}",
                            context.ExecutionId, processResult.Success, processResult.ExitCode);
                    }
                    // ============================================================================
                    // END OF TIER-AWARE DISPATCHING
                    // ============================================================================
                }
                else
                {
                    // Fallback to direct execution (less secure)
                    _logger.LogWarning("Docker is disabled. Using direct execution (not recommended for production).");
                    processResult = await RunProcessAsync(
                        "python",
                        arguments,
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

        private long EstimateMemoryUsage(string? output)
        {
            var outputSize = output?.Length ?? 0;
            return Math.Max(outputSize * 8, 30 * 1024 * 1024); // Minimum 30MB
        }
    }
}