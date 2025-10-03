using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Configuration;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class DemoShowcaseService : IDemoShowcaseService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DemoShowcaseService> _logger;
        private readonly IExecutionService _executionService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IOptions<DemoShowcaseSettings> _demoShowcaseSettings;

        public DemoShowcaseService(
            IUnitOfWork unitOfWork,
            ILogger<DemoShowcaseService> logger,
            IExecutionService executionService,
            IFileStorageService fileStorageService,
            IOptions<DemoShowcaseSettings> demoShowcaseSettings)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _executionService = executionService;
            _fileStorageService = fileStorageService;
            _demoShowcaseSettings = demoShowcaseSettings;
        }

        public async Task<List<DemoShowcasePublicDto>> GetAllPublicAsync(CancellationToken cancellationToken = default)
        {
            // Fetch all DemoShowcase items
            var showcases = await _unitOfWork.DemoShowcases.GetAllAsync(cancellationToken);
            var showcaseList = showcases.ToList();

            if (!showcaseList.Any())
                return new List<DemoShowcasePublicDto>();

            // Group by AppType and collect IDs
            var programIds = showcaseList
                .Where(x => x.AppType == AppType.Program)
                .Select(x => ObjectId.Parse(x.AssociatedAppId))
                .ToList();

            var workflowIds = showcaseList
                .Where(x => x.AppType == AppType.Workflow)
                .Select(x => ObjectId.Parse(x.AssociatedAppId))
                .ToList();

            var remoteAppIds = showcaseList
                .Where(x => x.AppType == AppType.RemoteApp)
                .Select(x => ObjectId.Parse(x.AssociatedAppId))
                .ToList();

            // Fetch apps in parallel
            var programsTask = GetProgramsByIdsAsync(programIds, cancellationToken);
            var workflowsTask = GetWorkflowsByIdsAsync(workflowIds, cancellationToken);
            var remoteAppsTask = GetRemoteAppsByIdsAsync(remoteAppIds, cancellationToken);

            await Task.WhenAll(programsTask, workflowsTask, remoteAppsTask);

            var programs = await programsTask;
            var workflows = await workflowsTask;
            var remoteApps = await remoteAppsTask;

            // Create lookup dictionaries
            var programDict = programs.ToDictionary(x => x._ID.ToString());
            var workflowDict = workflows.ToDictionary(x => x._ID.ToString());
            var remoteAppDict = remoteApps.ToDictionary(x => x._ID.ToString());

            // Map to DTOs
            var result = new List<DemoShowcasePublicDto>();

            foreach (var showcase in showcaseList)
            {
                DemoShowcasePublicDto? dto = showcase.AppType switch
                {
                    AppType.Program when programDict.TryGetValue(showcase.AssociatedAppId, out var program) =>
                        new DemoShowcasePublicDto
                        {
                            Id = showcase._ID.ToString(),
                            Group = $"{showcase.Tab} > {showcase.PrimaryGroup} > {showcase.SecondaryGroup}",
                            VideoPath = showcase.VideoPath,
                            AppType = "Program",
                            AppId = program._ID.ToString(),
                            Name = program.Name,
                            Description = program.Description,
                            Creator = program.CreatorId,
                            CreatedAt = program.CreatedAt
                        },
                    AppType.Workflow when workflowDict.TryGetValue(showcase.AssociatedAppId, out var workflow) =>
                        new DemoShowcasePublicDto
                        {
                            Id = showcase._ID.ToString(),
                            Group = $"{showcase.Tab} > {showcase.PrimaryGroup} > {showcase.SecondaryGroup}",
                            VideoPath = showcase.VideoPath,
                            AppType = "Workflow",
                            AppId = workflow._ID.ToString(),
                            Name = workflow.Name,
                            Description = workflow.Description,
                            Creator = workflow.Creator,
                            CreatedAt = workflow.CreatedAt
                        },
                    AppType.RemoteApp when remoteAppDict.TryGetValue(showcase.AssociatedAppId, out var remoteApp) =>
                        new DemoShowcasePublicDto
                        {
                            Id = showcase._ID.ToString(),
                            Group = $"{showcase.Tab} > {showcase.PrimaryGroup} > {showcase.SecondaryGroup}",
                            VideoPath = showcase.VideoPath,
                            AppType = "RemoteApp",
                            AppId = remoteApp._ID.ToString(),
                            Name = remoteApp.Name,
                            Description = remoteApp.Description,
                            Creator = remoteApp.Creator,
                            CreatedAt = remoteApp.CreatedAt
                        },
                    _ => null
                };

                if (dto != null)
                    result.Add(dto);
            }

            return result;
        }

        public async Task<List<DemoShowcaseDto>> GetAllAdminAsync(CancellationToken cancellationToken = default)
        {
            var showcases = await _unitOfWork.DemoShowcases.GetAllAsync(cancellationToken);

            return showcases.Select(s => new DemoShowcaseDto
            {
                Id = s._ID.ToString(),
                AssociatedAppId = s.AssociatedAppId,
                AppType = s.AppType.ToString(),
                Tab = s.Tab,
                PrimaryGroup = s.PrimaryGroup,
                SecondaryGroup = s.SecondaryGroup,
                VideoPath = s.VideoPath
            }).ToList();
        }

        public async Task<DemoShowcaseDto> CreateAsync(DemoShowcaseCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate AssociatedAppId format
            if (!ObjectId.TryParse(dto.AssociatedAppId, out var appObjectId))
                throw new ArgumentException("Invalid app ID format", nameof(dto.AssociatedAppId));

            // Verify the app exists
            var appExists = dto.AppType switch
            {
                AppType.Program => await _unitOfWork.Programs.ExistsAsync(appObjectId, cancellationToken),
                AppType.Workflow => await _unitOfWork.Workflows.ExistsAsync(appObjectId, cancellationToken),
                AppType.RemoteApp => await _unitOfWork.RemoteApps.ExistsAsync(appObjectId, cancellationToken),
                _ => false
            };

            if (!appExists)
                throw new KeyNotFoundException($"Associated {dto.AppType} with ID {dto.AssociatedAppId} not found");

            var showcase = new DemoShowcase
            {
                AssociatedAppId = dto.AssociatedAppId,
                AppType = dto.AppType,
                Tab = dto.Tab,
                PrimaryGroup = dto.PrimaryGroup,
                SecondaryGroup = dto.SecondaryGroup,
                VideoPath = dto.VideoPath
            };

            var created = await _unitOfWork.DemoShowcases.CreateAsync(showcase, cancellationToken);

            return new DemoShowcaseDto
            {
                Id = created._ID.ToString(),
                AssociatedAppId = created.AssociatedAppId,
                AppType = created.AppType.ToString(),
                Tab = created.Tab,
                PrimaryGroup = created.PrimaryGroup,
                SecondaryGroup = created.SecondaryGroup,
                VideoPath = created.VideoPath
            };
        }

        public async Task<DemoShowcaseDto> UpdateAsync(string id, DemoShowcaseUpdateDto dto, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            var showcase = await _unitOfWork.DemoShowcases.GetByIdAsync(objectId, cancellationToken);
            if (showcase == null)
                throw new KeyNotFoundException($"Demo showcase with ID {id} not found");

            // Update fields if provided
            if (dto.AssociatedAppId != null)
            {
                if (!ObjectId.TryParse(dto.AssociatedAppId, out var appObjectId))
                    throw new ArgumentException("Invalid app ID format", nameof(dto.AssociatedAppId));

                // Use existing AppType or the new one if provided
                var appType = dto.AppType ?? showcase.AppType;
                var appExists = appType switch
                {
                    AppType.Program => await _unitOfWork.Programs.ExistsAsync(appObjectId, cancellationToken),
                    AppType.Workflow => await _unitOfWork.Workflows.ExistsAsync(appObjectId, cancellationToken),
                    AppType.RemoteApp => await _unitOfWork.RemoteApps.ExistsAsync(appObjectId, cancellationToken),
                    _ => false
                };

                if (!appExists)
                    throw new KeyNotFoundException($"Associated {appType} with ID {dto.AssociatedAppId} not found");

                showcase.AssociatedAppId = dto.AssociatedAppId;
            }

            if (dto.AppType.HasValue)
                showcase.AppType = dto.AppType.Value;

            if (dto.Tab != null)
                showcase.Tab = dto.Tab;

            if (dto.PrimaryGroup != null)
                showcase.PrimaryGroup = dto.PrimaryGroup;

            if (dto.SecondaryGroup != null)
                showcase.SecondaryGroup = dto.SecondaryGroup;

            if (dto.VideoPath != null)
                showcase.VideoPath = dto.VideoPath;

            await _unitOfWork.DemoShowcases.UpdateAsync(objectId, showcase, cancellationToken);

            return new DemoShowcaseDto
            {
                Id = showcase._ID.ToString(),
                AssociatedAppId = showcase.AssociatedAppId,
                AppType = showcase.AppType.ToString(),
                Tab = showcase.Tab,
                PrimaryGroup = showcase.PrimaryGroup,
                SecondaryGroup = showcase.SecondaryGroup,
                VideoPath = showcase.VideoPath
            };
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format", nameof(id));

            var showcase = await _unitOfWork.DemoShowcases.GetByIdAsync(objectId, cancellationToken);
            if (showcase == null)
                throw new KeyNotFoundException($"Demo showcase with ID {id} not found");

            // Optionally delete the video file from disk
            try
            {
                var videoPath = Path.Combine("wwwroot", showcase.VideoPath.TrimStart('/'));
                if (File.Exists(videoPath))
                {
                    File.Delete(videoPath);
                    _logger.LogInformation("Deleted video file: {VideoPath}", videoPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete video file for showcase {Id}", id);
                // Don't fail the delete operation if file deletion fails
            }

            return await _unitOfWork.DemoShowcases.DeleteAsync(objectId, cancellationToken);
        }

        public async Task<VideoUploadResponseDto> UploadVideoAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            // Validate file extension
            var allowedExtensions = new[] { ".mp4", ".webm", ".avi", ".mov" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException($"Invalid file type. Allowed: {string.Join(", ", allowedExtensions)}");
            }

            // Validate file size (500 MB max)
            const long maxFileSize = 524_288_000; // 500 MB
            if (file.Length > maxFileSize)
            {
                throw new InvalidOperationException($"File size exceeds maximum allowed size of {maxFileSize / (1024 * 1024)} MB");
            }

            // Generate unique filename
            var fileName = $"{Guid.NewGuid()}{extension}";

            // Ensure upload directory exists
            var uploadPath = Path.Combine("wwwroot", "videos");
            Directory.CreateDirectory(uploadPath);

            // Save file to disk
            var fullPath = Path.Combine(uploadPath, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            _logger.LogInformation("Uploaded video file: {FileName} ({Size} bytes)", fileName, file.Length);

            // Return relative path (for web access)
            return new VideoUploadResponseDto
            {
                VideoPath = $"/videos/{fileName}",
                FileSize = file.Length
            };
        }

        public async Task<AvailableAppsDto> GetAvailableAppsAsync(CancellationToken cancellationToken = default)
        {
            var programsTask = _unitOfWork.Programs.GetAllAsync(cancellationToken);
            var workflowsTask = _unitOfWork.Workflows.GetAllAsync(cancellationToken);
            var remoteAppsTask = _unitOfWork.RemoteApps.GetAllAsync(cancellationToken);

            await Task.WhenAll(programsTask, workflowsTask, remoteAppsTask);

            var programs = await programsTask;
            var workflows = await workflowsTask;
            var remoteApps = await remoteAppsTask;

            return new AvailableAppsDto
            {
                Programs = programs.Select(p => new AppOptionDto
                {
                    Id = p._ID.ToString(),
                    Name = p.Name
                }).ToList(),
                Workflows = workflows.Select(w => new AppOptionDto
                {
                    Id = w._ID.ToString(),
                    Name = w.Name
                }).ToList(),
                RemoteApps = remoteApps.Select(r => new AppOptionDto
                {
                    Id = r._ID.ToString(),
                    Name = r.Name
                }).ToList()
            };
        }

        // New Public API Methods
        public async Task<PublicDemoShowcaseResponse> GetPublicDemoShowcaseAsync(CancellationToken cancellationToken = default)
        {
            // Fetch all active DemoShowcase items
            var showcases = await _unitOfWork.DemoShowcases.GetAllAsync(cancellationToken);
            var showcaseList = showcases.ToList();

            if (!showcaseList.Any())
                return new PublicDemoShowcaseResponse();

            // Group by AppType and collect IDs
            var programIds = showcaseList
                .Where(x => x.AppType == AppType.Program)
                .Select(x => ObjectId.Parse(x.AssociatedAppId))
                .Distinct()
                .ToList();

            var workflowIds = showcaseList
                .Where(x => x.AppType == AppType.Workflow)
                .Select(x => ObjectId.Parse(x.AssociatedAppId))
                .Distinct()
                .ToList();

            var remoteAppIds = showcaseList
                .Where(x => x.AppType == AppType.RemoteApp)
                .Select(x => ObjectId.Parse(x.AssociatedAppId))
                .Distinct()
                .ToList();

            // Fetch apps in parallel
            var programsTask = GetProgramsByIdsAsync(programIds, cancellationToken);
            var workflowsTask = GetWorkflowsByIdsAsync(workflowIds, cancellationToken);
            var remoteAppsTask = GetRemoteAppsByIdsAsync(remoteAppIds, cancellationToken);

            await Task.WhenAll(programsTask, workflowsTask, remoteAppsTask);

            var programs = await programsTask;
            var workflows = await workflowsTask;
            var remoteApps = await remoteAppsTask;

            // Extract unique creator IDs
            var creatorIds = new HashSet<ObjectId>();
            foreach (var p in programs)
                if (ObjectId.TryParse(p.CreatorId, out var cid)) creatorIds.Add(cid);
            foreach (var w in workflows)
                if (ObjectId.TryParse(w.Creator, out var cid)) creatorIds.Add(cid);
            foreach (var r in remoteApps)
                if (ObjectId.TryParse(r.Creator, out var cid)) creatorIds.Add(cid);

            // Fetch all users
            var users = await _unitOfWork.Users.FindAsync(u => creatorIds.Contains(u._ID), cancellationToken);
            var userDict = users.ToDictionary(u => u._ID.ToString(), u => $"{u.FirstName} {u.LastName}");

            // Check for UI Components (any UI component for the program)
            var uiComponents = await _unitOfWork.UiComponents.GetAllAsync(cancellationToken);
            var uiComponentDict = uiComponents
                .GroupBy(ui => ui.ProgramId.ToString())
                .ToDictionary(g => g.Key, g => true);

            // Create lookup dictionaries for apps
            var programDict = programs.ToDictionary(x => x._ID.ToString());
            var workflowDict = workflows.ToDictionary(x => x._ID.ToString());
            var remoteAppDict = remoteApps.ToDictionary(x => x._ID.ToString());

            // Build showcase items with enriched data
            var items = new List<(DemoShowcase showcase, DemoShowcaseItemDto item)>();

            foreach (var showcase in showcaseList)
            {
                DemoShowcaseItemDto? item = showcase.AppType switch
                {
                    AppType.Program when programDict.TryGetValue(showcase.AssociatedAppId, out var program) =>
                        new DemoShowcaseItemDto
                        {
                            Id = showcase._ID.ToString(),
                            Name = program.Name,
                            Description = program.Description,
                            IconUrl = null,
                            AppId = program._ID.ToString(),
                            AppType = "Program",
                            VideoPath = showcase.VideoPath,
                            CreatedAt = program.CreatedAt,
                            CreatorFullName = userDict.TryGetValue(program.CreatorId, out var pCreator) ? pCreator : "Unknown",
                            HasPublicUiComponent = uiComponentDict.ContainsKey(program._ID.ToString())
                        },
                    AppType.Workflow when workflowDict.TryGetValue(showcase.AssociatedAppId, out var workflow) =>
                        new DemoShowcaseItemDto
                        {
                            Id = showcase._ID.ToString(),
                            Name = workflow.Name,
                            Description = workflow.Description,
                            IconUrl = null,
                            AppId = workflow._ID.ToString(),
                            AppType = "Workflow",
                            VideoPath = showcase.VideoPath,
                            CreatedAt = workflow.CreatedAt,
                            CreatorFullName = userDict.TryGetValue(workflow.Creator, out var wCreator) ? wCreator : "Unknown",
                            HasPublicUiComponent = false // Workflows don't have UI components
                        },
                    AppType.RemoteApp when remoteAppDict.TryGetValue(showcase.AssociatedAppId, out var remoteApp) =>
                        new DemoShowcaseItemDto
                        {
                            Id = showcase._ID.ToString(),
                            Name = remoteApp.Name,
                            Description = remoteApp.Description,
                            IconUrl = null,
                            AppId = remoteApp._ID.ToString(),
                            AppType = "RemoteApp",
                            VideoPath = showcase.VideoPath,
                            CreatedAt = remoteApp.CreatedAt,
                            CreatorFullName = userDict.TryGetValue(remoteApp.Creator, out var rCreator) ? rCreator : "Unknown",
                            HasPublicUiComponent = false // RemoteApps don't have UI components
                        },
                    _ => null
                };

                if (item != null)
                    items.Add((showcase, item));
            }

            // Group by Tab -> PrimaryGroup -> SecondaryGroup
            var response = new PublicDemoShowcaseResponse
            {
                Tabs = items
                    .GroupBy(x => x.showcase.Tab)
                    .Select(tabGroup => new TabGroupDto
                    {
                        TabName = tabGroup.Key,
                        PrimaryGroups = tabGroup
                            .GroupBy(x => x.showcase.PrimaryGroup)
                            .Select(primaryGroup => new PrimaryGroupDto
                            {
                                PrimaryGroupName = primaryGroup.Key,
                                SecondaryGroups = primaryGroup
                                    .GroupBy(x => x.showcase.SecondaryGroup)
                                    .Select(secondaryGroup => new SecondaryGroupDto
                                    {
                                        SecondaryGroupName = secondaryGroup.Key,
                                        Items = secondaryGroup.Select(x => x.item).ToList()
                                    })
                                    .ToList()
                            })
                            .ToList()
                    })
                    .ToList()
            };

            return response;
        }

        public async Task<UiComponentResponseDto> GetPublicUiComponentAsync(string appId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(appId, out var programId))
                throw new ArgumentException("Invalid app ID format", nameof(appId));

            // Fetch the program
            var program = await _unitOfWork.Programs.GetByIdAsync(programId, cancellationToken);
            if (program == null)
                throw new KeyNotFoundException($"Program with ID {appId} not found");

            // Verify IsPublic
            if (!program.IsPublic)
                throw new UnauthorizedAccessException("This application is not publicly accessible");

            // Fetch UI Component
            var uiComponents = await _unitOfWork.UiComponents.FindAsync(
                ui => ui.ProgramId == programId,
                cancellationToken);

            var uiComponent = uiComponents.FirstOrDefault();
            if (uiComponent == null)
                throw new KeyNotFoundException($"No public UI Component found for program {appId}");

            return new UiComponentResponseDto
            {
                Id = uiComponent._ID.ToString(),
                ProgramId = uiComponent.ProgramId.ToString(),
                Schema = uiComponent.Schema,
                Configuration = uiComponent.Configuration
            };
        }

        public async Task<ExecutionResponseDto> ExecutePublicAppAsync(string appId, ExecutionRequestDto request, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(appId, out var programId))
                throw new ArgumentException("Invalid app ID format", nameof(appId));

            // Fetch the program
            var program = await _unitOfWork.Programs.GetByIdAsync(programId, cancellationToken);
            if (program == null)
                throw new KeyNotFoundException($"Program with ID {appId} not found");

            // Verify IsPublic
            if (!program.IsPublic)
                throw new UnauthorizedAccessException("This application is not publicly accessible");

            // Get public system user ID from settings
            var publicSystemUserId = _demoShowcaseSettings.Value.PublicSystemUserId;
            if (string.IsNullOrEmpty(publicSystemUserId))
                throw new InvalidOperationException("Public system user ID is not configured");

            if (!ObjectId.TryParse(publicSystemUserId, out var systemUserObjectId))
                throw new InvalidOperationException("Invalid public system user ID format in configuration");

            // Create execution request
            var executionRequest = new ProgramExecutionRequestDto
            {
                Parameters = request.Inputs
            };

            // Execute using the system user
            var execution = await _executionService.ExecuteProgramAsync(
                appId,
                systemUserObjectId,
                executionRequest,
                cancellationToken);

            return new ExecutionResponseDto
            {
                ExecutionId = execution.Id ?? string.Empty,
                Status = execution.Status ?? "Unknown",
                Result = execution.Results?.Output,
                ErrorMessage = execution.Results?.Error
            };
        }

        // Public Execution Monitoring Methods
        public async Task<PublicExecutionDetailDto> GetPublicExecutionDetailAsync(string executionId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(executionId, out _))
                throw new ArgumentException("Invalid execution ID format", nameof(executionId));

            // Get execution details
            var execution = await _executionService.GetByIdAsync(executionId, cancellationToken);
            if (execution == null)
                throw new KeyNotFoundException($"Execution with ID {executionId} not found");

            // Load program and verify IsPublic
            if (!ObjectId.TryParse(execution.ProgramId, out var programId))
                throw new InvalidOperationException("Invalid program ID in execution");

            var program = await _unitOfWork.Programs.GetByIdAsync(programId, cancellationToken);
            if (program == null || !program.IsPublic)
                throw new UnauthorizedAccessException("This execution is not publicly accessible");

            // Map to public DTO
            return new PublicExecutionDetailDto
            {
                ExecutionId = execution.Id,
                Status = execution.Status,
                StartedAt = execution.StartedAt,
                CompletedAt = execution.CompletedAt,
                Parameters = execution.Parameters,
                ExitCode = execution.Results?.ExitCode,
                ErrorMessage = execution.Results?.Error,
                Duration = execution.CompletedAt.HasValue
                    ? (execution.CompletedAt.Value - execution.StartedAt).TotalSeconds
                    : null
            };
        }

        public async Task<PublicExecutionLogsDto> GetPublicExecutionLogsAsync(string executionId, int lines = 100, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(executionId, out _))
                throw new ArgumentException("Invalid execution ID format", nameof(executionId));

            // Get execution to verify ownership
            var execution = await _executionService.GetByIdAsync(executionId, cancellationToken);
            if (execution == null)
                throw new KeyNotFoundException($"Execution with ID {executionId} not found");

            // Load program and verify IsPublic
            if (!ObjectId.TryParse(execution.ProgramId, out var programId))
                throw new InvalidOperationException("Invalid program ID in execution");

            var program = await _unitOfWork.Programs.GetByIdAsync(programId, cancellationToken);
            if (program == null || !program.IsPublic)
                throw new UnauthorizedAccessException("This execution is not publicly accessible");

            // Get logs from execution service
            var logs = await _executionService.GetExecutionLogsAsync(executionId, lines, cancellationToken);

            return new PublicExecutionLogsDto
            {
                ExecutionId = executionId,
                Logs = logs,
                TotalLines = logs.Count
            };
        }

        public async Task<PublicExecutionFilesDto> GetPublicExecutionFilesAsync(string executionId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(executionId, out _))
                throw new ArgumentException("Invalid execution ID format", nameof(executionId));

            // Get execution to verify ownership
            var execution = await _executionService.GetByIdAsync(executionId, cancellationToken);
            if (execution == null)
                throw new KeyNotFoundException($"Execution with ID {executionId} not found");

            // Load program and verify IsPublic
            if (!ObjectId.TryParse(execution.ProgramId, out var programId))
                throw new InvalidOperationException("Invalid program ID in execution");

            var program = await _unitOfWork.Programs.GetByIdAsync(programId, cancellationToken);
            if (program == null || !program.IsPublic)
                throw new UnauthorizedAccessException("This execution is not publicly accessible");

            // Get execution result to access output files
            var result = await _executionService.GetExecutionResultAsync(executionId, cancellationToken);

            return new PublicExecutionFilesDto
            {
                ExecutionId = executionId,
                Files = result.OutputFiles,
                TotalFiles = result.OutputFiles?.Count ?? 0
            };
        }

        public async Task<VersionFileDetailDto> DownloadPublicExecutionFileAsync(string executionId, string filePath, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(executionId, out _))
                throw new ArgumentException("Invalid execution ID format", nameof(executionId));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required", nameof(filePath));

            // Get execution to verify ownership
            var execution = await _executionService.GetByIdAsync(executionId, cancellationToken);
            if (execution == null)
                throw new KeyNotFoundException($"Execution with ID {executionId} not found");

            // Load program and verify IsPublic
            if (!ObjectId.TryParse(execution.ProgramId, out var programId))
                throw new InvalidOperationException("Invalid program ID in execution");

            var program = await _unitOfWork.Programs.GetByIdAsync(programId, cancellationToken);
            if (program == null || !program.IsPublic)
                throw new UnauthorizedAccessException("This execution is not publicly accessible");

            // Get execution file from storage
            return await _fileStorageService.GetExecutionFileAsync(
                execution.ProgramId,
                execution.VersionId,
                execution.Id,
                filePath,
                cancellationToken);
        }

        public async Task<FileDownloadResponseDto> DownloadAllPublicExecutionFilesAsync(string executionId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(executionId, out _))
                throw new ArgumentException("Invalid execution ID format", nameof(executionId));

            // Get execution to verify ownership
            var execution = await _executionService.GetByIdAsync(executionId, cancellationToken);
            if (execution == null)
                throw new KeyNotFoundException($"Execution with ID {executionId} not found");

            // Load program and verify IsPublic
            if (!ObjectId.TryParse(execution.ProgramId, out var programId))
                throw new InvalidOperationException("Invalid program ID in execution");

            var program = await _unitOfWork.Programs.GetByIdAsync(programId, cancellationToken);
            if (program == null || !program.IsPublic)
                throw new UnauthorizedAccessException("This execution is not publicly accessible");

            // Get all execution files as ZIP using the existing method
            var zipResult = await _fileStorageService.CreateExecutionZipArchiveAsync(execution, cancellationToken);

            // Convert byte array to stream
            var zipStream = new MemoryStream(zipResult.ZipContent);

            return new FileDownloadResponseDto
            {
                FileStream = zipStream,
                FileName = $"execution-{executionId}-files.zip"
            };
        }

        public async Task<PublicExecutionDetailExtendedDto> GetPublicExecutionDetailsAsync(string executionId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(executionId, out _))
                throw new ArgumentException("Invalid execution ID format", nameof(executionId));

            // Get execution details
            var execution = await _executionService.GetByIdAsync(executionId, cancellationToken);
            if (execution == null)
                throw new KeyNotFoundException($"Execution with ID {executionId} not found");

            // Load program and verify IsPublic
            if (!ObjectId.TryParse(execution.ProgramId, out var programId))
                throw new InvalidOperationException("Invalid program ID in execution");

            var program = await _unitOfWork.Programs.GetByIdAsync(programId, cancellationToken);
            if (program == null || !program.IsPublic)
                throw new UnauthorizedAccessException("This execution is not publicly accessible");

            // Map to extended public DTO with resource usage and results
            return new PublicExecutionDetailExtendedDto
            {
                ExecutionId = execution.Id,
                Status = execution.Status,
                StartedAt = execution.StartedAt,
                CompletedAt = execution.CompletedAt,
                Parameters = execution.Parameters,
                ErrorMessage = execution.Results?.Error,
                Duration = execution.CompletedAt.HasValue
                    ? (execution.CompletedAt.Value - execution.StartedAt).TotalSeconds
                    : null,
                ResourceUsage = execution.ResourceUsage != null ? new ExecutionResourceUsageExtendedDto
                {
                    MaxMemoryUsedMb = execution.ResourceUsage.MemoryUsed / (1024.0 * 1024.0),
                    MaxCpuPercent = execution.ResourceUsage.CpuPercentage,
                    ExecutionTimeMinutes = execution.CompletedAt.HasValue
                        ? (execution.CompletedAt.Value - execution.StartedAt).TotalMinutes
                        : 0
                } : null,
                Result = execution.Results != null ? new ExecutionResultExtendedDto
                {
                    ExitCode = execution.Results.ExitCode,
                    Output = execution.Results.Output ?? string.Empty,
                    ErrorOutput = execution.Results.Error ?? string.Empty
                } : null
            };
        }

        public async Task<ExecutionStopResponseDto> StopPublicExecutionAsync(string executionId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(executionId, out _))
                throw new ArgumentException("Invalid execution ID format", nameof(executionId));

            // Get execution to verify ownership
            var execution = await _executionService.GetByIdAsync(executionId, cancellationToken);
            if (execution == null)
                throw new KeyNotFoundException($"Execution with ID {executionId} not found");

            // Load program and verify IsPublic
            if (!ObjectId.TryParse(execution.ProgramId, out var programId))
                throw new InvalidOperationException("Invalid program ID in execution");

            var program = await _unitOfWork.Programs.GetByIdAsync(programId, cancellationToken);
            if (program == null || !program.IsPublic)
                throw new UnauthorizedAccessException("This execution is not publicly accessible");

            // Stop the execution
            var success = await _executionService.StopExecutionAsync(executionId, cancellationToken);

            return new ExecutionStopResponseDto
            {
                Success = success
            };
        }

        public async Task<RemoteAppLaunchResponseDto> LaunchRemoteAppAsync(string appId, CancellationToken cancellationToken = default)
        {
            if (!ObjectId.TryParse(appId, out var remoteAppId))
                throw new ArgumentException("Invalid app ID format", nameof(appId));

            // Fetch the remote app
            var remoteApp = await _unitOfWork.RemoteApps.GetByIdAsync(remoteAppId, cancellationToken);
            if (remoteApp == null)
                throw new KeyNotFoundException($"Remote app with ID {appId} not found");

            // Verify IsPublic
            if (!remoteApp.IsPublic)
                throw new UnauthorizedAccessException("This remote application is not publicly accessible");

            // Build redirect URL with SSO credentials if configured
            var redirectUrl = remoteApp.Url;
            var requiresSso = false;

            // Check if SSO URL is configured
            if (!string.IsNullOrEmpty(remoteApp.SsoUrl))
            {
                requiresSso = true;
                var separator = redirectUrl.Contains("?") ? "&" : "?";
                redirectUrl = $"{remoteApp.SsoUrl}{separator}username={Uri.EscapeDataString(remoteApp.DefaultUsername)}&password={Uri.EscapeDataString(remoteApp.DefaultPassword)}";
            }

            return new RemoteAppLaunchResponseDto
            {
                RedirectUrl = redirectUrl,
                RequiresSso = requiresSso
            };
        }

        // Helper methods to fetch apps by IDs
        private async Task<List<Program>> GetProgramsByIdsAsync(List<ObjectId> ids, CancellationToken cancellationToken)
        {
            if (!ids.Any())
                return new List<Program>();

            var programs = await _unitOfWork.Programs.FindAsync(p => ids.Contains(p._ID), cancellationToken);
            return programs.ToList();
        }

        private async Task<List<Workflow>> GetWorkflowsByIdsAsync(List<ObjectId> ids, CancellationToken cancellationToken)
        {
            if (!ids.Any())
                return new List<Workflow>();

            var workflows = await _unitOfWork.Workflows.FindAsync(w => ids.Contains(w._ID), cancellationToken);
            return workflows.ToList();
        }

        private async Task<List<RemoteApp>> GetRemoteAppsByIdsAsync(List<ObjectId> ids, CancellationToken cancellationToken)
        {
            if (!ids.Any())
                return new List<RemoteApp>();

            var remoteApps = await _unitOfWork.RemoteApps.FindAsync(r => ids.Contains(r._ID), cancellationToken);
            return remoteApps.ToList();
        }
    }
}
