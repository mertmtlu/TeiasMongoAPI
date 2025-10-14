// TeiasMongoAPI.Services/Services/Implementations/Execution/ProjectExecutionEngine.cs
using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Interfaces.Execution;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations.Execution
{
    public class ProjectExecutionEngine : BaseService, IProjectExecutionEngine
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IProgramService _programService;
        private readonly IVersionService _versionService;
        private readonly IEnumerable<IProjectLanguageRunner> _languageRunners;
        private readonly ProjectExecutionSettings _settings;
        private readonly IExecutionOutputStreamingService? _streamingService;
        private readonly Dictionary<string, ExecutionSession> _activeSessions = new();

        public ProjectExecutionEngine(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IFileStorageService fileStorageService,
            IProgramService programService,
            IVersionService versionService,
            IEnumerable<IProjectLanguageRunner> languageRunners,
            IOptions<ProjectExecutionSettings> settings,
            ILogger<ProjectExecutionEngine> logger,
            IExecutionOutputStreamingService? streamingService = null)
            : base(unitOfWork, mapper, logger)
        {
            _fileStorageService = fileStorageService;
            _programService = programService;
            _versionService = versionService;
            _languageRunners = languageRunners.OrderBy(r => r.Priority);
            _settings = settings.Value;
            _streamingService = streamingService;
        }

        public async Task<ProjectExecutionResult> ExecuteProjectAsync(ProjectExecutionRequest request, string executionId, CancellationToken cancellationToken = default)
        {
            var session = new ExecutionSession
            {
                ExecutionId = executionId,
                StartedAt = DateTime.UtcNow,
                Request = request,
                CancellationTokenSource = new CancellationTokenSource()
            };

            _activeSessions[executionId] = session;

            // Create a unique Docker volume for this execution to persist packages between build and execution
            string? packageVolumeName = null;
            if (_settings.EnableDocker)
            {
                packageVolumeName = $"pip-cache-{executionId}";
                session.PackageVolumeName = packageVolumeName;
            }

            try
            {
                _logger.LogInformation("Starting project execution {ExecutionId} for program {ProgramId}",
                    executionId, request.ProgramId);

                // Create Docker volume for package persistence (if Docker is enabled)
                if (!string.IsNullOrEmpty(packageVolumeName))
                {
                    await CreateDockerVolumeAsync(packageVolumeName, cancellationToken);
                }

                // Set up execution timeout using settings
                var timeoutMinutes = _settings.DefaultTimeoutMinutes;
                var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, session.CancellationTokenSource.Token);
                timeoutCts.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));

                // Step 1: Resolve version to execute
                var versionId = await ResolveExecutionVersionAsync(request.ProgramId, request.VersionId, timeoutCts.Token);
                session.VersionId = versionId;

                // Step 2: Validate program and version
                await ValidateProgramAndVersionAsync(request.ProgramId, versionId, timeoutCts.Token);

                // Step 3: Create execution directory structure
                var executionDirectory = CreateExecutionDirectory(request.ProgramId, versionId, executionId);
                session.ExecutionDirectory = executionDirectory;

                // Step 4: Extract project files to execution directory
                var projectDirectory = await ExtractProjectFilesAsync(request.ProgramId, versionId, executionDirectory, timeoutCts.Token);
                session.ProjectDirectory = projectDirectory;

                // Step 5: Analyze project structure
                var projectStructure = await AnalyzeProjectStructureAsync(projectDirectory, timeoutCts.Token);
                session.ProjectStructure = projectStructure;

                // Step 6: Validate project
                var validation = await ValidateExtractedProjectAsync(projectDirectory, timeoutCts.Token);
                if (!validation.IsValid)
                {
                    return CreateFailureResult(executionId, session, $"Project validation failed: {string.Join(", ", validation.Errors)}");
                }

                // Step 7: Find appropriate language runner
                var runner = await FindLanguageRunnerAsync(projectDirectory, projectStructure, timeoutCts.Token);
                if (runner == null)
                {
                    return CreateFailureResult(executionId, session, $"No suitable language runner found for project type: {projectStructure.Language}");
                }

                session.LanguageRunner = runner;

                // Step 8: Build project if needed
                ProjectBuildResult? buildResult = null;
                if (projectStructure.HasBuildFile && !(request.BuildArgs?.SkipBuild ?? false))
                {
                    var buildArgs = request.BuildArgs ?? new ProjectBuildArgs();
                    buildArgs.PackageVolumeName = packageVolumeName; // Pass volume name to build
                    buildResult = await BuildProjectAsync(runner, projectDirectory, buildArgs, timeoutCts.Token);
                    if (!buildResult.Success)
                    {
                        _logger.LogError("Build has failed with output: {output}", buildResult.Output);
                        return CreateFailureResult(executionId, session, $"Build failed: {buildResult.ErrorMessage}", buildResult);
                    }
                }

                // Step 8.1: Capture initial file snapshot before build/execution
                var initialFiles = new HashSet<string>(Directory.GetFiles(projectDirectory, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(projectDirectory, f)));

                session.InitialFiles = initialFiles;

                // Step 9: Execute project with enhanced exception handling for long-running processes
                var executionContext = CreateExecutionContext(executionId, projectDirectory, request, projectStructure, packageVolumeName, timeoutCts.Token);
                ProjectExecutionResult result;

                try
                {
                    result = await runner.ExecuteAsync(executionContext, timeoutCts.Token);
                    _logger.LogInformation("Project execution completed for {ExecutionId} with success: {Success}, exit code: {ExitCode}",
                        executionId, result.Success, result.ExitCode);
                }
                catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Timeout occurred but not user cancellation
                    _logger.LogWarning("Project execution {ExecutionId} timed out after {TimeoutMinutes} minutes", executionId, timeoutMinutes);

                    // Notify client of timeout via streaming service
                    if (_streamingService != null)
                    {
                        try
                        {
                            var completedArgs = new ExecutionCompletedEventArgs
                            {
                                ExecutionId = executionId,
                                Status = "timed_out",
                                ExitCode = -1,
                                ErrorMessage = "Execution timed out.",
                                CompletedAt = DateTime.UtcNow,
                                Duration = DateTime.UtcNow - session.StartedAt,
                                Success = false,
                                OutputFiles = new List<string>()
                            };
                            await _streamingService.StreamExecutionCompletedAsync(executionId, completedArgs, cancellationToken);
                        }
                        catch (Exception streamEx)
                        {
                            _logger.LogWarning(streamEx, "Failed to stream timeout notification for execution {ExecutionId}", executionId);
                        }
                    }

                    return CreateFailureResult(executionId, session, $"Execution timed out after {timeoutMinutes} minutes");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // User/system cancellation
                    _logger.LogInformation("Project execution {ExecutionId} was cancelled by user/system", executionId);
                    return CreateFailureResult(executionId, session, "Execution was cancelled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Project execution {ExecutionId} failed during runner execution", executionId);
                    return CreateFailureResult(executionId, session, $"Execution failed: {ex.Message}");
                }

                // Step 10: Collect and store output files with cancellation safety
                if (request.SaveResults)
                {
                    try
                    {
                        result.OutputFiles = await CollectAndStoreOutputFilesAsync(projectDirectory, executionDirectory, session.InitialFiles, timeoutCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("File collection was cancelled for {ExecutionId}, but execution completed successfully", executionId);
                        // Don't fail the entire execution for file collection issues
                        result.OutputFiles = new List<string>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to collect output files for {ExecutionId}, but execution completed successfully", executionId);
                        result.OutputFiles = new List<string>();
                    }
                }

                // Step 11: Update result with session info
                result.ExecutionId = executionId;
                result.StartedAt = session.StartedAt;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;
                result.BuildResult = buildResult;

                // Step 12: Store execution logs with cancellation safety
                try
                {
                    await StoreExecutionLogsAsync(executionDirectory, result, timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Log storage was cancelled for {ExecutionId}, but execution completed successfully", executionId);
                    // Don't fail the entire execution for log storage issues
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to store execution logs for {ExecutionId}, but execution completed successfully", executionId);
                }

                _logger.LogInformation("Completed project execution {ExecutionId} with exit code {ExitCode} and duration {Duration}",
                    executionId, result.ExitCode, result.Duration?.TotalMinutes);

                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Project execution {ExecutionId} was cancelled during setup/cleanup", executionId);
                return CreateFailureResult(executionId, session, "Execution was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Project execution {ExecutionId} failed with unexpected exception", executionId);
                return CreateFailureResult(executionId, session, $"Execution failed: {ex.Message}");
            }
            finally
            {
                // Cleanup Docker volume
                if (!string.IsNullOrEmpty(packageVolumeName))
                {
                    await DeleteDockerVolumeAsync(packageVolumeName, CancellationToken.None);
                }

                // Cleanup temporary project files but preserve execution results
                if (request.CleanupOnCompletion)
                {
                    await CleanupSessionAsync(session);
                }
                _activeSessions.Remove(executionId);
            }
        }

        public async Task<ProjectValidationResult> ValidateProjectAsync(string programId, string versionId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Resolve version if not provided
                var resolvedVersionId = await ResolveExecutionVersionAsync(programId, versionId, cancellationToken);

                // Validate program and version
                await ValidateProgramAndVersionAsync(programId, resolvedVersionId, cancellationToken);

                // Create temporary directory for validation
                var tempDir = Path.Combine(_settings.WorkingDirectory, "validation", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                try
                {
                    await ExtractFilesToDirectoryAsync(programId, resolvedVersionId, tempDir, cancellationToken);
                    return await ValidateExtractedProjectAsync(tempDir, cancellationToken);
                }
                finally
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate project {ProgramId}/{VersionId}", programId, versionId);
                return new ProjectValidationResult
                {
                    IsValid = false,
                    Errors = { $"Validation failed: {ex.Message}" }
                };
            }
        }

        public async Task<ProjectStructureAnalysis> AnalyzeProjectStructureAsync(string programId, string versionId, CancellationToken cancellationToken = default)
        {
            try
            {
                // Resolve version if not provided
                var resolvedVersionId = await ResolveExecutionVersionAsync(programId, versionId, cancellationToken);

                // Validate program and version
                await ValidateProgramAndVersionAsync(programId, resolvedVersionId, cancellationToken);

                var tempDir = Path.Combine(_settings.WorkingDirectory, "analysis", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                try
                {
                    await ExtractFilesToDirectoryAsync(programId, resolvedVersionId, tempDir, cancellationToken);
                    return await AnalyzeProjectStructureAsync(tempDir, cancellationToken);
                }
                finally
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze project structure {ProgramId}/{VersionId}", programId, versionId);
                return new ProjectStructureAnalysis
                {
                    Language = "Unknown",
                    ProjectType = "Unknown",
                    Complexity = new ProjectComplexity { ComplexityLevel = "Unknown" }
                };
            }
        }

        public async Task<bool> CancelExecutionAsync(string executionId, CancellationToken cancellationToken = default)
        {
            if (_activeSessions.TryGetValue(executionId, out var session))
            {
                session.CancellationTokenSource.Cancel();
                _logger.LogInformation("Cancelled project execution {ExecutionId}", executionId);
                return true;
            }
            return false;
        }

        public async Task<List<string>> GetSupportedLanguagesAsync(CancellationToken cancellationToken = default)
        {
            return _languageRunners.Select(r => r.Language).ToList();
        }

        #region Private Helper Methods

        /// <summary>
        /// Resolves the version to execute. If versionId is provided, validates it exists.
        /// If not provided, gets the current version from the program.
        /// </summary>
        private async Task<string> ResolveExecutionVersionAsync(string programId, string? versionId, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(versionId))
            {
                // Validate the specified version exists
                try
                {
                    await _versionService.GetByIdAsync(versionId, cancellationToken);
                    return versionId;
                }
                catch (KeyNotFoundException)
                {
                    throw new KeyNotFoundException($"Version with ID {versionId} not found");
                }
            }

            // Get current version from program
            try
            {
                var program = await _programService.GetByIdAsync(programId, cancellationToken);
                if (!string.IsNullOrEmpty(program.CurrentVersion))
                {
                    return program.CurrentVersion;
                }

                // Fall back to latest approved version
                var latestVersion = await _versionService.GetLatestVersionForProgramAsync(programId, cancellationToken);
                return latestVersion.Id;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not resolve version for program {programId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates that the program and version exist and are in proper state for execution
        /// </summary>
        private async Task ValidateProgramAndVersionAsync(string programId, string versionId, CancellationToken cancellationToken)
        {
            // Validate program exists
            try
            {
                var program = await _programService.GetByIdAsync(programId, cancellationToken);
                if (program.Status == "archived" || program.Status == "deleted")
                {
                    throw new InvalidOperationException($"Cannot execute archived or deleted program {programId}");
                }
            }
            catch (KeyNotFoundException)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found");
            }

            // Validate version exists and is approved
            try
            {
                var version = await _versionService.GetByIdAsync(versionId, cancellationToken);
                if (version.Status != "approved")
                {
                    throw new InvalidOperationException($"Can only execute approved versions. Version {versionId} has status: {version.Status}");
                }
            }
            catch (KeyNotFoundException)
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found");
            }
        }

        /// <summary>
        /// Creates the execution directory structure: ./storage/ProgramId/VersionId/execution/ExecutionId
        /// </summary>
        private string CreateExecutionDirectory(string programId, string versionId, string executionId)
        {
            var executionPath = Path.Combine(_settings.WorkingDirectory, programId, versionId, "execution", executionId);
            Directory.CreateDirectory(executionPath);

            // Create subdirectories
            Directory.CreateDirectory(Path.Combine(executionPath, "project"));
            Directory.CreateDirectory(Path.Combine(executionPath, "outputs"));
            Directory.CreateDirectory(Path.Combine(executionPath, "logs"));

            _logger.LogDebug("Created execution directory structure at {ExecutionPath}", executionPath);
            return executionPath;
        }

        /// <summary>
        /// Extracts project files from version storage to execution directory
        /// </summary>
        private async Task<string> ExtractProjectFilesAsync(string programId, string versionId, string executionDirectory, CancellationToken cancellationToken)
        {
            var projectDirectory = Path.Combine(executionDirectory, "project");
            await ExtractFilesToDirectoryAsync(programId, versionId, projectDirectory, cancellationToken);

            _logger.LogDebug("Extracted project files to {ProjectDirectory}", projectDirectory);
            return projectDirectory;
        }

        /// <summary>
        /// Extracts files from version storage to a target directory
        /// </summary>
        private async Task ExtractFilesToDirectoryAsync(string programId, string versionId, string targetDirectory, CancellationToken cancellationToken)
        {
            try
            {
                var files = await _fileStorageService.ListVersionFilesAsync(programId, versionId, cancellationToken);

                foreach (var file in files)
                {
                    try
                    {
                        var content = await _fileStorageService.GetFileContentAsync(programId, versionId, file.Path, cancellationToken);
                        var targetPath = Path.Combine(targetDirectory, file.Path.TrimStart('/'));
                        var targetDir = Path.GetDirectoryName(targetPath);

                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        await File.WriteAllBytesAsync(targetPath, content, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract file {FilePath} from program {ProgramId}, version {VersionId}",
                            file.Path, programId, versionId);
                    }
                }

                _logger.LogDebug("Extracted {FileCount} files from program {ProgramId}, version {VersionId}",
                    files.Count, programId, versionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract files from program {ProgramId}, version {VersionId}", programId, versionId);
                throw;
            }
        }

        /// <summary>
        /// Analyzes the project structure in the extracted directory
        /// </summary>
        private async Task<ProjectStructureAnalysis> AnalyzeProjectStructureAsync(string projectDirectory, CancellationToken cancellationToken)
        {
            var analysis = new ProjectStructureAnalysis();

            try
            {
                // Get all files
                var allFiles = Directory.GetFiles(projectDirectory, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(projectDirectory, f))
                    .ToList();

                analysis.Files = allFiles.Select(f => new ProjectFile
                {
                    Path = f,
                    Extension = Path.GetExtension(f),
                    Size = new FileInfo(Path.Combine(projectDirectory, f)).Length,
                    Type = DetermineFileType(f),
                    LineCount = EstimateLineCount(new FileInfo(Path.Combine(projectDirectory, f)).Length)
                }).ToList();

                analysis.SourceFiles = allFiles.Where(f => IsSourceFile(f)).ToList();

                // Try each language runner to analyze
                foreach (var runner in _languageRunners)
                {
                    if (await runner.CanHandleProjectAsync(projectDirectory, cancellationToken))
                    {
                        var runnerAnalysis = await runner.AnalyzeProjectAsync(projectDirectory, cancellationToken);

                        // Merge analysis results
                        analysis.Language = runnerAnalysis.Language;
                        analysis.ProjectType = runnerAnalysis.ProjectType;
                        analysis.EntryPoints = runnerAnalysis.EntryPoints;
                        analysis.ConfigFiles = runnerAnalysis.ConfigFiles;
                        analysis.Dependencies = runnerAnalysis.Dependencies;
                        analysis.HasBuildFile = runnerAnalysis.HasBuildFile;
                        analysis.MainEntryPoint = runnerAnalysis.MainEntryPoint;
                        analysis.Metadata = runnerAnalysis.Metadata;

                        break;
                    }
                }

                // Calculate complexity
                analysis.Complexity = CalculateProjectComplexity(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze project structure in {ProjectDirectory}", projectDirectory);
                analysis.Language = "Unknown";
                analysis.ProjectType = "Unknown";
                analysis.Complexity = new ProjectComplexity { ComplexityLevel = "Unknown" };
            }

            return analysis;
        }

        /// <summary>
        /// Validates the extracted project for security and basic integrity
        /// </summary>
        private async Task<ProjectValidationResult> ValidateExtractedProjectAsync(string projectDirectory, CancellationToken cancellationToken)
        {
            var result = new ProjectValidationResult { IsValid = true };

            try
            {
                // Basic file system validation
                if (!Directory.Exists(projectDirectory))
                {
                    result.IsValid = false;
                    result.Errors.Add("Project directory does not exist");
                    return result;
                }

                var files = Directory.GetFiles(projectDirectory, "*", SearchOption.AllDirectories);
                if (files.Length == 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Project directory is empty");
                    return result;
                }

                // Check for blocked file extensions
                var blockedFiles = files.Where(f => _settings.BlockedFileExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
                if (blockedFiles.Any())
                {
                    result.Warnings.AddRange(blockedFiles.Select(f => $"Potentially unsafe file: {Path.GetFileName(f)}"));
                }

                // Security scan
                if (_settings.EnableSecurityScanning)
                {
                    result.SecurityScan = await PerformSecurityScanAsync(projectDirectory, cancellationToken);
                    if (result.SecurityScan.HasSecurityIssues)
                    {
                        result.Warnings.AddRange(result.SecurityScan.Issues.Select(i => $"Security: {i.Description}"));
                    }
                }

                // Project size validation
                var totalSize = files.Sum(f => new FileInfo(f).Length);
                if (totalSize > _settings.MaxProjectSizeBytes)
                {
                    result.Errors.Add($"Project size ({totalSize:N0} bytes) exceeds maximum allowed size ({_settings.MaxProjectSizeBytes:N0} bytes)");
                    result.IsValid = false;
                }

                // Try to find a suitable runner for validation
                foreach (var runner in _languageRunners)
                {
                    if (await runner.CanHandleProjectAsync(projectDirectory, cancellationToken))
                    {
                        var runnerValidation = await runner.ValidateProjectAsync(projectDirectory, cancellationToken);
                        result.Errors.AddRange(runnerValidation.Errors);
                        result.Warnings.AddRange(runnerValidation.Warnings);
                        result.Suggestions.AddRange(runnerValidation.Suggestions);

                        if (!runnerValidation.IsValid)
                        {
                            result.IsValid = false;
                        }
                        break;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate project at {ProjectDirectory}", projectDirectory);
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Finds the appropriate language runner for the project
        /// </summary>
        private async Task<IProjectLanguageRunner?> FindLanguageRunnerAsync(string projectDirectory, ProjectStructureAnalysis analysis, CancellationToken cancellationToken)
        {
            foreach (var runner in _languageRunners)
            {
                if (await runner.CanHandleProjectAsync(projectDirectory, cancellationToken))
                {
                    return runner;
                }
            }
            return null;
        }

        /// <summary>
        /// Builds the project using the appropriate language runner
        /// </summary>
        private async Task<ProjectBuildResult> BuildProjectAsync(IProjectLanguageRunner runner, string projectDirectory, ProjectBuildArgs buildArgs, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Building project in {ProjectDirectory} using {Runner}", projectDirectory, runner.Language);

                var result = await runner.BuildProjectAsync(projectDirectory, buildArgs, cancellationToken);
                result.Duration = stopwatch.Elapsed;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Build failed for project in {ProjectDirectory}", projectDirectory);
                return new ProjectBuildResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Duration = stopwatch.Elapsed
                };
            }
        }

        /// <summary>
        /// Creates the execution context for the language runner
        /// </summary>
        private ProjectExecutionContext CreateExecutionContext(string executionId, string projectDirectory, ProjectExecutionRequest request, ProjectStructureAnalysis projectStructure, string? packageVolumeName, CancellationToken cancellationToken)
        {
            var resourceLimits = request.ResourceLimits ?? new ProjectResourceLimits();

            return new ProjectExecutionContext
            {
                ExecutionId = executionId,
                ProjectDirectory = projectDirectory,
                UserId = request.UserId,
                Parameters = request.Parameters,
                Environment = request.Environment,
                ResourceLimits = resourceLimits,
                ProjectStructure = projectStructure,
                CancellationToken = cancellationToken,
                WorkingDirectory = projectDirectory,
                OutputCallback = output => _logger.LogDebug("Execution output: {Output}", output),
                ErrorCallback = error => _logger.LogWarning("Execution error: {Error}", error),
                PackageVolumeName = packageVolumeName,

                // TIERED EXECUTION: Pass tier information to language runners
                ExecutionTier = request.ExecutionTier,  // "RAM" or "Disk"
                JobProfile = request.JobProfile          // Job profile name
            };
        }

        /// <summary>
        /// Collects output files and stores them in the execution directory
        /// </summary>
        private async Task<List<string>> CollectAndStoreOutputFilesAsync(
    string projectDirectory,
    string executionDirectory,
    HashSet<string>? initialFiles,
    CancellationToken cancellationToken)
        {
            // --- 1. Setup: Prepare constants and lookups for high performance ---

            // Using HashSet provides O(1) "Contains" checks, which is much faster than a List.
            var outputSubDirs = new HashSet<string> {"dist", "build", "target", "out", "output" };
            var excludedFileNames = new HashSet<string> { "WorkflowInputs" };
            var excludedSubDirs = new HashSet<string> { "__pycache__", ".git", "node_modules", "bin", "obj" };

            var outputsDirectory = Path.Combine(executionDirectory, "outputs");
            var collectedOutputPaths = new List<string>();

            try
            {
                // --- 2. Identify all files to be copied in a single, efficient query ---

                var allProjectFiles = Directory.EnumerateFiles(projectDirectory, "*", SearchOption.AllDirectories);

                var filesToCopy = allProjectFiles.Where(filePath =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var relativePath = Path.GetRelativePath(projectDirectory, filePath);

                    var pathSegments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (pathSegments.Any(segment => excludedSubDirs.Contains(segment)))
                    {
                        return false;
                    }

                    // Rule: Ignore files with specifically excluded names.
                    if (excludedFileNames.Contains(Path.GetFileNameWithoutExtension(filePath)))
                    {
                        return false;
                    }

                    // Condition 1: Is the file new? (i.e., not in the initial snapshot).
                    bool isNewFile = initialFiles == null || !initialFiles.Contains(relativePath);

                    // Condition 2: Is the file part of a standard output directory?
                    var firstDir = pathSegments.FirstOrDefault();
                    bool isInStandardOutputDir = firstDir != null && outputSubDirs.Contains(firstDir);

                    // We copy the file if it's new OR if it resides in a known output folder.
                    return isNewFile || isInStandardOutputDir;
                })
                .Distinct();


                // --- 3. Copy the filtered files to the outputs directory ---

                foreach (var sourcePath in filesToCopy)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var relativePath = Path.GetRelativePath(projectDirectory, sourcePath);
                        var targetPath = Path.Combine(outputsDirectory, relativePath);
                        var targetDir = Path.GetDirectoryName(targetPath);

                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        File.Copy(sourcePath, targetPath, true);
                        collectedOutputPaths.Add(Path.GetFullPath(targetPath));
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "Failed to copy output file {SourceFile}", sourcePath);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Output file collection was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A critical error occurred while collecting output files from {ProjectDirectory}", projectDirectory);
            }

            return collectedOutputPaths;
        }

        /// <summary>
        /// Stores execution logs and metadata
        /// </summary>
        private async Task StoreExecutionLogsAsync(string executionDirectory, ProjectExecutionResult result, CancellationToken cancellationToken)
        {
            try
            {
                var logsDirectory = Path.Combine(executionDirectory, "logs");

                // Store execution result as JSON
                var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(logsDirectory, "execution-result.json"), resultJson, cancellationToken);

                // Store output logs
                if (!string.IsNullOrEmpty(result.Output))
                {
                    await File.WriteAllTextAsync(Path.Combine(logsDirectory, "output.log"), result.Output, cancellationToken);
                }

                // Store error logs
                if (!string.IsNullOrEmpty(result.ErrorOutput))
                {
                    await File.WriteAllTextAsync(Path.Combine(logsDirectory, "error.log"), result.ErrorOutput, cancellationToken);
                }

                // Store execution metadata
                var metadata = new
                {
                    ExecutionId = result.ExecutionId,
                    StartedAt = result.StartedAt,
                    CompletedAt = result.CompletedAt,
                    Duration = result.Duration?.TotalSeconds,
                    ExitCode = result.ExitCode,
                    Success = result.Success,
                    ResourceUsage = result.ResourceUsage,
                    OutputFileCount = result.OutputFiles.Count
                };

                var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(Path.Combine(logsDirectory, "execution-metadata.json"), metadataJson, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store execution logs for execution directory {ExecutionDirectory}", executionDirectory);
            }
        }

        /// <summary>
        /// Performs security scanning on the project
        /// </summary>
        private async Task<ProjectSecurityScan> PerformSecurityScanAsync(string projectDirectory, CancellationToken cancellationToken)
        {
            var scan = new ProjectSecurityScan();

            try
            {
                var suspiciousPatterns = new[]
                {
                    "System.Diagnostics.Process.Start",
                    "Runtime.getRuntime().exec",
                    "os.system(",
                    "subprocess.call",
                    "eval(",
                    "exec(",
                    "import os",
                    "require('child_process')"
                };

                var files = Directory.GetFiles(projectDirectory, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (new[] { ".cs", ".py", ".java", ".js", ".ts", ".php", ".rb" }.Contains(extension))
                    {
                        try
                        {
                            var content = await File.ReadAllTextAsync(file, cancellationToken);

                            foreach (var pattern in suspiciousPatterns)
                            {
                                if (content.Contains(pattern))
                                {
                                    scan.Issues.Add(new SecurityIssue
                                    {
                                        Type = "SuspiciousCode",
                                        Description = $"Potentially dangerous pattern: {pattern}",
                                        File = Path.GetRelativePath(projectDirectory, file),
                                        Severity = "Medium"
                                    });
                                    if (!scan.SuspiciousPatterns.Contains(pattern))
                                    {
                                        scan.SuspiciousPatterns.Add(pattern);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to scan file {File} for security issues", file);
                        }
                    }
                }

                scan.HasSecurityIssues = scan.Issues.Any();
                scan.RiskLevel = CalculateRiskLevel(scan.Issues);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Security scan failed for {ProjectDirectory}", projectDirectory);
            }

            return scan;
        }

        /// <summary>
        /// Calculates project complexity metrics
        /// </summary>
        private ProjectComplexity CalculateProjectComplexity(ProjectStructureAnalysis analysis)
        {
            var complexity = new ProjectComplexity
            {
                TotalFiles = analysis.Files.Count,
                Dependencies = analysis.Dependencies.Count,
                TotalLines = analysis.Files.Sum(f => f.LineCount)
            };

            // Calculate complexity score
            var score = 0.0;
            score += Math.Min(complexity.TotalFiles * 0.1, 5.0);
            score += Math.Min(complexity.Dependencies * 0.2, 3.0);
            score += Math.Min(complexity.TotalLines / 1000.0, 2.0);

            complexity.ComplexityScore = score;
            complexity.ComplexityLevel = score switch
            {
                < 2.0 => "Simple",
                < 5.0 => "Moderate",
                < 8.0 => "Complex",
                _ => "Very Complex"
            };

            return complexity;
        }

        /// <summary>
        /// Creates a failure result with session information
        /// </summary>
        private ProjectExecutionResult CreateFailureResult(string executionId, ExecutionSession session, string errorMessage, ProjectBuildResult? buildResult = null)
        {
            return new ProjectExecutionResult
            {
                ExecutionId = executionId,
                Success = false,
                ExitCode = -1,
                ErrorMessage = errorMessage,
                StartedAt = session.StartedAt,
                CompletedAt = DateTime.UtcNow,
                Duration = DateTime.UtcNow - session.StartedAt,
                BuildResult = buildResult,
                ResourceUsage = new ProjectResourceUsage()
            };
        }

        /// <summary>
        /// Cleans up temporary session files while preserving execution results
        /// </summary>
        private async Task CleanupSessionAsync(ExecutionSession session)
        {
            try
            {
                // Only cleanup the project directory, preserve execution results
                if (!string.IsNullOrEmpty(session.ProjectDirectory) && Directory.Exists(session.ProjectDirectory))
                {
                    Directory.Delete(session.ProjectDirectory, true);
                    _logger.LogDebug("Cleaned up project directory {ProjectDirectory}", session.ProjectDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup session directory {ProjectDirectory}", session.ProjectDirectory);
            }
        }

        /// <summary>
        /// Determines the type of a file based on its extension
        /// </summary>
        private string DetermineFileType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".cs" => "C# Source",
                ".py" => "Python Source",
                ".java" => "Java Source",
                ".js" => "JavaScript",
                ".ts" => "TypeScript",
                ".json" => "JSON Config",
                ".xml" => "XML Config",
                ".yml" or ".yaml" => "YAML Config",
                ".csproj" => "C# Project",
                ".sln" => "Visual Studio Solution",
                ".pom" => "Maven Project",
                ".gradle" => "Gradle Build",
                ".txt" => "Text",
                ".md" => "Markdown",
                ".html" => "HTML",
                ".css" => "Stylesheet",
                ".dockerfile" => "Docker File",
                _ => "Other"
            };
        }

        /// <summary>
        /// Checks if a file is a source code file
        /// </summary>
        private bool IsSourceFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return new[] { ".cs", ".py", ".java", ".js", ".ts", ".cpp", ".c", ".h", ".hpp", ".rs", ".go" }.Contains(extension);
        }

        /// <summary>
        /// Estimates line count from file size
        /// </summary>
        private int EstimateLineCount(long fileSize)
        {
            // Rough estimate: 50 characters per line
            return Math.Max(1, (int)(fileSize / 50));
        }

        /// <summary>
        /// Calculates security risk level
        /// </summary>
        private int CalculateRiskLevel(List<SecurityIssue> issues)
        {
            return issues.Count switch
            {
                0 => 1,
                < 3 => 2,
                < 6 => 3,
                < 10 => 4,
                _ => 5
            };
        }

        /// <summary>
        /// Creates a Docker volume for persisting packages between build and execution
        /// </summary>
        private async Task CreateDockerVolumeAsync(string volumeName, CancellationToken cancellationToken)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"volume create {volumeName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync(cancellationToken);
                    if (process.ExitCode == 0)
                    {
                        _logger.LogInformation("Created Docker volume {VolumeName} for package persistence", volumeName);
                    }
                    else
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        _logger.LogWarning("Failed to create Docker volume {VolumeName}: {Error}", volumeName, error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create Docker volume {VolumeName}", volumeName);
            }
        }

        /// <summary>
        /// Deletes a Docker volume to clean up resources
        /// </summary>
        private async Task DeleteDockerVolumeAsync(string volumeName, CancellationToken cancellationToken)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"volume rm {volumeName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync(cancellationToken);
                    if (process.ExitCode == 0)
                    {
                        _logger.LogInformation("Deleted Docker volume {VolumeName}", volumeName);
                    }
                    else
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        _logger.LogWarning("Failed to delete Docker volume {VolumeName}: {Error}", volumeName, error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete Docker volume {VolumeName}", volumeName);
            }
        }

        #endregion

        #region Supporting Classes

        private class ExecutionSession
        {
            public string ExecutionId { get; set; } = string.Empty;
            public DateTime StartedAt { get; set; }
            public ProjectExecutionRequest Request { get; set; } = null!;
            public CancellationTokenSource CancellationTokenSource { get; set; } = new();
            public string VersionId { get; set; } = string.Empty;
            public string ExecutionDirectory { get; set; } = string.Empty;
            public string ProjectDirectory { get; set; } = string.Empty;
            public HashSet<string>? InitialFiles { get; set; }
            public ProjectStructureAnalysis? ProjectStructure { get; set; }
            public IProjectLanguageRunner? LanguageRunner { get; set; }
            public string? PackageVolumeName { get; set; }
        }

        #endregion
    }

    public class ProjectExecutionSettings
    {
        public string WorkingDirectory { get; set; } = "./storage";
        public int MaxConcurrentExecutions { get; set; } = 5;
        public int DefaultTimeoutMinutes { get; set; } = 2880;
        public long MaxProjectSizeBytes { get; set; } = 500 * 1024 * 1024; // 500MB
        public List<string> BlockedFileExtensions { get; set; } = new() { ".exe", ".bat", ".cmd", ".ps1", ".sh", ".scr", ".vbs" };
        public bool EnableSecurityScanning { get; set; } = true;
        public bool CleanupOnCompletion { get; set; } = true;
        public int ExecutionRetentionDays { get; set; } = 30;

        // Docker-based execution settings
        public bool EnableDocker { get; set; } = true;
        public Dictionary<string, string> DockerImages { get; set; } = new()
        {
            { "Python", "python-executor:latest" },
            { "NodeJs", "nodejs-executor:latest" },
            { "CSharp", "dotnet-executor:latest" },
            { "Java", "java-executor:latest" }
        };
        public bool EnableNetworkAccess { get; set; } = true;
        public ResourceLimits ResourceLimits { get; set; } = new();

        // Tiered Execution Settings
        public TieredExecutionSettings TieredExecution { get; set; } = new();
    }

    public class ResourceLimits
    {
        public int MemoryMB { get; set; } = 1024;
        public double CPUs { get; set; } = 2.0;
        public int ProcessLimit { get; set; } = 100;
        public int TempStorageMB { get; set; } = 256;
    }

    // Tiered Execution Configuration Classes
    public class TieredExecutionSettings
    {
        public bool EnableTieredExecution { get; set; } = false;
        public RamPoolSettings RamPool { get; set; } = new();
        public DiskPoolSettings DiskPool { get; set; } = new();
        public Dictionary<string, JobProfile> JobProfiles { get; set; } = new();
        public string DefaultJobProfile { get; set; } = "Standard";
        public TierSelectionStrategySettings TierSelectionStrategy { get; set; } = new();
    }

    public class RamPoolSettings
    {
        public int TotalCapacityGB { get; set; } = 16;
        public int MaxConcurrentJobs { get; set; } = 10;
        public int TmpfsBaseSizeMB { get; set; } = 512;
        public bool EnableIterativeRelaunch { get; set; } = true;
        public IterativeRelaunchSettings IterativeRelaunch { get; set; } = new();
    }

    public class IterativeRelaunchSettings
    {
        public int BaselineSizeMB { get; set; } = 512;
        public double MultiplierFactor { get; set; } = 1.5;
        public int MaxSizeMB { get; set; } = 4096;
        public int MaxRetries { get; set; } = 3;
        public List<string> TriggerPatterns { get; set; } = new();
    }

    public class DiskPoolSettings
    {
        public int MaxConcurrentJobs { get; set; } = 5;
        public string DiskVolumePath { get; set; } = "./storage/disk_volumes";
        public bool EnableVolumeReuse { get; set; } = true;
        public int VolumeCleanupDelayMinutes { get; set; } = 60;
    }

    public class JobProfile
    {
        public string Description { get; set; } = string.Empty;
        public double RamCapacityCostGB { get; set; }
        public double CpuCost { get; set; }
        public string PreferredTier { get; set; } = "RAM";
        public int MaxExecutionMinutes { get; set; }
    }

    public class TierSelectionStrategySettings
    {
        public bool EnableAutoTierSelection { get; set; } = true;
        public bool FallbackToDisk { get; set; } = true;
        public string RamPoolFullBehavior { get; set; } = "Queue";
        public int MaxQueueDepth { get; set; } = 20;
        public int QueueTimeoutMinutes { get; set; } = 30;
    }
}