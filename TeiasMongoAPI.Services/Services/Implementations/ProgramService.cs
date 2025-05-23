using AutoMapper;
using Microsoft.Extensions.Logging;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;
using TeiasMongoAPI.Services.Interfaces;
using TeiasMongoAPI.Services.Services.Base;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class ProgramService : BaseService, IProgramService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IDeploymentService _deploymentService;

        public ProgramService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IFileStorageService fileStorageService,
            IDeploymentService deploymentService,
            ILogger<ProgramService> logger)
            : base(unitOfWork, mapper, logger)
        {
            _fileStorageService = fileStorageService;
            _deploymentService = deploymentService;
        }

        #region Basic CRUD Operations

        public async Task<ProgramDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {id} not found.");
            }

            var dto = _mapper.Map<ProgramDetailDto>(program);

            // Get permissions
            var permissions = new List<ProgramPermissionDto>();

            // Add user permissions
            foreach (var userPerm in program.Permissions.Users)
            {
                try
                {
                    var user = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(userPerm.UserId), cancellationToken);
                    if (user != null)
                    {
                        permissions.Add(new ProgramPermissionDto
                        {
                            Type = "user",
                            Id = userPerm.UserId,
                            Name = user.FullName,
                            AccessLevel = userPerm.AccessLevel
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get user {UserId} for program {ProgramId}", userPerm.UserId, id);
                }
            }

            // Add group permissions
            foreach (var groupPerm in program.Permissions.Groups)
            {
                permissions.Add(new ProgramPermissionDto
                {
                    Type = "group",
                    Id = groupPerm.GroupId,
                    Name = $"Group {groupPerm.GroupId}",
                    AccessLevel = groupPerm.AccessLevel
                });
            }

            dto.Permissions = permissions;

            // Get files
            try
            {
                var files = await GetFilesAsync(id, cancellationToken);
                dto.Files = files;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get files for program {ProgramId}", id);
                dto.Files = new List<ProgramFileDto>();
            }

            // Get deployment status
            try
            {
                var deploymentStatus = await _deploymentService.GetDeploymentStatusAsync(id, cancellationToken);
                dto.DeploymentStatus = deploymentStatus;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get deployment status for program {ProgramId}", id);
                dto.DeploymentStatus = null;
            }

            // Get program statistics
            var executions = await _unitOfWork.Executions.GetByProgramIdAsync(objectId, cancellationToken);
            var executionsList = executions.ToList();
            var versions = await _unitOfWork.Versions.GetByProgramIdAsync(objectId, cancellationToken);

            var completedExecutions = executionsList.Where(e => e.CompletedAt.HasValue).ToList();

            dto.Stats = new ProgramStatsDto
            {
                TotalExecutions = executionsList.Count,
                SuccessfulExecutions = executionsList.Count(e => e.Status == "completed" && e.Results.ExitCode == 0),
                FailedExecutions = executionsList.Count(e => e.Status == "failed" || (e.Status == "completed" && e.Results.ExitCode != 0)),
                LastExecution = executionsList.OrderByDescending(e => e.StartedAt).FirstOrDefault()?.StartedAt,
                AverageExecutionTime = completedExecutions.Any()
                    ? completedExecutions.Average(e => (e.CompletedAt!.Value - e.StartedAt).TotalMinutes)
                    : 0,
                TotalVersions = versions.Count(),
                LastUpdate = versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault()?.CreatedAt
            };

            return dto;
        }

        public async Task<PagedResponse<ProgramListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetAllAsync(cancellationToken);
            var programsList = programs.ToList();

            // Apply pagination
            var totalCount = programsList.Count;
            var paginatedPrograms = programsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ProgramListDto>>(paginatedPrograms);

            return new PagedResponse<ProgramListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<ProgramListDto>> SearchAsync(ProgramSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var allPrograms = await _unitOfWork.Programs.GetAllAsync(cancellationToken);
            var filteredPrograms = allPrograms.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchDto.Name))
            {
                filteredPrograms = filteredPrograms.Where(p => p.Name.Contains(searchDto.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(searchDto.Description))
            {
                filteredPrograms = filteredPrograms.Where(p => p.Description.Contains(searchDto.Description, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(searchDto.Type))
            {
                filteredPrograms = filteredPrograms.Where(p => p.Type == searchDto.Type);
            }

            if (!string.IsNullOrEmpty(searchDto.Language))
            {
                filteredPrograms = filteredPrograms.Where(p => p.Language == searchDto.Language);
            }

            if (!string.IsNullOrEmpty(searchDto.UiType))
            {
                filteredPrograms = filteredPrograms.Where(p => p.UiType == searchDto.UiType);
            }

            if (!string.IsNullOrEmpty(searchDto.Creator))
            {
                filteredPrograms = filteredPrograms.Where(p => p.Creator == searchDto.Creator);
            }

            if (!string.IsNullOrEmpty(searchDto.Status))
            {
                filteredPrograms = filteredPrograms.Where(p => p.Status == searchDto.Status);
            }

            if (searchDto.DeploymentType.HasValue)
            {
                filteredPrograms = filteredPrograms.Where(p => p.DeploymentInfo != null && p.DeploymentInfo.DeploymentType == searchDto.DeploymentType.Value);
            }

            if (searchDto.CreatedFrom.HasValue)
            {
                filteredPrograms = filteredPrograms.Where(p => p.CreatedAt >= searchDto.CreatedFrom.Value);
            }

            if (searchDto.CreatedTo.HasValue)
            {
                filteredPrograms = filteredPrograms.Where(p => p.CreatedAt <= searchDto.CreatedTo.Value);
            }

            var programsList = filteredPrograms.ToList();

            // Apply pagination
            var totalCount = programsList.Count;
            var paginatedPrograms = programsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ProgramListDto>>(paginatedPrograms);

            return new PagedResponse<ProgramListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<ProgramDto> CreateAsync(ProgramCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate name uniqueness
            if (!await _unitOfWork.Programs.IsNameUniqueAsync(dto.Name, null, cancellationToken))
            {
                throw new InvalidOperationException($"Program with name '{dto.Name}' already exists.");
            }

            var program = _mapper.Map<Program>(dto);
            program.CreatedAt = DateTime.UtcNow;
            program.Status = "draft";

            var createdProgram = await _unitOfWork.Programs.CreateAsync(program, cancellationToken);

            _logger.LogInformation("Created program {ProgramId} with name {ProgramName}", createdProgram._ID, createdProgram.Name);

            return _mapper.Map<ProgramDto>(createdProgram);
        }

        public async Task<ProgramDto> UpdateAsync(string id, ProgramUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var existingProgram = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (existingProgram == null)
            {
                throw new KeyNotFoundException($"Program with ID {id} not found.");
            }

            // If updating name, check uniqueness
            if (!string.IsNullOrEmpty(dto.Name) && dto.Name != existingProgram.Name)
            {
                if (!await _unitOfWork.Programs.IsNameUniqueAsync(dto.Name, objectId, cancellationToken))
                {
                    throw new InvalidOperationException($"Program with name '{dto.Name}' already exists.");
                }
            }

            _mapper.Map(dto, existingProgram);

            var success = await _unitOfWork.Programs.UpdateAsync(objectId, existingProgram, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update program with ID {id}.");
            }

            _logger.LogInformation("Updated program {ProgramId}", id);

            return _mapper.Map<ProgramDto>(existingProgram);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {id} not found.");
            }

            // Check if there are any executions for this program
            var executions = await _unitOfWork.Executions.GetByProgramIdAsync(objectId, cancellationToken);
            if (executions.Any())
            {
                throw new InvalidOperationException("Cannot delete program that has execution history. Consider deactivating instead.");
            }

            // Undeploy if deployed
            if (program.DeploymentInfo != null)
            {
                try
                {
                    await _deploymentService.UndeployApplicationAsync(id, cancellationToken);
                    _logger.LogInformation("Undeployed application for program {ProgramId} during deletion", id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to undeploy application for program {ProgramId} during deletion", id);
                }
            }

            // Delete associated files
            try
            {
                await _fileStorageService.DeleteProgramFilesAsync(id, cancellationToken);
                _logger.LogInformation("Deleted files for program {ProgramId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete files for program {ProgramId}", id);
            }

            var success = await _unitOfWork.Programs.DeleteAsync(objectId, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Deleted program {ProgramId}", id);
            }

            return success;
        }

        #endregion

        #region Program-specific Operations

        public async Task<PagedResponse<ProgramListDto>> GetByCreatorAsync(string creatorId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetByCreatorAsync(creatorId, cancellationToken);
            return await CreatePagedResponse(programs, pagination);
        }

        public async Task<PagedResponse<ProgramListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetByStatusAsync(status, cancellationToken);
            return await CreatePagedResponse(programs, pagination);
        }

        public async Task<PagedResponse<ProgramListDto>> GetByTypeAsync(string type, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetByTypeAsync(type, cancellationToken);
            return await CreatePagedResponse(programs, pagination);
        }

        public async Task<PagedResponse<ProgramListDto>> GetByLanguageAsync(string language, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetByLanguageAsync(language, cancellationToken);
            return await CreatePagedResponse(programs, pagination);
        }

        public async Task<PagedResponse<ProgramListDto>> GetUserAccessibleProgramsAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetUserAccessibleProgramsAsync(userId, cancellationToken);
            return await CreatePagedResponse(programs, pagination);
        }

        public async Task<PagedResponse<ProgramListDto>> GetGroupAccessibleProgramsAsync(string groupId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetGroupAccessibleProgramsAsync(groupId, cancellationToken);
            return await CreatePagedResponse(programs, pagination);
        }

        #endregion

        #region Status Management

        public async Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {id} not found.");
            }

            var success = await _unitOfWork.Programs.UpdateStatusAsync(objectId, status, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Updated status of program {ProgramId} to {Status}", id, status);
            }

            return success;
        }

        public async Task<bool> UpdateCurrentVersionAsync(string id, string versionId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {id} not found.");
            }

            var versionObjectId = ParseObjectId(versionId);
            if (!await _unitOfWork.Versions.ExistsAsync(versionObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            var success = await _unitOfWork.Programs.UpdateCurrentVersionAsync(objectId, versionId, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Updated current version of program {ProgramId} to {VersionId}", id, versionId);
            }

            return success;
        }

        #endregion

        #region Permission Management

        public async Task<ProgramDto> AddUserPermissionAsync(string programId, ProgramUserPermissionDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Verify user exists
            var userObjectId = ParseObjectId(dto.UserId);
            if (!await _unitOfWork.Users.ExistsAsync(userObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"User with ID {dto.UserId} not found.");
            }

            var success = await _unitOfWork.Programs.AddUserPermissionAsync(objectId, dto.UserId, dto.AccessLevel, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to add user permission for program {programId}.");
            }

            _logger.LogInformation("Added user permission for user {UserId} on program {ProgramId} with access level {AccessLevel}",
                dto.UserId, programId, dto.AccessLevel);

            var updatedProgram = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);
            return _mapper.Map<ProgramDto>(updatedProgram);
        }

        public async Task<bool> RemoveUserPermissionAsync(string programId, string userId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var success = await _unitOfWork.Programs.RemoveUserPermissionAsync(objectId, userId, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Removed user permission for user {UserId} from program {ProgramId}", userId, programId);
            }

            return success;
        }

        public async Task<ProgramDto> UpdateUserPermissionAsync(string programId, ProgramUserPermissionDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var success = await _unitOfWork.Programs.UpdateUserPermissionAsync(objectId, dto.UserId, dto.AccessLevel, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update user permission for program {programId}.");
            }

            _logger.LogInformation("Updated user permission for user {UserId} on program {ProgramId} to access level {AccessLevel}",
                dto.UserId, programId, dto.AccessLevel);

            var updatedProgram = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);
            return _mapper.Map<ProgramDto>(updatedProgram);
        }

        public async Task<ProgramDto> AddGroupPermissionAsync(string programId, ProgramGroupPermissionDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var success = await _unitOfWork.Programs.AddGroupPermissionAsync(objectId, dto.GroupId, dto.AccessLevel, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to add group permission for program {programId}.");
            }

            _logger.LogInformation("Added group permission for group {GroupId} on program {ProgramId} with access level {AccessLevel}",
                dto.GroupId, programId, dto.AccessLevel);

            var updatedProgram = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);
            return _mapper.Map<ProgramDto>(updatedProgram);
        }

        public async Task<bool> RemoveGroupPermissionAsync(string programId, string groupId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var success = await _unitOfWork.Programs.RemoveGroupPermissionAsync(objectId, groupId, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Removed group permission for group {GroupId} from program {ProgramId}", groupId, programId);
            }

            return success;
        }

        public async Task<ProgramDto> UpdateGroupPermissionAsync(string programId, ProgramGroupPermissionDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var success = await _unitOfWork.Programs.UpdateGroupPermissionAsync(objectId, dto.GroupId, dto.AccessLevel, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update group permission for program {programId}.");
            }

            _logger.LogInformation("Updated group permission for group {GroupId} on program {ProgramId} to access level {AccessLevel}",
                dto.GroupId, programId, dto.AccessLevel);

            var updatedProgram = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);
            return _mapper.Map<ProgramDto>(updatedProgram);
        }

        public async Task<List<ProgramPermissionDto>> GetProgramPermissionsAsync(string programId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var permissions = new List<ProgramPermissionDto>();

            // Add user permissions
            foreach (var userPerm in program.Permissions.Users)
            {
                try
                {
                    var user = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(userPerm.UserId), cancellationToken);
                    if (user != null)
                    {
                        permissions.Add(new ProgramPermissionDto
                        {
                            Type = "user",
                            Id = userPerm.UserId,
                            Name = user.FullName,
                            AccessLevel = userPerm.AccessLevel
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get user {UserId} for program permissions", userPerm.UserId);
                }
            }

            // Add group permissions
            foreach (var groupPerm in program.Permissions.Groups)
            {
                permissions.Add(new ProgramPermissionDto
                {
                    Type = "group",
                    Id = groupPerm.GroupId,
                    Name = $"Group {groupPerm.GroupId}",
                    AccessLevel = groupPerm.AccessLevel
                });
            }

            return permissions;
        }

        #endregion

        #region File Management

        public async Task<bool> UploadFilesAsync(string programId, List<ProgramFileUploadDto> files, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            _logger.LogInformation("Starting file upload for program {ProgramId} with {FileCount} files", programId, files.Count);

            // Store files using the file storage service
            var results = await _fileStorageService.StoreFilesAsync(programId, null, files, cancellationToken);

            var successCount = results.Count(r => r.Success);
            var failedResults = results.Where(r => !r.Success).ToList();

            if (failedResults.Any())
            {
                var failedFiles = string.Join(", ", failedResults.Select(r => $"{r.FilePath}: {r.ErrorMessage}"));
                _logger.LogWarning("Failed to upload {FailedCount}/{TotalCount} files for program {ProgramId}: {FailedFiles}",
                    failedResults.Count, files.Count, programId, failedFiles);
            }

            _logger.LogInformation("Successfully uploaded {SuccessCount}/{TotalCount} files for program {ProgramId}",
                successCount, files.Count, programId);

            return successCount > 0;
        }

        public async Task<List<ProgramFileDto>> GetFilesAsync(string programId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            try
            {
                // Get file metadata from storage service
                var fileMetadata = await _fileStorageService.ListProgramFilesAsync(programId, null, cancellationToken);

                return fileMetadata.Select(metadata => new ProgramFileDto
                {
                    Path = metadata.FilePath,
                    ContentType = metadata.ContentType,
                    Size = metadata.Size,
                    LastModified = metadata.LastModified,
                    Hash = metadata.Hash,
                    Description = null // Could be enhanced with additional metadata
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get files for program {ProgramId}", programId);
                return new List<ProgramFileDto>();
            }
        }

        public async Task<ProgramFileContentDto> GetFileContentAsync(string programId, string filePath, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            try
            {
                return await _fileStorageService.GetFileAsync(programId, filePath, null, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get file content for {FilePath} in program {ProgramId}", filePath, programId);
                throw;
            }
        }

        public async Task<bool> UpdateFileAsync(string programId, string filePath, ProgramFileUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            try
            {
                var contentType = dto.ContentType ?? "application/octet-stream";
                await _fileStorageService.UpdateFileAsync(programId, filePath, dto.Content, contentType, null, cancellationToken);

                _logger.LogInformation("Updated file {FilePath} for program {ProgramId}", filePath, programId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update file {FilePath} for program {ProgramId}", filePath, programId);
                return false;
            }
        }

        public async Task<bool> DeleteFileAsync(string programId, string filePath, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            try
            {
                // Get file metadata to find storage key
                var files = await _fileStorageService.ListProgramFilesAsync(programId, null, cancellationToken);
                var fileMetadata = files.FirstOrDefault(f => f.FilePath == filePath);

                if (fileMetadata == null)
                {
                    throw new FileNotFoundException($"File {filePath} not found in program {programId}");
                }

                var success = await _fileStorageService.DeleteFileAsync(fileMetadata.StorageKey, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Deleted file {FilePath} from program {ProgramId}", filePath, programId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file {FilePath} from program {ProgramId}", filePath, programId);
                return false;
            }
        }

        #endregion

        #region Deployment Operations - Now Fully Implemented

        public async Task<ProgramDeploymentDto> DeployPreBuiltAppAsync(string programId, ProgramDeploymentRequestDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var request = new AppDeploymentRequestDto
            {
                DeploymentType = AppDeploymentType.PreBuiltWebApp,
                Configuration = dto.Configuration,
                Environment = new Dictionary<string, string>(),
                SupportedFeatures = dto.SupportedFeatures,
                AutoStart = true,
                SpaRouting = true,
                ApiIntegration = true,
                AuthenticationMode = "jwt_injection"
            };

            return await _deploymentService.DeployPreBuiltAppAsync(programId, request, cancellationToken);
        }

        public async Task<ProgramDeploymentDto> DeployStaticSiteAsync(string programId, ProgramDeploymentRequestDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var request = new StaticSiteDeploymentRequestDto
            {
                DeploymentType = AppDeploymentType.StaticSite,
                Configuration = dto.Configuration,
                Environment = new Dictionary<string, string>(),
                SupportedFeatures = dto.SupportedFeatures,
                AutoStart = true,
                EntryPoint = "index.html",
                CachingStrategy = "aggressive",
                CdnEnabled = false
            };

            return await _deploymentService.DeployStaticSiteAsync(programId, request, cancellationToken);
        }

        public async Task<ProgramDeploymentDto> DeployContainerAppAsync(string programId, ProgramDeploymentRequestDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var request = new ContainerDeploymentRequestDto
            {
                DeploymentType = AppDeploymentType.DockerContainer,
                Configuration = dto.Configuration,
                Environment = new Dictionary<string, string>(),
                SupportedFeatures = dto.SupportedFeatures,
                AutoStart = true,
                DockerfilePath = "Dockerfile",
                Replicas = 1,
                ResourceLimits = new ContainerResourceLimitsDto
                {
                    CpuLimit = "0.5",
                    MemoryLimit = "512M"
                }
            };

            return await _deploymentService.DeployContainerAppAsync(programId, request, cancellationToken);
        }

        public async Task<ProgramDeploymentStatusDto> GetDeploymentStatusAsync(string programId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            return await _deploymentService.GetDeploymentStatusAsync(programId, cancellationToken);
        }

        public async Task<bool> RestartApplicationAsync(string programId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            return await _deploymentService.RestartApplicationAsync(programId, cancellationToken);
        }

        public async Task<List<string>> GetApplicationLogsAsync(string programId, int lines = 100, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            return await _deploymentService.GetApplicationLogsAsync(programId, lines, cancellationToken);
        }

        public async Task<ProgramDto> UpdateDeploymentConfigAsync(string programId, ProgramDeploymentConfigDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var configUpdate = new AppDeploymentConfigUpdateDto
            {
                Configuration = dto.Configuration,
                Environment = new Dictionary<string, string>(),
                SupportedFeatures = dto.SupportedFeatures
            };

            return await _deploymentService.UpdateDeploymentConfigAsync(programId, configUpdate, cancellationToken);
        }

        #endregion

        #region Validation

        public async Task<bool> ValidateNameUniqueAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default)
        {
            MongoDB.Bson.ObjectId? excludeObjectId = null;
            if (!string.IsNullOrEmpty(excludeId))
            {
                excludeObjectId = ParseObjectId(excludeId);
            }

            return await _unitOfWork.Programs.IsNameUniqueAsync(name, excludeObjectId, cancellationToken);
        }

        public async Task<bool> ValidateUserAccessAsync(string programId, string userId, string requiredAccessLevel, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                return false;
            }

            // Check if user is the creator
            if (program.Creator == userId)
            {
                return true;
            }

            // Check user permissions
            var userPermission = program.Permissions.Users.FirstOrDefault(up => up.UserId == userId);
            if (userPermission != null)
            {
                return ValidateAccessLevel(userPermission.AccessLevel, requiredAccessLevel);
            }

            return false;
        }

        #endregion

        #region Private Helper Methods

        private async Task<PagedResponse<ProgramListDto>> CreatePagedResponse(IEnumerable<Program> programs, PaginationRequestDto pagination)
        {
            var programsList = programs.ToList();
            var totalCount = programsList.Count;
            var paginatedPrograms = programsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ProgramListDto>>(paginatedPrograms);
            return new PagedResponse<ProgramListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        private static bool ValidateAccessLevel(string userAccessLevel, string requiredAccessLevel)
        {
            // Define access level hierarchy
            var accessLevels = new Dictionary<string, int>
            {
                { "read", 1 },
                { "write", 2 },
                { "admin", 3 }
            };

            if (!accessLevels.TryGetValue(userAccessLevel, out var userLevel) ||
                !accessLevels.TryGetValue(requiredAccessLevel, out var requiredLevel))
            {
                return false;
            }

            return userLevel >= requiredLevel;
        }

        #endregion
    }
}