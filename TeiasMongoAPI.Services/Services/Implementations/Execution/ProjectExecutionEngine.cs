// TeiasMongoAPI.Services/Services/Implementations/Execution/ProjectExecutionEngine.cs
using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Text.Json;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
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
        private readonly IEnumerable<IProjectLanguageRunner> _languageRunners;
        private readonly ProjectExecutionSettings _settings;
        private readonly Dictionary<string, ExecutionSession> _activeSessions = new();

        public ProjectExecutionEngine(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IFileStorageService fileStorageService,
            IEnumerable<IProjectLanguageRunner> languageRunners,
            IOptions<ProjectExecutionSettings> settings,
            ILogger<ProjectExecutionEngine> logger)
            : base(unitOfWork, mapper, logger)
        {
            _fileStorageService = fileStorageService;
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

                // Step 1: Extract project files
                var projectDirectory = await ExtractProjectFilesAsync(request, session, timeoutCts.Token);
                session.ProjectDirectory = projectDirectory;

                // Step 2: Analyze project structure
                var projectStructure = await AnalyzeProjectStructureAsync(projectDirectory, timeoutCts.Token);
                session.ProjectStructure = projectStructure;

                // Step 3: Validate project
                var validation = await ValidateExtractedProjectAsync(projectDirectory, timeoutCts.Token);
                if (!validation.IsValid)
                {
                    return CreateFailureResult(executionId, session, $"Project validation failed: {string.Join(", ", validation.Errors)}");
                }

                // Step 4: Find appropriate language runner
                var runner = await FindLanguageRunnerAsync(projectDirectory, projectStructure, timeoutCts.Token);
                if (runner == null)
                {
                    return CreateFailureResult(executionId, session, $"No suitable language runner found for project type: {projectStructure.Language}");
                }

                session.LanguageRunner = runner;

                // Step 5: Build project if needed
                ProjectBuildResult? buildResult = null;
                if (projectStructure.HasBuildFile && !(request.BuildArgs?.SkipBuild ?? false))
                {
                    buildResult = await BuildProjectAsync(runner, projectDirectory, request.BuildArgs ?? new(), timeoutCts.Token);
                    if (!buildResult.Success)
                    {
                        return CreateFailureResult(executionId, session, $"Build failed: {buildResult.ErrorMessage}", buildResult);
                    }
                }

                // Step 6: Execute project
                var executionContext = CreateExecutionContext(executionId, projectDirectory, request, projectStructure, timeoutCts.Token);
                var result = await runner.ExecuteAsync(executionContext, timeoutCts.Token);

                // Step 7: Collect output files
                if (request.SaveResults)
                {
                    result.OutputFiles = await CollectOutputFilesAsync(projectDirectory, timeoutCts.Token);
                }

                // Step 8: Update result with session info
                result.ExecutionId = executionId;
                result.StartedAt = session.StartedAt;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;
                result.BuildResult = buildResult;

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
                // Cleanup
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
                // Extract files to temporary directory for validation
                var tempDir = Path.Combine(_settings.WorkingDirectory, "validation", Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                try
                {
                    await ExtractFilesToDirectoryAsync(programId, versionId, tempDir, cancellationToken);
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
            var tempDir = Path.Combine(_settings.WorkingDirectory, "analysis", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                await ExtractFilesToDirectoryAsync(programId, versionId, tempDir, cancellationToken);
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

        private async Task<string> ExtractProjectFilesAsync(ProjectExecutionRequest request, ExecutionSession session, CancellationToken cancellationToken)
        {
            var projectDir = Path.Combine(_settings.WorkingDirectory, session.ExecutionId);
            Directory.CreateDirectory(projectDir);

            await ExtractFilesToDirectoryAsync(request.ProgramId, request.VersionId, projectDir, cancellationToken);

            _logger.LogDebug("Extracted project files to {ProjectDirectory}", projectDir);
            return projectDir;
        }

        private async Task ExtractFilesToDirectoryAsync(string programId, string? versionId, string targetDirectory, CancellationToken cancellationToken)
        {
            var files = await _fileStorageService.ListProgramFilesAsync(programId, versionId, cancellationToken);

            foreach (var file in files)
            {
                var content = await _fileStorageService.GetFileContentAsync(file.StorageKey, cancellationToken);
                var targetPath = Path.Combine(targetDirectory, file.FilePath.TrimStart('/'));
                var targetDir = Path.GetDirectoryName(targetPath);

                if (!string.IsNullOrEmpty(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                await File.WriteAllBytesAsync(targetPath, content, cancellationToken);
            }
        }

        private async Task<ProjectStructureAnalysis> AnalyzeProjectStructureAsync(string projectDirectory, CancellationToken cancellationToken)
        {
            var analysis = new ProjectStructureAnalysis();

            // Get all files
            var allFiles = Directory.GetFiles(projectDirectory, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(projectDirectory, f))
                .ToList();

            analysis.Files = allFiles.Select(f => new ProjectFile
            {
                Path = f,
                Extension = Path.GetExtension(f),
                Size = new FileInfo(Path.Combine(projectDirectory, f)).Length,
                Type = DetermineFileType(f)
            }).ToList();

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

            return analysis;
        }

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

                // Security scan
                result.SecurityScan = await PerformSecurityScanAsync(projectDirectory, cancellationToken);
                if (result.SecurityScan.HasSecurityIssues)
                {
                    result.Warnings.AddRange(result.SecurityScan.Issues.Select(i => $"Security: {i.Description}"));
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

        private async Task<List<string>> CollectOutputFilesAsync(string projectDirectory, CancellationToken cancellationToken)
        {
            var outputFiles = new List<string>();

            try
            {
                // Look for common output directories
                var outputDirs = new[] { "bin", "dist", "build", "target", "out" };

                foreach (var dir in outputDirs)
                {
                    var outputDir = Path.Combine(projectDirectory, dir);
                    if (Directory.Exists(outputDir))
                    {
                        var files = Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories);
                        outputFiles.AddRange(files.Select(f => Path.GetRelativePath(projectDirectory, f)));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect output files from {ProjectDirectory}", projectDirectory);
            }

            return outputFiles;
        }

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
                    "exec("
                };

                var files = Directory.GetFiles(projectDirectory, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (new[] { ".cs", ".py", ".java", ".js", ".ts" }.Contains(extension))
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
                                scan.SuspiciousPatterns.Add(pattern);
                            }
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

        private ProjectComplexity CalculateProjectComplexity(ProjectStructureAnalysis analysis)
        {
            var complexity = new ProjectComplexity
            {
                TotalFiles = analysis.Files.Count,
                Dependencies = analysis.Dependencies.Count,
                TotalLines = analysis.Files.Sum(f => EstimateLineCount(f.Size))
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
                BuildResult = buildResult
            };
        }

        private async Task CleanupSessionAsync(ExecutionSession session)
        {
            try
            {
                if (!string.IsNullOrEmpty(session.ProjectDirectory) && Directory.Exists(session.ProjectDirectory))
                {
                    Directory.Delete(session.ProjectDirectory, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup session directory {ProjectDirectory}", session.ProjectDirectory);
            }
        }

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
                ".csproj" => "C# Project",
                ".sln" => "Visual Studio Solution",
                ".pom" => "Maven Project",
                ".gradle" => "Gradle Build",
                ".txt" => "Text",
                ".md" => "Markdown",
                _ => "Other"
            };
        }

        private int EstimateLineCount(long fileSize)
        {
            // Rough estimate: 50 characters per line
            return (int)(fileSize / 50);
        }

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
            public string ProjectDirectory { get; set; } = string.Empty;
            public ProjectStructureAnalysis? ProjectStructure { get; set; }
            public IProjectLanguageRunner? LanguageRunner { get; set; }
        }

        #endregion
    }

    public class ProjectExecutionSettings
    {
        public string WorkingDirectory { get; set; } = "./executions";
        public int MaxConcurrentExecutions { get; set; } = 5;
        public int DefaultTimeoutMinutes { get; set; } = 30;
        public long MaxProjectSizeBytes { get; set; } = 500 * 1024 * 1024; // 500MB
        public List<string> BlockedFileExtensions { get; set; } = new() { ".exe", ".bat", ".cmd", ".ps1", ".sh" };
        public bool EnableSecurityScanning { get; set; } = true;
        public bool CleanupOnCompletion { get; set; } = true;
    }
}