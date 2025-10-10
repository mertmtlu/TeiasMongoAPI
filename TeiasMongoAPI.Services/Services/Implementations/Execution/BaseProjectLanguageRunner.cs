using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TeiasMongoAPI.Services.DTOs.Request.Execution;
using TeiasMongoAPI.Services.DTOs.Response.Execution;
using TeiasMongoAPI.Services.Interfaces.Execution;
using TeiasMongoAPI.Core.Models.DTOs;
using TeiasMongoAPI.Services.Services.Implementations;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.Services.Implementations.Execution
{
    public abstract class BaseProjectLanguageRunner : IProjectLanguageRunner
    {
        protected readonly ILogger _logger;
        protected readonly IBsonToDtoMappingService _bsonMapper;
        protected readonly IExecutionOutputStreamingService? _streamingService;
        private string? _dockerPath;
        private bool _dockerPathResolved;

        public abstract string Language { get; }
        public virtual int Priority => 100;

        protected BaseProjectLanguageRunner(ILogger logger, IBsonToDtoMappingService bsonMapper, IExecutionOutputStreamingService? streamingService = null)
        {
            _logger = logger;
            _bsonMapper = bsonMapper;
            _streamingService = streamingService;
        }

        /// <summary>
        /// Resolves the Docker executable path, checking multiple locations and configurations.
        /// Supports both Windows and Linux environments, with special handling for WSL2.
        /// </summary>
        protected virtual string GetDockerExecutablePath()
        {
            // Return cached result if already resolved
            if (_dockerPathResolved)
            {
                return _dockerPath ?? "docker";
            }

            _dockerPathResolved = true;

            try
            {
                // 1. Check environment variable override (highest priority)
                var envDockerPath = Environment.GetEnvironmentVariable("DOCKER_PATH");
                if (!string.IsNullOrEmpty(envDockerPath) && IsDockerExecutableValid(envDockerPath))
                {
                    _dockerPath = envDockerPath;
                    _logger.LogInformation("Using Docker from DOCKER_PATH environment variable: {DockerPath}", _dockerPath);
                    return _dockerPath;
                }

                // 2. Try "docker" in PATH (standard approach)
                if (IsDockerExecutableValid("docker"))
                {
                    _dockerPath = "docker";
                    _logger.LogDebug("Using Docker from PATH");
                    return _dockerPath;
                }

                // 3. Platform-specific fallbacks
                var platformPaths = new List<string>();

                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    // Windows paths
                    platformPaths.AddRange(new[]
                    {
                        @"C:\Program Files\Docker\Docker\resources\bin\docker.exe",
                        @"C:\ProgramData\DockerDesktop\version-bin\docker.exe",
                        @"C:\Program Files\Docker\Docker\resources\bin\docker.exe"
                    });
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    // Check if running in WSL2
                    var isWsl = File.Exists("/proc/version") &&
                               File.ReadAllText("/proc/version").Contains("microsoft", StringComparison.OrdinalIgnoreCase);

                    if (isWsl)
                    {
                        // WSL2 paths - try Windows Docker Desktop first, then Linux
                        platformPaths.AddRange(new[]
                        {
                            "/mnt/c/Program Files/Docker/Docker/resources/bin/docker.exe",
                            "/mnt/c/ProgramData/DockerDesktop/version-bin/docker.exe",
                            "/usr/bin/docker",
                            "/usr/local/bin/docker"
                        });
                    }
                    else
                    {
                        // Native Linux
                        platformPaths.AddRange(new[]
                        {
                            "/usr/bin/docker",
                            "/usr/local/bin/docker",
                            "/snap/bin/docker"
                        });
                    }
                }

                // Try each platform-specific path
                foreach (var path in platformPaths)
                {
                    if (IsDockerExecutableValid(path))
                    {
                        _dockerPath = path;
                        _logger.LogInformation("Docker found at non-standard location: {DockerPath}", _dockerPath);
                        return _dockerPath;
                    }
                }

                // 4. No Docker found - log warning and fall back to "docker" (will fail with clear error)
                _logger.LogWarning("Docker executable not found in PATH or common locations. " +
                                 "Docker commands will likely fail. Set DOCKER_PATH environment variable to override.");
                _dockerPath = "docker"; // Will produce "command not found" error with clear message
                return _dockerPath;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while resolving Docker path. Falling back to 'docker' command.");
                _dockerPath = "docker";
                return _dockerPath;
            }
        }

        /// <summary>
        /// Validates if a Docker executable path is valid by attempting to run --version
        /// </summary>
        private bool IsDockerExecutableValid(string dockerPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = dockerPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return false;

                    process.WaitForExit(5000); // 5 second timeout
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if Docker is available and provides detailed diagnostic information if not
        /// </summary>
        protected virtual (bool IsAvailable, string DiagnosticMessage) CheckDockerAvailability()
        {
            var dockerPath = GetDockerExecutablePath();

            if (IsDockerExecutableValid(dockerPath))
            {
                return (true, string.Empty);
            }

            // Build detailed diagnostic message
            var diagnostic = new StringBuilder();
            diagnostic.AppendLine("Docker is not available on this system.");
            diagnostic.AppendLine();
            diagnostic.AppendLine("Diagnostic Information:");
            diagnostic.AppendLine($"  - OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
            diagnostic.AppendLine($"  - Architecture: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
            diagnostic.AppendLine($"  - Attempted Docker path: {dockerPath}");
            diagnostic.AppendLine($"  - Current user: {Environment.UserName}");
            diagnostic.AppendLine($"  - Working directory: {Environment.CurrentDirectory}");
            diagnostic.AppendLine();
            diagnostic.AppendLine("Please ensure:");
            diagnostic.AppendLine("  1. Docker is installed on the system");
            diagnostic.AppendLine("  2. Docker daemon is running");
            diagnostic.AppendLine("  3. Docker executable is in the system PATH or accessible to the application");
            diagnostic.AppendLine("  4. The user running this service has permissions to execute Docker");
            diagnostic.AppendLine();
            diagnostic.AppendLine("Solutions:");
            diagnostic.AppendLine("  - Set DOCKER_PATH environment variable to the full path of docker executable");
            diagnostic.AppendLine("  - For Windows: Ensure Docker Desktop is running");
            diagnostic.AppendLine("  - For WSL2: Enable Docker Desktop WSL2 integration in Docker Desktop settings");
            diagnostic.AppendLine("  - For Linux: Run 'sudo systemctl start docker' or install Docker Engine");
            diagnostic.AppendLine();
            diagnostic.AppendLine("Installation instructions: https://docs.docker.com/get-docker/");

            return (false, diagnostic.ToString());
        }

        /// <summary>
        /// Normalizes paths for Docker, handling Windows to WSL/Unix path conversion
        /// </summary>
        protected virtual string NormalizePathForDocker(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Check if running in WSL
            var isWsl = File.Exists("/proc/version") &&
                       File.ReadAllText("/proc/version").Contains("microsoft", StringComparison.OrdinalIgnoreCase);

            if (!isWsl)
            {
                // Not in WSL - just normalize slashes for Docker on Windows
                return path.Replace("\\", "/");
            }

            // In WSL - need to convert Windows-style paths to WSL paths
            var normalizedPath = path.Replace("\\", "/");

            // Handle relative paths starting with ./ or ./storage
            if (normalizedPath.StartsWith("./") || normalizedPath.StartsWith("../"))
            {
                // Resolve to absolute path first
                var absolutePath = Path.GetFullPath(path);
                normalizedPath = absolutePath.Replace("\\", "/");
            }

            // Convert Windows absolute paths to WSL paths
            // C:/path or C:\path -> /mnt/c/path
            if (normalizedPath.Length >= 2 && normalizedPath[1] == ':')
            {
                var driveLetter = char.ToLower(normalizedPath[0]);
                var pathWithoutDrive = normalizedPath.Substring(2).TrimStart('/');
                var wslPath = $"/mnt/{driveLetter}/{pathWithoutDrive}";

                _logger.LogDebug("Converted Windows path to WSL path: {WindowsPath} -> {WslPath}", path, wslPath);
                return wslPath;
            }

            // Already a WSL/Unix path (/mnt/c/... or /home/...)
            return normalizedPath;
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
            Dictionary<string, string>? environment = null, int timeoutMinutes = 2880, CancellationToken cancellationToken = default,
            string? executionId = null)
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

            // COMPREHENSIVE LOGGING: Process execution start
            LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "Starting", startTime, cancellationToken.IsCancellationRequested);

            using var process = new Process { StartInfo = processInfo };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    //_logger.LogInformation(">>> [PYTHON STDOUT]: {Data}", e.Data);
                    output.AppendLine(e.Data);

                    // LIVE STREAMING: Stream stdout output in real-time
                    if (!string.IsNullOrEmpty(executionId) && _streamingService != null)
                    {
                        try
                        {
                            // Fire and forget - don't wait for streaming to complete
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _streamingService.StreamOutputAsync(executionId, e.Data, DateTime.UtcNow);
                                }
                                catch (Exception streamEx)
                                {
                                    _logger.LogTrace(streamEx, "Failed to stream stdout output for execution {ExecutionId}", executionId);
                                }
                            });
                        }
                        catch
                        {
                            // Ignore streaming errors to prevent breaking process execution
                        }
                    }
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    if (e.Data.ToLower().Contains("error"))
                    {
                        //_logger.LogError(">>> [PYTHON STDERR]: {Data}", e.Data);
                        error.AppendLine(e.Data);

                        // LIVE STREAMING: Stream stderr output in real-time
                        if (!string.IsNullOrEmpty(executionId) && _streamingService != null)
                        {
                            try
                            {
                                // Fire and forget - don't wait for streaming to complete
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await _streamingService.StreamErrorAsync(executionId, e.Data, DateTime.UtcNow);
                                    }
                                    catch (Exception streamEx)
                                    {
                                        _logger.LogTrace(streamEx, "Failed to stream stderr output for execution {ExecutionId}", executionId);
                                    }
                                });
                            }
                            catch
                            {
                                // Ignore streaming errors to prevent breaking process execution
                            }
                        }
                    }
                    else
                    {
                        output.AppendLine(e.Data);

                        // LIVE STREAMING: Stream info output as stdout
                        if (!string.IsNullOrEmpty(executionId) && _streamingService != null)
                        {
                            try
                            {
                                // Fire and forget - don't wait for streaming to complete
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await _streamingService.StreamOutputAsync(executionId, e.Data, DateTime.UtcNow);
                                    }
                                    catch (Exception streamEx)
                                    {
                                        _logger.LogTrace(streamEx, "Failed to stream info output for execution {ExecutionId}", executionId);
                                    }
                                });
                            }
                            catch
                            {
                                // Ignore streaming errors to prevent breaking process execution
                            }
                        }
                    }
                }
            };

            try
            {
                _logger.LogDebug("Running: {Executable} {Arguments} in {WorkingDirectory}", executable, arguments, workingDirectory);

                // Register cancellation callback to ensure process is killed immediately when cancellation is triggered
                cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            _logger.LogWarning("Terminating process {Executable} due to cancellation (timeout or user request)", executable);
                            process.Kill(true); // Kill entire process tree
                        }
                    }
                    catch (Exception killEx)
                    {
                        _logger.LogWarning(killEx, "Failed to kill process {Executable} in cancellation callback", executable);
                    }
                });

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // FIXED: Robust handling for long-running processes
                LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "WaitingForExit", DateTime.UtcNow, cancellationToken.IsCancellationRequested);

                try
                {
                    await process.WaitForExitAsync(cancellationToken);
                    LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "WaitCompleted", DateTime.UtcNow, cancellationToken.IsCancellationRequested, processHasExited: process.HasExited);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    var cancelTime = DateTime.UtcNow;
                    var partialDuration = cancelTime - startTime;

                    LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "Cancelled", cancelTime, true,
                        duration: partialDuration, processHasExited: process.HasExited);

                    // Critical fix: Check if process actually completed despite cancellation
                    if (process.HasExited)
                    {
                        LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "CompletedDespiteCancellation",
                            cancelTime, true, exitCode: process.ExitCode, duration: partialDuration, processHasExited: true);
                        // Process completed naturally - continue to return success/failure based on exit code
                    }
                    else
                    {
                        LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "TerminatingAfterCancellation",
                            cancelTime, true, duration: partialDuration, processHasExited: false);

                        // Process still running - kill it and wait for termination
                        process.Kill(true);
                        await process.WaitForExitAsync(CancellationToken.None);

                        var terminationDuration = DateTime.UtcNow - startTime;
                        LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "TerminatedAfterCancellation",
                            DateTime.UtcNow, true, exitCode: -1, duration: terminationDuration, processHasExited: true);

                        return new ProcessResult
                        {
                            ExitCode = -1,
                            Output = output.ToString(),
                            Error = "Process was cancelled due to timeout or user request",
                            Duration = terminationDuration,
                            Success = false
                        };
                    }
                }

                // Process timeout or other cancellation scenarios
                if (!process.HasExited)
                {
                    LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "UnexpectedNotExited",
                        DateTime.UtcNow, cancellationToken.IsCancellationRequested, processHasExited: false);

                    process.Kill(true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }

                var finalEndTime = DateTime.UtcNow;
                var finalDuration = finalEndTime - startTime;

                var result = new ProcessResult
                {
                    ExitCode = process.ExitCode,
                    Output = output.ToString(),
                    Error = error.ToString(),
                    Duration = finalDuration,
                    Success = process.ExitCode == 0
                };

                LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "Completed",
                    finalEndTime, cancellationToken.IsCancellationRequested, exitCode: result.ExitCode,
                    duration: finalDuration, processHasExited: true);

                return result;
            }
            catch (Exception ex)
            {
                var errorTime = DateTime.UtcNow;
                var errorDuration = errorTime - startTime;

                LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "Failed",
                    errorTime, cancellationToken.IsCancellationRequested, exitCode: -1,
                    duration: errorDuration, processHasExited: process.HasExited);

                _logger.LogError(ex, "Process execution failed: {Executable} {Arguments} after {Duration}",
                    executable, arguments, errorDuration);

                // Ensure process is cleaned up even in case of unexpected errors
                try
                {
                    if (!process.HasExited)
                    {
                        LogProcessExecution(executable, arguments, workingDirectory, timeoutMinutes, "CleanupKillAfterError",
                            DateTime.UtcNow, cancellationToken.IsCancellationRequested, processHasExited: false);

                        process.Kill(true);
                        await process.WaitForExitAsync(CancellationToken.None);
                    }
                }
                catch (Exception killEx)
                {
                    _logger.LogWarning(killEx, "Failed to kill process {Executable} during error cleanup", executable);
                }

                return new ProcessResult
                {
                    ExitCode = -1,
                    Error = ex.Message,
                    Duration = DateTime.UtcNow - startTime,
                    Success = false
                };
            }
        }

        /// <summary>
        /// Executes a process inside a Docker container for security isolation
        /// </summary>
        protected async Task<ProcessResult> RunDockerProcessAsync(
            string dockerImage,
            string command,
            string arguments,
            string projectDirectory,
            string? outputDirectory = null,
            Dictionary<string, string>? environment = null,
            int timeoutMinutes = 2880,
            CancellationToken cancellationToken = default,
            string? executionId = null,
            bool enableNetwork = true,
            int memoryMB = 1024,
            double cpus = 2.0,
            int processLimit = 100,
            int tempStorageMB = 256,
            Dictionary<string, string>? volumes = null,
            string? user = null,
            bool allowChown = false)
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Now executing with WSL2 path normalization.");


            try
            {
                // Normalize paths for Docker (handle Windows to WSL/Unix conversion)
                var normalizedProjectDir = NormalizePathForDocker(projectDirectory);
                var normalizedOutputDir = outputDirectory != null
                    ? NormalizePathForDocker(outputDirectory)
                    : NormalizePathForDocker(Path.Combine(projectDirectory, "output"));

                _logger.LogInformation("Normalized Project Directory for Docker volume mount: {Path}", normalizedProjectDir);

                // Ensure output directory exists
                if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                // Build Docker arguments with security constraints
                var dockerArgs = new StringBuilder();
                dockerArgs.Append("run --rm ");

                // Network configuration
                dockerArgs.Append(enableNetwork ? "--network bridge " : "--network none ");

                // Read-only filesystem with writable temp
                dockerArgs.Append("--read-only ");
                dockerArgs.Append($"--tmpfs /tmp:rw,noexec,size={tempStorageMB}m ");
                dockerArgs.Append("--tmpfs /home/executor:rw,noexec,uid=1000 ");

                // Resource limits
                dockerArgs.Append($"--memory={memoryMB}m ");
                dockerArgs.Append($"--cpus={cpus.ToString(System.Globalization.CultureInfo.InvariantCulture)} ");
                dockerArgs.Append($"--pids-limit={processLimit} ");

                // Security options
                dockerArgs.Append("--security-opt=no-new-privileges ");

                if (allowChown)
                {
                    // For chown operations, we need to keep CAP_CHOWN capability
                    dockerArgs.Append("--cap-drop=ALL --cap-add=CHOWN ");
                }
                else
                {
                    dockerArgs.Append("--cap-drop=ALL ");
                }

                // Set user (defaults to executor, but can be overridden to root for setup tasks)
                dockerArgs.Append($"--user={user ?? "executor"} ");

                // Mount project directory (read-write for build, read-only for execution)
                // During build phase (enableNetwork=true), we need write access for dependency installation
                var volumeMode = enableNetwork ? "rw" : "ro";
                dockerArgs.Append($"-v \"{normalizedProjectDir}:/app:{volumeMode}\" ");

                // Mount output directory as read-write (if specified)
                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    dockerArgs.Append($"-v \"{normalizedOutputDir}:/output:rw\" ");
                }

                // Mount additional volumes (e.g., package cache volumes)
                if (volumes != null)
                {
                    foreach (var volume in volumes)
                    {
                        dockerArgs.Append($"--volume {volume.Key}:{volume.Value} ");
                    }
                }

                // Add environment variables
                if (environment != null)
                {
                    foreach (var env in environment)
                    {
                        // Escape quotes in environment values
                        var escapedValue = env.Value.Replace("\"", "\\\"");
                        dockerArgs.Append($"-e \"{env.Key}={escapedValue}\" ");
                    }
                }

                // Set working directory inside container
                dockerArgs.Append("-w /app ");

                // Specify image and command using shell form to properly handle arguments with spaces
                // Escape any double quotes in the command string
                var fullCommand = $"{command} {arguments}";
                var escapedCommand = fullCommand.Replace("\"", "\\\"");
                dockerArgs.Append($"{dockerImage} /bin/sh -c \"{escapedCommand}\"");

                // Check Docker availability before attempting execution
                var (isAvailable, diagnosticMessage) = CheckDockerAvailability();
                if (!isAvailable)
                {
                    _logger.LogError("Docker is not available. Cannot execute sandboxed project.\n{DiagnosticInfo}", diagnosticMessage);
                    return new ProcessResult
                    {
                        ExitCode = 127,
                        Error = diagnosticMessage,
                        Duration = DateTime.UtcNow - startTime,
                        Success = false
                    };
                }

                var dockerExecutable = GetDockerExecutablePath();
                _logger.LogInformation("Running Docker container: {DockerImage} with command: {Command} {Arguments} using Docker at: {DockerPath}",
                    dockerImage, command, arguments?.Length > 100 ? arguments.Substring(0, 100) + "..." : arguments, dockerExecutable);

                // Execute Docker command using resolved Docker executable path
                var result = await RunProcessAsync(
                    dockerExecutable,
                    dockerArgs.ToString(),
                    projectDirectory,
                    null, // Don't pass environment again (already added to docker args)
                    timeoutMinutes,
                    cancellationToken,
                    executionId);

                // Enhanced Docker-specific error handling
                if (!result.Success)
                {
                    if (result.ExitCode == 127 || result.Error.Contains("command not found") || result.Error.Contains("not recognized"))
                    {
                        var (_, diagMsg) = CheckDockerAvailability();
                        _logger.LogError("Docker command failed with exit code 127 (command not found).\n{DiagnosticInfo}", diagMsg);
                        result.Error = diagMsg;
                    }
                    else if (result.Error.Contains("Cannot connect to the Docker daemon"))
                    {
                        _logger.LogError("Docker daemon is not running. Please start Docker Desktop or Docker daemon.");
                        result.Error += "\n\nDocker daemon is not running. Please start Docker Desktop (Windows/Mac) or 'sudo systemctl start docker' (Linux).";
                    }
                    else if (result.Error.Contains("image") && result.Error.Contains("not found"))
                    {
                        _logger.LogError("Docker image {DockerImage} not found. Please build the Docker images using build-docker-images.sh", dockerImage);
                        result.Error = $"Docker image '{dockerImage}' not found. Please build the required Docker images using:\n" +
                                     $"  ./build-docker-images.sh (Linux/Mac)\n" +
                                     $"  or manually: docker build -t {dockerImage} -f Dockerfiles/<language>.Dockerfile .";
                    }
                    else if (result.Error.Contains("permission denied") || result.Error.Contains("access denied"))
                    {
                        _logger.LogError("Docker permission denied. User may not have access to Docker daemon.");
                        result.Error += "\n\nPermission denied. On Linux, add user to docker group: sudo usermod -aG docker $USER";
                    }
                    else
                    {
                        _logger.LogError("Unclassified error: {error}", result.Error);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Docker process execution failed for image {DockerImage}", dockerImage);
                return new ProcessResult
                {
                    ExitCode = -1,
                    Error = $"Docker execution failed: {ex.Message}",
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

        /// <summary>
        /// FIXED: Safe parameter processing with proper BSON to JSON conversion
        /// </summary>
        protected async Task ProcessInputFilesFromParametersAsync(ProjectExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                // CRITICAL FIX: Use DTO mapping service instead of direct JsonSerializer
                var contextDto = _bsonMapper.MapToExecutionContextDto(
                    context.Parameters,
                    context.ExecutionId,
                    context.ProjectDirectory);

                if (contextDto.InputFiles != null && contextDto.InputFiles.Any())
                {
                    // Create input directory in project
                    var inputDir = Path.Combine(context.ProjectDirectory, "input");
                    Directory.CreateDirectory(inputDir);

                    foreach (var inputFile in contextDto.InputFiles)
                    {
                        if (!string.IsNullOrEmpty(inputFile.Content))
                        {
                            var filePath = Path.Combine(inputDir, inputFile.Name);
                            var content = Convert.FromBase64String(inputFile.Content);
                            await File.WriteAllBytesAsync(filePath, content, cancellationToken);

                            _logger.LogDebug("Created input file: {FilePath} ({Size} bytes)",
                                filePath, content.Length);
                        }
                    }

                    _logger.LogInformation("Processed {FileCount} input files for execution {ExecutionId}",
                        contextDto.InputFiles.Count, context.ExecutionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process input files from parameters for execution {ExecutionId}", context.ExecutionId);
                // Don't fail the execution, just log the warning
            }
        }

        /// <summary>
        /// FIXED: Safe parameter processing for external scripts
        /// </summary>
        protected object ProcessParametersForExecution(object parameters, string projectDirectory)
        {
            try
            {
                // CRITICAL FIX: Convert to JSON-safe DTO before serialization
                var contextDto = _bsonMapper.MapToExecutionContextDto(parameters, Guid.NewGuid().ToString(), projectDirectory);
                var parametersDict = contextDto.Parameters;

                if (parametersDict == null)
                {
                    return parameters;
                }

                // Process input files using DTO service
                if (contextDto.InputFiles != null && contextDto.InputFiles.Any())
                {
                    var filePaths = contextDto.InputFiles.Select(f => $"./input/{f.Name}").ToList();
                    var fileNames = contextDto.InputFiles.Select(f => f.Name).ToList();

                    parametersDict["inputFiles"] = filePaths;
                    parametersDict["inputFileNames"] = fileNames;
                    parametersDict["inputDirectory"] = "./input";
                    parametersDict["hasInputFiles"] = true;
                }
                else
                {
                    parametersDict["hasInputFiles"] = false;
                }

                // Process table elements - flatten nested table data
                var processedParameters = new Dictionary<string, object>();

                foreach (var kvp in parametersDict)
                {
                    if (kvp.Key.StartsWith("file_input")) continue;
                    if (kvp.Value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                    {
                        // Check if this might be a table element (nested object structure)
                        var nestedDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());

                        if (nestedDict != null && IsTableElement(nestedDict))
                        {
                            // This is a table element - flatten it
                            foreach (var cellKvp in nestedDict)
                            {
                                if (cellKvp.Key == "content") continue;
                                var cellKey = $"{kvp.Key}_{cellKvp.Key}";
                                processedParameters[cellKey] = cellKvp.Value;
                            }

                            // Also keep the original table structure for backward compatibility
                            processedParameters[kvp.Key] = nestedDict;
                        }
                        else
                        {
                            // Regular nested object, keep as is
                            processedParameters[kvp.Key] = nestedDict;
                        }
                    }
                    else
                    {
                        // Regular parameter, keep as is
                        processedParameters[kvp.Key] = kvp.Value;
                    }
                }

                return processedParameters;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process parameters for execution");
                return parameters; // Return original if processing fails
            }
        }

        private bool IsTableElement(Dictionary<string, object> dict)
        {
            // Heuristic to determine if this is a table element:
            // 1. All keys should be cell IDs (like "a1", "b1") or custom names
            // 2. Values should be simple types (strings, numbers, booleans)
            // 3. Should have multiple entries (tables typically have multiple cells)

            if (dict.Count == 0)
                return false;

            // Check if all values are simple types (not nested objects)
            foreach (var kvp in dict)
            {
                if (kvp.Value is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.Object || jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        return false; // Complex nested structure, probably not a table
                    }
                }
                else if (kvp.Value is Dictionary<string, object> || kvp.Value is List<object>)
                {
                    return false; // Complex nested structure, probably not a table
                }
            }

            // Additional heuristics could be added here:
            // - Check for cell ID patterns (a1, b1, etc.)
            // - Check for common table-like key patterns
            // - Check against known table element names from component configuration

            return true; // Likely a table element
        }

        /// <summary>
        /// COMPREHENSIVE LOGGING: Detailed logging for long-running execution debugging
        /// </summary>
        private void LogProcessExecution(string executable, string arguments, string workingDirectory,
            int timeoutMinutes, string phase, DateTime timestamp, bool cancellationRequested,
            int? exitCode = null, TimeSpan? duration = null, bool? processHasExited = null)
        {
            var logLevel = phase switch
            {
                "Starting" => LogLevel.Information,
                "WaitingForExit" => LogLevel.Debug,
                "Completed" => LogLevel.Information,
                "Cancelled" => LogLevel.Warning,
                "Failed" => LogLevel.Error,
                "Timeout" => LogLevel.Warning,
                _ => LogLevel.Debug
            };

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["ProcessExecutable"] = executable,
                ["ProcessArguments"] = arguments.Length > 100 ? arguments[..100] + "..." : arguments,
                ["WorkingDirectory"] = workingDirectory,
                ["TimeoutMinutes"] = timeoutMinutes,
                ["Phase"] = phase,
                ["Timestamp"] = timestamp.ToString("O"),
                ["CancellationRequested"] = cancellationRequested
            });

            var message = $"Process execution {phase}: {executable} " +
                         $"(timeout: {timeoutMinutes}min, cancellation: {cancellationRequested}";

            if (exitCode.HasValue)
                message += $", exit code: {exitCode.Value}";

            if (duration.HasValue)
                message += $", duration: {duration.Value.TotalMinutes:F2}min";

            if (processHasExited.HasValue)
                message += $", has exited: {processHasExited.Value}";

            message += ")";

            _logger.Log(logLevel, message);

            // Additional detailed logging for critical phases
            if (phase == "Cancelled" || phase == "Timeout" || (phase == "Completed" && duration?.TotalHours >= 1))
            {
                _logger.LogInformation("LONG_RUNNING_EXECUTION_DETAILS: Process {Executable} {Phase} after {Duration} " +
                                     "with cancellation_requested={CancellationRequested}, process_exited={ProcessExited}, exit_code={ExitCode}",
                    executable, phase, duration?.ToString() ?? "unknown", cancellationRequested, processHasExited, exitCode);
            }
        }
        protected class InputFileData
        {
            public string Name { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty; // Base64 encoded
            public string ContentType { get; set; } = string.Empty;
            public long Size { get; set; }
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