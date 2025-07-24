// TeiasMongoAPI.Services/Services/Implementations/Execution/ProjectExecutionEngine.cs
using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
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
        private readonly Dictionary<string, ExecutionSession> _activeSessions = new();

        public ProjectExecutionEngine(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IFileStorageService fileStorageService,
            IProgramService programService,
            IVersionService versionService,
            IEnumerable<IProjectLanguageRunner> languageRunners,
            IOptions<ProjectExecutionSettings> settings,
            ILogger<ProjectExecutionEngine> logger)
            : base(unitOfWork, mapper, logger)
        {
            _fileStorageService = fileStorageService;
            _programService = programService;
            _versionService = versionService;
            _languageRunners = languageRunners.OrderBy(r => r.Priority);
            _settings = settings.Value;
        }

        public async Task<ProjectExecutionResult> ExecuteProjectAsync(ProjectExecutionRequest request, CancellationToken cancellationToken = default)
        {
            var executionId = Guid.NewGuid().ToString();
            var session = new ExecutionSession
            {
                ExecutionId = executionId,
                StartedAt = DateTime.UtcNow,
                Request = request,
                CancellationTokenSource = new CancellationTokenSource()
            };

            _activeSessions[executionId] = session;

            try
            {
                _logger.LogInformation("Starting project execution {ExecutionId} for program {ProgramId}",
                    executionId, request.ProgramId);

                // Set up execution timeout
                var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, session.CancellationTokenSource.Token);
                timeoutCts.CancelAfter(TimeSpan.FromMinutes(request.TimeoutMinutes));

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
                    buildResult = await BuildProjectAsync(runner, projectDirectory, request.BuildArgs ?? new(), timeoutCts.Token);
                    if (!buildResult.Success)
                    {
                        return CreateFailureResult(executionId, session, $"Build failed: {buildResult.ErrorMessage}", buildResult);
                    }
                }

                // Step 9: Execute project
                var executionContext = CreateExecutionContext(executionId, projectDirectory, request, projectStructure, timeoutCts.Token);
                var result = await runner.ExecuteAsync(executionContext, timeoutCts.Token);

                // Step 10: Collect and store output files
                if (request.SaveResults)
                {
                    result.OutputFiles = await CollectAndStoreOutputFilesAsync(projectDirectory, executionDirectory, timeoutCts.Token);
                }

                // Step 11: Update result with session info
                result.ExecutionId = executionId;
                result.StartedAt = session.StartedAt;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;
                result.BuildResult = buildResult;

                // Step 12: Store execution logs
                await StoreExecutionLogsAsync(executionDirectory, result, timeoutCts.Token);

                _logger.LogInformation("Completed project execution {ExecutionId} with exit code {ExitCode}",
                    executionId, result.ExitCode);

                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Project execution {ExecutionId} was cancelled", executionId);
                return CreateFailureResult(executionId, session, "Execution was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Project execution {ExecutionId} failed with exception", executionId);
                return CreateFailureResult(executionId, session, $"Execution failed: {ex.Message}");
            }
            finally
            {
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
        private ProjectExecutionContext CreateExecutionContext(string executionId, string projectDirectory, ProjectExecutionRequest request, ProjectStructureAnalysis projectStructure, CancellationToken cancellationToken)
        {
            return new ProjectExecutionContext
            {
                ExecutionId = executionId,
                ProjectDirectory = projectDirectory,
                UserId = request.UserId,
                Parameters = request.Parameters,
                Environment = request.Environment,
                ResourceLimits = request.ResourceLimits ?? new ProjectResourceLimits(),
                ProjectStructure = projectStructure,
                CancellationToken = cancellationToken,
                WorkingDirectory = projectDirectory,
                OutputCallback = output => _logger.LogDebug("Execution output: {Output}", output),
                ErrorCallback = error => _logger.LogWarning("Execution error: {Error}", error)
            };
        }

        /// <summary>
        /// Collects output files and stores them in the execution directory
        /// </summary>
        private async Task<List<string>> CollectAndStoreOutputFilesAsync(string projectDirectory, string executionDirectory, CancellationToken cancellationToken)
        {
            var outputFiles = new List<string>();
            var outputsDirectory = Path.Combine(executionDirectory, "outputs");

            try
            {
                // Look for common output directories
                var outputDirs = new[] { "bin", "dist", "build", "target", "out", "output" };

                foreach (var dir in outputDirs)
                {
                    var outputDir = Path.Combine(projectDirectory, dir);
                    if (Directory.Exists(outputDir))
                    {
                        var files = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);

                        foreach (var file in files)
                        {
                            var relativePath = Path.GetRelativePath(projectDirectory, file);
                            var targetPath = Path.Combine(outputsDirectory, relativePath);
                            var targetDir = Path.GetDirectoryName(targetPath);

                            if (!string.IsNullOrEmpty(targetDir))
                            {
                                Directory.CreateDirectory(targetDir);
                            }

                            File.Copy(file, targetPath, true);
                            outputFiles.Add(Path.GetFullPath(targetPath));
                        }
                    }
                }

                // Also collect any files that were modified during execution
                var allFiles = Directory.GetFiles(projectDirectory, "*", SearchOption.AllDirectories);
                var modifiedFiles = allFiles.Where(f => File.GetLastWriteTime(f) > DateTime.Now.AddMinutes(-5)).ToList();

                foreach (var file in modifiedFiles)
                {
                    var relativePath = Path.GetRelativePath(projectDirectory, file);
                    if (!outputFiles.Contains(relativePath))
                    {
                        var targetPath = Path.Combine(outputsDirectory, relativePath);
                        var targetDir = Path.GetDirectoryName(targetPath);

                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        File.Copy(file, targetPath, true);
                        outputFiles.Add(Path.GetFullPath(targetPath));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect output files from {ProjectDirectory}", projectDirectory);
            }

            return outputFiles;
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
            public ProjectStructureAnalysis? ProjectStructure { get; set; }
            public IProjectLanguageRunner? LanguageRunner { get; set; }
        }

        #endregion
    }

    public class ProjectExecutionSettings
    {
        public string WorkingDirectory { get; set; } = "./storage";
        public int MaxConcurrentExecutions { get; set; } = 5;
        public int DefaultTimeoutMinutes { get; set; } = 30;
        public long MaxProjectSizeBytes { get; set; } = 500 * 1024 * 1024; // 500MB
        public List<string> BlockedFileExtensions { get; set; } = new() { ".exe", ".bat", ".cmd", ".ps1", ".sh", ".scr", ".vbs" };
        public bool EnableSecurityScanning { get; set; } = true;
        public bool CleanupOnCompletion { get; set; } = true;
        public int ExecutionRetentionDays { get; set; } = 30;
    }
}