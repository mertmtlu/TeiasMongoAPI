using AutoMapper;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
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
        public ProgramService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<ProgramService> logger)
            : base(unitOfWork, mapper, logger)
        {
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

            // Enrich with additional data
            await EnrichProgramDetailAsync(dto, program, cancellationToken);

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

            // Enrich list items
            await EnrichProgramListAsync(dtos, cancellationToken);

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

            if (searchDto.CreatedFrom.HasValue)
            {
                filteredPrograms = filteredPrograms.Where(p => p.CreatedAt >= searchDto.CreatedFrom.Value);
            }

            if (searchDto.CreatedTo.HasValue)
            {
                filteredPrograms = filteredPrograms.Where(p => p.CreatedAt <= searchDto.CreatedTo.Value);
            }

            if (searchDto.DeploymentType.HasValue)
            {
                filteredPrograms = filteredPrograms.Where(p => p.DeploymentInfo != null && p.DeploymentInfo.DeploymentType == searchDto.DeploymentType.Value);
            }

            var programsList = filteredPrograms.ToList();

            // Apply pagination
            var totalCount = programsList.Count;
            var paginatedPrograms = programsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ProgramListDto>>(paginatedPrograms);

            // Enrich list items
            await EnrichProgramListAsync(dtos, cancellationToken);

            return new PagedResponse<ProgramListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<ProgramDto> CreateAsync(ProgramCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate name uniqueness
            if (!await ValidateNameUniqueAsync(dto.Name, null, cancellationToken))
            {
                throw new InvalidOperationException($"Program with name '{dto.Name}' already exists.");
            }

            var program = _mapper.Map<Program>(dto);
            program.CreatedAt = DateTime.UtcNow;
            program.Status = "draft";

            // Set creator from current context (would come from authentication context)
            // program.Creator = currentUserId; // This would be injected via service context

            var createdProgram = await _unitOfWork.Programs.CreateAsync(program, cancellationToken);

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
                if (!await ValidateNameUniqueAsync(dto.Name, id, cancellationToken))
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

            // Check if program can be deleted (no active executions, etc.)
            var canDelete = await CanDeleteProgramAsync(objectId, cancellationToken);
            if (!canDelete)
            {
                throw new InvalidOperationException("Program cannot be deleted. It may have active executions or dependencies.");
            }

            return await _unitOfWork.Programs.DeleteAsync(objectId, cancellationToken);
        }

        #endregion

        #region Program-specific Operations

        public async Task<PagedResponse<ProgramListDto>> GetByCreatorAsync(string creatorId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetByCreatorAsync(creatorId, cancellationToken);
            var programsList = programs.ToList();

            // Apply pagination
            var totalCount = programsList.Count;
            var paginatedPrograms = programsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ProgramListDto>>(paginatedPrograms);
            await EnrichProgramListAsync(dtos, cancellationToken);

            return new PagedResponse<ProgramListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<ProgramListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetByStatusAsync(status, cancellationToken);
            var programsList = programs.ToList();

            // Apply pagination
            var totalCount = programsList.Count;
            var paginatedPrograms = programsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ProgramListDto>>(paginatedPrograms);
            await EnrichProgramListAsync(dtos, cancellationToken);

            return new PagedResponse<ProgramListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<ProgramListDto>> GetByTypeAsync(string type, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetByTypeAsync(type, cancellationToken);
            var programsList = programs.ToList();

            // Apply pagination
            var totalCount = programsList.Count;
            var paginatedPrograms = programsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ProgramListDto>>(paginatedPrograms);
            await EnrichProgramListAsync(dtos, cancellationToken);

            return new PagedResponse<ProgramListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<ProgramListDto>> GetByLanguageAsync(string language, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetByLanguageAsync(language, cancellationToken);
            var programsList = programs.ToList();

            // Apply pagination
            var totalCount = programsList.Count;
            var paginatedPrograms = programsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ProgramListDto>>(paginatedPrograms);
            await EnrichProgramListAsync(dtos, cancellationToken);

            return new PagedResponse<ProgramListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<ProgramListDto>> GetUserAccessibleProgramsAsync(string userId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetUserAccessibleProgramsAsync(userId, cancellationToken);
            var programsList = programs.ToList();

            // Apply pagination
            var totalCount = programsList.Count;
            var paginatedPrograms = programsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ProgramListDto>>(paginatedPrograms);
            await EnrichProgramListAsync(dtos, cancellationToken);

            return new PagedResponse<ProgramListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<ProgramListDto>> GetGroupAccessibleProgramsAsync(string groupId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programs = await _unitOfWork.Programs.GetGroupAccessibleProgramsAsync(groupId, cancellationToken);
            var programsList = programs.ToList();

            // Apply pagination
            var totalCount = programsList.Count;
            var paginatedPrograms = programsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<ProgramListDto>>(paginatedPrograms);
            await EnrichProgramListAsync(dtos, cancellationToken);

            return new PagedResponse<ProgramListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        #endregion

        #region Status Management

        public async Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {id} not found.");
            }

            return await _unitOfWork.Programs.UpdateStatusAsync(objectId, status, cancellationToken);
        }

        public async Task<bool> UpdateCurrentVersionAsync(string id, string versionId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {id} not found.");
            }

            return await _unitOfWork.Programs.UpdateCurrentVersionAsync(objectId, versionId, cancellationToken);
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

            var success = await _unitOfWork.Programs.AddUserPermissionAsync(objectId, dto.UserId, dto.AccessLevel, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException("Failed to add user permission.");
            }

            var updatedProgram = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);
            return _mapper.Map<ProgramDto>(updatedProgram);
        }

        public async Task<bool> RemoveUserPermissionAsync(string programId, string userId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            return await _unitOfWork.Programs.RemoveUserPermissionAsync(objectId, userId, cancellationToken);
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
                throw new InvalidOperationException("Failed to update user permission.");
            }

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
                throw new InvalidOperationException("Failed to add group permission.");
            }

            var updatedProgram = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);
            return _mapper.Map<ProgramDto>(updatedProgram);
        }

        public async Task<bool> RemoveGroupPermissionAsync(string programId, string groupId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            return await _unitOfWork.Programs.RemoveGroupPermissionAsync(objectId, groupId, cancellationToken);
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
                throw new InvalidOperationException("Failed to update group permission.");
            }

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
                // In a real implementation, you'd fetch user details
                permissions.Add(new ProgramPermissionDto
                {
                    Type = "user",
                    Id = userPerm.UserId,
                    Name = userPerm.UserId, // Would fetch actual user name
                    AccessLevel = userPerm.AccessLevel
                });
            }

            // Add group permissions
            foreach (var groupPerm in program.Permissions.Groups)
            {
                // In a real implementation, you'd fetch group details
                permissions.Add(new ProgramPermissionDto
                {
                    Type = "group",
                    Id = groupPerm.GroupId,
                    Name = groupPerm.GroupId, // Would fetch actual group name
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
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Implement file storage logic
            // This would involve saving files to storage and updating program metadata

            _logger.LogInformation("Uploading {FileCount} files for program {ProgramId}", files.Count, programId);

            return true; // Placeholder
        }

        public async Task<List<ProgramFileDto>> GetFilesAsync(string programId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Implement file listing logic
            // This would involve reading from storage and returning file metadata

            return new List<ProgramFileDto>(); // Placeholder
        }

        public async Task<ProgramFileContentDto> GetFileContentAsync(string programId, string filePath, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Implement file content retrieval logic
            // This would involve reading file content from storage

            throw new NotImplementedException("File content retrieval not yet implemented.");
        }

        public async Task<bool> UpdateFileAsync(string programId, string filePath, ProgramFileUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Implement file update logic

            return true; // Placeholder
        }

        public async Task<bool> DeleteFileAsync(string programId, string filePath, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Implement file deletion logic

            return true; // Placeholder
        }

        #endregion

        #region Deployment Operations

        public async Task<ProgramDeploymentDto> DeployPreBuiltAppAsync(string programId, ProgramDeploymentRequestDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Implement pre-built app deployment logic

            return new ProgramDeploymentDto
            {
                Id = programId,
                DeploymentType = AppDeploymentType.PreBuiltWebApp,
                Status = "active",
                LastDeployed = DateTime.UtcNow,
                Configuration = dto.Configuration,
                SupportedFeatures = dto.SupportedFeatures
            };
        }

        public async Task<ProgramDeploymentDto> DeployStaticSiteAsync(string programId, ProgramDeploymentRequestDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Implement static site deployment logic

            return new ProgramDeploymentDto
            {
                Id = programId,
                DeploymentType = AppDeploymentType.StaticSite,
                Status = "active",
                LastDeployed = DateTime.UtcNow,
                Configuration = dto.Configuration,
                SupportedFeatures = dto.SupportedFeatures
            };
        }

        public async Task<ProgramDeploymentDto> DeployContainerAppAsync(string programId, ProgramDeploymentRequestDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Implement container app deployment logic

            return new ProgramDeploymentDto
            {
                Id = programId,
                DeploymentType = AppDeploymentType.DockerContainer,
                Status = "building",
                LastDeployed = DateTime.UtcNow,
                Configuration = dto.Configuration,
                SupportedFeatures = dto.SupportedFeatures
            };
        }

        public async Task<ProgramDeploymentStatusDto> GetDeploymentStatusAsync(string programId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Implement deployment status retrieval logic

            return new ProgramDeploymentStatusDto
            {
                DeploymentType = program.DeploymentInfo?.DeploymentType ?? AppDeploymentType.SourceCode,
                Status = program.DeploymentInfo?.Status ?? "inactive",
                LastDeployed = program.DeploymentInfo?.LastDeployed,
                IsHealthy = true,
                LastHealthCheck = DateTime.UtcNow
            };
        }

        public async Task<bool> RestartApplicationAsync(string programId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Implement application restart logic

            return true;
        }

        public async Task<List<string>> GetApplicationLogsAsync(string programId, int lines = 100, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Implement log retrieval logic

            return new List<string> { "Log line 1", "Log line 2", "Log line 3" }; // Placeholder
        }

        public async Task<ProgramDto> UpdateDeploymentConfigAsync(string programId, ProgramDeploymentConfigDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(objectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Update deployment configuration
            if (program.DeploymentInfo != null)
            {
                program.DeploymentInfo.Configuration = dto.Configuration;
                program.DeploymentInfo.SupportedFeatures = dto.SupportedFeatures;
            }

            var success = await _unitOfWork.Programs.UpdateAsync(objectId, program, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException("Failed to update deployment configuration.");
            }

            return _mapper.Map<ProgramDto>(program);
        }

        #endregion

        #region Validation

        public async Task<bool> ValidateNameUniqueAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default)
        {
            ObjectId? excludeObjectId = null;
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

            // Check if user is creator
            if (program.Creator == userId)
            {
                return true;
            }

            // Check user permissions
            var userPermission = program.Permissions.Users.FirstOrDefault(u => u.UserId == userId);
            if (userPermission != null)
            {
                return ValidateAccessLevel(userPermission.AccessLevel, requiredAccessLevel);
            }

            // TODO: Check group permissions if user belongs to any groups

            return false;
        }

        #endregion

        #region Private Helper Methods

        private async Task EnrichProgramDetailAsync(ProgramDetailDto dto, Program program, CancellationToken cancellationToken)
        {
            // Enrich with permissions
            dto.Permissions = await GetProgramPermissionsAsync(dto.Id, cancellationToken);

            // Enrich with files
            dto.Files = await GetFilesAsync(dto.Id, cancellationToken);

            // Enrich with deployment status
            dto.DeploymentStatus = await GetDeploymentStatusAsync(dto.Id, cancellationToken);

            // TODO: Enrich with stats (execution count, etc.)
        }

        private async Task EnrichProgramListAsync(List<ProgramListDto> dtos, CancellationToken cancellationToken)
        {
            foreach (var dto in dtos)
            {
                // TODO: Add user name resolution, deployment status, etc.
                await Task.CompletedTask; // Placeholder
            }
        }

        private async Task<bool> CanDeleteProgramAsync(ObjectId programId, CancellationToken cancellationToken)
        {
            // TODO: Check for active executions, dependencies, etc.
            await Task.CompletedTask; // Placeholder
            return true;
        }

        private static bool ValidateAccessLevel(string userLevel, string requiredLevel)
        {
            // Define access level hierarchy: read < write < admin
            var levels = new Dictionary<string, int>
            {
                ["read"] = 1,
                ["write"] = 2,
                ["admin"] = 3
            };

            return levels.TryGetValue(userLevel, out var userLevelValue) &&
                   levels.TryGetValue(requiredLevel, out var requiredLevelValue) &&
                   userLevelValue >= requiredLevelValue;
        }

        #endregion
    }
}