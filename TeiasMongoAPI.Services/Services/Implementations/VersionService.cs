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
using Version = TeiasMongoAPI.Core.Models.Collaboration.Version;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class VersionService : BaseService, IVersionService
    {
        public VersionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<VersionService> logger)
            : base(unitOfWork, mapper, logger)
        {
        }

        #region Basic CRUD Operations

        public async Task<VersionDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var version = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {id} not found.");
            }

            var dto = _mapper.Map<VersionDetailDto>(version);

            // Enrich with additional data
            await EnrichVersionDetailAsync(dto, version, cancellationToken);

            return dto;
        }

        public async Task<PagedResponse<VersionListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var versions = await _unitOfWork.Versions.GetAllAsync(cancellationToken);
            var versionsList = versions.ToList();

            // Apply pagination
            var totalCount = versionsList.Count;
            var paginatedVersions = versionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<VersionListDto>>(paginatedVersions);

            // Enrich list items
            await EnrichVersionListAsync(dtos, cancellationToken);

            return new PagedResponse<VersionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<VersionListDto>> SearchAsync(VersionSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var allVersions = await _unitOfWork.Versions.GetAllAsync(cancellationToken);
            var filteredVersions = allVersions.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchDto.ProgramId))
            {
                var programObjectId = ParseObjectId(searchDto.ProgramId);
                filteredVersions = filteredVersions.Where(v => v.ProgramId == programObjectId);
            }

            if (!string.IsNullOrEmpty(searchDto.CreatedBy))
            {
                filteredVersions = filteredVersions.Where(v => v.CreatedBy == searchDto.CreatedBy);
            }

            if (!string.IsNullOrEmpty(searchDto.Reviewer))
            {
                filteredVersions = filteredVersions.Where(v => v.Reviewer == searchDto.Reviewer);
            }

            if (!string.IsNullOrEmpty(searchDto.Status))
            {
                filteredVersions = filteredVersions.Where(v => v.Status == searchDto.Status);
            }

            if (searchDto.CreatedFrom.HasValue)
            {
                filteredVersions = filteredVersions.Where(v => v.CreatedAt >= searchDto.CreatedFrom.Value);
            }

            if (searchDto.CreatedTo.HasValue)
            {
                filteredVersions = filteredVersions.Where(v => v.CreatedAt <= searchDto.CreatedTo.Value);
            }

            if (searchDto.ReviewedFrom.HasValue)
            {
                filteredVersions = filteredVersions.Where(v => v.ReviewedAt >= searchDto.ReviewedFrom.Value);
            }

            if (searchDto.ReviewedTo.HasValue)
            {
                filteredVersions = filteredVersions.Where(v => v.ReviewedAt <= searchDto.ReviewedTo.Value);
            }

            if (searchDto.VersionNumberFrom.HasValue)
            {
                filteredVersions = filteredVersions.Where(v => v.VersionNumber >= searchDto.VersionNumberFrom.Value);
            }

            if (searchDto.VersionNumberTo.HasValue)
            {
                filteredVersions = filteredVersions.Where(v => v.VersionNumber <= searchDto.VersionNumberTo.Value);
            }

            var versionsList = filteredVersions.ToList();

            // Apply pagination
            var totalCount = versionsList.Count;
            var paginatedVersions = versionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<VersionListDto>>(paginatedVersions);

            // Enrich list items
            await EnrichVersionListAsync(dtos, cancellationToken);

            return new PagedResponse<VersionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<VersionDto> CreateAsync(VersionCreateDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(dto.ProgramId);

            // Verify program exists
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);
            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {dto.ProgramId} not found.");
            }

            // Get next version number
            var nextVersionNumber = await _unitOfWork.Versions.GetNextVersionNumberAsync(programObjectId, cancellationToken);

            var version = _mapper.Map<Version>(dto);
            version.ProgramId = programObjectId;
            version.VersionNumber = nextVersionNumber;
            version.CreatedAt = DateTime.UtcNow;
            version.Status = "pending";

            // Set creator from current context (would come from authentication context)
            // version.CreatedBy = currentUserId; // This would be injected via service context

            // Process files
            var versionFiles = new List<VersionFile>();
            foreach (var fileDto in dto.Files)
            {
                var versionFile = new VersionFile
                {
                    Path = fileDto.Path,
                    StorageKey = GenerateStorageKey(version._ID, fileDto.Path),
                    Hash = ComputeFileHash(fileDto.Content),
                    Size = fileDto.Content.Length,
                    FileType = fileDto.FileType
                };
                versionFiles.Add(versionFile);

                // TODO: Store file content using storage service
            }
            version.Files = versionFiles;

            var createdVersion = await _unitOfWork.Versions.CreateAsync(version, cancellationToken);

            return _mapper.Map<VersionDto>(createdVersion);
        }

        public async Task<VersionDto> UpdateAsync(string id, VersionUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var existingVersion = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (existingVersion == null)
            {
                throw new KeyNotFoundException($"Version with ID {id} not found.");
            }

            _mapper.Map(dto, existingVersion);

            var success = await _unitOfWork.Versions.UpdateAsync(objectId, existingVersion, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update version with ID {id}.");
            }

            return _mapper.Map<VersionDto>(existingVersion);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var version = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {id} not found.");
            }

            // Check if version can be deleted (not deployed, not current version, etc.)
            var canDelete = await CanDeleteVersionAsync(objectId, cancellationToken);
            if (!canDelete)
            {
                throw new InvalidOperationException("Version cannot be deleted. It may be the current version or deployed.");
            }

            return await _unitOfWork.Versions.DeleteAsync(objectId, cancellationToken);
        }

        #endregion

        #region Program-specific Version Operations

        public async Task<PagedResponse<VersionListDto>> GetByProgramIdAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var versions = await _unitOfWork.Versions.GetByProgramIdAsync(programObjectId, cancellationToken);
            var versionsList = versions.ToList();

            // Apply pagination
            var totalCount = versionsList.Count;
            var paginatedVersions = versionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<VersionListDto>>(paginatedVersions);
            await EnrichVersionListAsync(dtos, cancellationToken);

            return new PagedResponse<VersionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<VersionDto> GetLatestVersionForProgramAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var version = await _unitOfWork.Versions.GetLatestVersionForProgramAsync(programObjectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"No versions found for program {programId}.");
            }

            return _mapper.Map<VersionDto>(version);
        }

        public async Task<VersionDto> GetByProgramIdAndVersionNumberAsync(string programId, int versionNumber, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var version = await _unitOfWork.Versions.GetByProgramIdAndVersionNumberAsync(programObjectId, versionNumber, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version {versionNumber} not found for program {programId}.");
            }

            return _mapper.Map<VersionDto>(version);
        }

        public async Task<int> GetNextVersionNumberAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            return await _unitOfWork.Versions.GetNextVersionNumberAsync(programObjectId, cancellationToken);
        }

        #endregion

        #region User-specific Operations

        public async Task<PagedResponse<VersionListDto>> GetByCreatorAsync(string creatorId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var versions = await _unitOfWork.Versions.GetByCreatorAsync(creatorId, cancellationToken);
            var versionsList = versions.ToList();

            // Apply pagination
            var totalCount = versionsList.Count;
            var paginatedVersions = versionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<VersionListDto>>(paginatedVersions);
            await EnrichVersionListAsync(dtos, cancellationToken);

            return new PagedResponse<VersionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<VersionListDto>> GetByReviewerAsync(string reviewerId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var versions = await _unitOfWork.Versions.GetByReviewerAsync(reviewerId, cancellationToken);
            var versionsList = versions.ToList();

            // Apply pagination
            var totalCount = versionsList.Count;
            var paginatedVersions = versionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<VersionListDto>>(paginatedVersions);
            await EnrichVersionListAsync(dtos, cancellationToken);

            return new PagedResponse<VersionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        #endregion

        #region Status and Review Management

        public async Task<PagedResponse<VersionListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var versions = await _unitOfWork.Versions.GetByStatusAsync(status, cancellationToken);
            var versionsList = versions.ToList();

            // Apply pagination
            var totalCount = versionsList.Count;
            var paginatedVersions = versionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<VersionListDto>>(paginatedVersions);
            await EnrichVersionListAsync(dtos, cancellationToken);

            return new PagedResponse<VersionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<VersionListDto>> GetPendingReviewsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var versions = await _unitOfWork.Versions.GetPendingReviewsAsync(cancellationToken);
            var versionsList = versions.ToList();

            // Apply pagination
            var totalCount = versionsList.Count;
            var paginatedVersions = versionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<VersionListDto>>(paginatedVersions);
            await EnrichVersionListAsync(dtos, cancellationToken);

            return new PagedResponse<VersionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<bool> UpdateStatusAsync(string id, VersionStatusUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var version = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {id} not found.");
            }

            // TODO: Get reviewer from current context
            var reviewerId = "current-user-id"; // This would come from authentication context

            return await _unitOfWork.Versions.UpdateStatusAsync(objectId, dto.Status, reviewerId, dto.Comments, cancellationToken);
        }

        public async Task<VersionReviewDto> SubmitReviewAsync(string id, VersionReviewSubmissionDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var version = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {id} not found.");
            }

            // TODO: Get reviewer from current context
            var reviewerId = "current-user-id"; // This would come from authentication context

            var success = await _unitOfWork.Versions.UpdateStatusAsync(objectId, dto.Status, reviewerId, dto.Comments, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException("Failed to submit review.");
            }

            // If approved, update program's current version
            if (dto.Status == "approved")
            {
                await _unitOfWork.Programs.UpdateCurrentVersionAsync(version.ProgramId, id, cancellationToken);
            }

            return new VersionReviewDto
            {
                Id = Guid.NewGuid().ToString(),
                VersionId = id,
                Status = dto.Status,
                Comments = dto.Comments,
                ReviewedBy = reviewerId,
                ReviewedByName = "Current User", // Would fetch from user service
                ReviewedAt = DateTime.UtcNow
            };
        }

        #endregion

        #region File Management within Versions

        public async Task<bool> AddFileAsync(string versionId, VersionFileCreateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(versionId);
            var version = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            var versionFile = new VersionFile
            {
                Path = dto.Path,
                StorageKey = GenerateStorageKey(objectId, dto.Path),
                Hash = ComputeFileHash(dto.Content),
                Size = dto.Content.Length,
                FileType = dto.FileType
            };

            // TODO: Store file content using storage service

            return await _unitOfWork.Versions.AddFileAsync(objectId, versionFile, cancellationToken);
        }

        public async Task<bool> UpdateFileAsync(string versionId, string filePath, VersionFileUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(versionId);
            var version = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            var existingFile = await _unitOfWork.Versions.GetFileByPathAsync(objectId, filePath, cancellationToken);
            if (existingFile == null)
            {
                throw new KeyNotFoundException($"File {filePath} not found in version {versionId}.");
            }

            var updatedFile = new VersionFile
            {
                Path = filePath,
                StorageKey = existingFile.StorageKey,
                Hash = ComputeFileHash(dto.Content),
                Size = dto.Content.Length,
                FileType = dto.FileType ?? existingFile.FileType
            };

            // TODO: Update file content using storage service

            return await _unitOfWork.Versions.UpdateFileAsync(objectId, filePath, updatedFile, cancellationToken);
        }

        public async Task<bool> RemoveFileAsync(string versionId, string filePath, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(versionId);
            var version = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            // TODO: Delete file from storage service

            return await _unitOfWork.Versions.RemoveFileAsync(objectId, filePath, cancellationToken);
        }

        public async Task<List<VersionFileDto>> GetFilesByVersionIdAsync(string versionId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(versionId);
            var files = await _unitOfWork.Versions.GetFilesByVersionIdAsync(objectId, cancellationToken);

            return _mapper.Map<List<VersionFileDto>>(files);
        }

        public async Task<VersionFileDetailDto> GetFileByPathAsync(string versionId, string filePath, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(versionId);
            var file = await _unitOfWork.Versions.GetFileByPathAsync(objectId, filePath, cancellationToken);

            if (file == null)
            {
                throw new KeyNotFoundException($"File {filePath} not found in version {versionId}.");
            }

            var dto = _mapper.Map<VersionFileDetailDto>(file);

            // TODO: Load file content from storage service
            // dto.Content = await storageService.GetFileContentAsync(file.StorageKey);

            return dto;
        }

        #endregion

        #region Version Comparison and Diff

        public async Task<VersionDiffDto> GetDiffBetweenVersionsAsync(string fromVersionId, string toVersionId, CancellationToken cancellationToken = default)
        {
            var fromObjectId = ParseObjectId(fromVersionId);
            var toObjectId = ParseObjectId(toVersionId);

            var fromVersion = await _unitOfWork.Versions.GetByIdAsync(fromObjectId, cancellationToken);
            var toVersion = await _unitOfWork.Versions.GetByIdAsync(toObjectId, cancellationToken);

            if (fromVersion == null)
            {
                throw new KeyNotFoundException($"From version {fromVersionId} not found.");
            }

            if (toVersion == null)
            {
                throw new KeyNotFoundException($"To version {toVersionId} not found.");
            }

            return await ComputeVersionDiffAsync(fromVersion, toVersion, cancellationToken);
        }

        public async Task<VersionDiffDto> GetDiffFromPreviousAsync(string versionId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(versionId);
            var version = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version {versionId} not found.");
            }

            // Get previous version
            var previousVersion = await _unitOfWork.Versions.GetByProgramIdAndVersionNumberAsync(
                version.ProgramId,
                version.VersionNumber - 1,
                cancellationToken);

            if (previousVersion == null)
            {
                throw new KeyNotFoundException($"No previous version found for version {versionId}.");
            }

            return await ComputeVersionDiffAsync(previousVersion, version, cancellationToken);
        }

        public async Task<List<VersionChangeDto>> GetChangeSummaryAsync(string versionId, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(versionId);
            var version = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version {versionId} not found.");
            }

            // TODO: Implement change summary logic
            var changes = new List<VersionChangeDto>();

            foreach (var file in version.Files)
            {
                changes.Add(new VersionChangeDto
                {
                    Path = file.Path,
                    Action = "modified", // This would be computed based on diff
                    Description = $"File {file.Path} was modified",
                    ImpactLevel = 2 // This would be computed based on file type and changes
                });
            }

            return changes;
        }

        #endregion

        #region Version Deployment Operations

        public async Task<VersionDeploymentDto> DeployVersionAsync(string versionId, VersionDeploymentRequestDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(versionId);
            var version = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version {versionId} not found.");
            }

            // TODO: Implement deployment logic

            return new VersionDeploymentDto
            {
                Id = Guid.NewGuid().ToString(),
                VersionId = versionId,
                Status = "deployed",
                DeployedAt = DateTime.UtcNow,
                DeployedBy = "current-user-id", // Would come from auth context
                TargetEnvironments = dto.TargetEnvironments,
                Configuration = dto.DeploymentConfiguration
            };
        }

        public async Task<bool> RevertToPreviousVersionAsync(string programId, string versionId, CancellationToken cancellationToken = default)
        {
            var versionObjectId = ParseObjectId(versionId);
            var version = await _unitOfWork.Versions.GetByIdAsync(versionObjectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version {versionId} not found.");
            }

            var previousVersion = await _unitOfWork.Versions.GetByProgramIdAndVersionNumberAsync(
                version.ProgramId,
                version.VersionNumber - 1,
                cancellationToken);

            if (previousVersion == null)
            {
                throw new KeyNotFoundException("No previous version found to revert to.");
            }

            var programObjectId = ParseObjectId(programId);
            return await _unitOfWork.Programs.UpdateCurrentVersionAsync(programObjectId, previousVersion._ID.ToString(), cancellationToken);
        }

        public async Task<bool> SetAsCurrentVersionAsync(string programId, string versionId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var versionObjectId = ParseObjectId(versionId);

            // Verify version exists and belongs to program
            var version = await _unitOfWork.Versions.GetByIdAsync(versionObjectId, cancellationToken);
            if (version == null || version.ProgramId != programObjectId)
            {
                throw new KeyNotFoundException($"Version {versionId} not found for program {programId}.");
            }

            return await _unitOfWork.Programs.UpdateCurrentVersionAsync(programObjectId, versionId, cancellationToken);
        }

        #endregion

        #region Version Statistics

        public async Task<VersionStatsDto> GetVersionStatsAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var versions = await _unitOfWork.Versions.GetByProgramIdAsync(programObjectId, cancellationToken);
            var versionsList = versions.ToList();

            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);
            var currentVersionId = program?.CurrentVersion;

            return new VersionStatsDto
            {
                TotalFiles = versionsList.Sum(v => v.Files.Count),
                TotalSize = versionsList.Sum(v => v.Files.Sum(f => f.Size)),
                FileTypeCount = ComputeFileTypeCount(versionsList),
                ExecutionCount = 0, // Would be computed from executions
                IsCurrentVersion = false // Would be set based on version
            };
        }

        public async Task<List<VersionActivityDto>> GetVersionActivityAsync(string programId, int days = 30, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var versions = await _unitOfWork.Versions.GetByProgramIdAsync(programObjectId, cancellationToken);

            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var recentVersions = versions.Where(v => v.CreatedAt >= cutoffDate);

            var activities = new List<VersionActivityDto>();

            foreach (var version in recentVersions)
            {
                activities.Add(new VersionActivityDto
                {
                    Date = version.CreatedAt,
                    Activity = "Version Created",
                    UserId = version.CreatedBy,
                    UserName = "User Name", // Would fetch from user service
                    Description = $"Version {version.VersionNumber} created: {version.CommitMessage}"
                });

                if (version.ReviewedAt.HasValue)
                {
                    activities.Add(new VersionActivityDto
                    {
                        Date = version.ReviewedAt.Value,
                        Activity = "Version Reviewed",
                        UserId = version.Reviewer ?? string.Empty,
                        UserName = "Reviewer Name", // Would fetch from user service
                        Description = $"Version {version.VersionNumber} {version.Status}"
                    });
                }
            }

            return activities.OrderByDescending(a => a.Date).ToList();
        }

        #endregion

        #region Commit Operations

        public async Task<VersionDto> CommitChangesAsync(string programId, VersionCommitDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            // Verify program exists
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);
            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Get next version number
            var nextVersionNumber = await _unitOfWork.Versions.GetNextVersionNumberAsync(programObjectId, cancellationToken);

            var version = new Version
            {
                ProgramId = programObjectId,
                VersionNumber = nextVersionNumber,
                CommitMessage = dto.CommitMessage,
                CreatedAt = DateTime.UtcNow,
                Status = "pending",
                CreatedBy = "current-user-id", // Would come from auth context
                Files = new List<VersionFile>()
            };

            // Process file changes
            foreach (var change in dto.Changes)
            {
                if (change.Action == "delete")
                    continue; // Skip deleted files

                if (change.Content == null)
                    throw new InvalidOperationException($"Content is required for file action '{change.Action}' on {change.Path}");

                var versionFile = new VersionFile
                {
                    Path = change.Path,
                    StorageKey = GenerateStorageKey(version._ID, change.Path),
                    Hash = ComputeFileHash(change.Content),
                    Size = change.Content.Length,
                    FileType = DetermineFileType(change.Path, change.ContentType)
                };

                version.Files.Add(versionFile);

                // TODO: Store file content using storage service
            }

            var createdVersion = await _unitOfWork.Versions.CreateAsync(version, cancellationToken);

            return _mapper.Map<VersionDto>(createdVersion);
        }

        public async Task<bool> ValidateCommitAsync(string programId, VersionCommitValidationDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            // Verify program exists
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);
            if (program == null)
            {
                return false;
            }

            // Validate changes
            foreach (var change in dto.Changes)
            {
                // Validate file paths
                if (string.IsNullOrEmpty(change.Path) || change.Path.Contains(".."))
                {
                    return false;
                }

                // Validate actions
                if (!new[] { "add", "modify", "delete" }.Contains(change.Action))
                {
                    return false;
                }

                // Validate content for non-delete actions
                if (change.Action != "delete" && change.Content == null)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Private Helper Methods

        private async Task EnrichVersionDetailAsync(VersionDetailDto dto, Version version, CancellationToken cancellationToken)
        {
            // Enrich with program name
            var program = await _unitOfWork.Programs.GetByIdAsync(version.ProgramId, cancellationToken);
            dto.ProgramName = program?.Name ?? "Unknown Program";

            // TODO: Enrich with user names
            dto.CreatedByName = "User Name"; // Would fetch from user service
            dto.ReviewerName = string.IsNullOrEmpty(version.Reviewer) ? null : "Reviewer Name";

            // Enrich with files
            dto.Files = _mapper.Map<List<VersionFileDto>>(version.Files);

            // Enrich with stats
            dto.Stats = new VersionStatsDto
            {
                TotalFiles = version.Files.Count,
                TotalSize = version.Files.Sum(f => f.Size),
                FileTypeCount = ComputeFileTypeCount(new[] { version }),
                ExecutionCount = 0, // Would be computed from executions
                IsCurrentVersion = program?.CurrentVersion == version._ID.ToString()
            };
        }

        private async Task EnrichVersionListAsync(List<VersionListDto> dtos, CancellationToken cancellationToken)
        {
            foreach (var dto in dtos)
            {
                // TODO: Add program name, user name resolution, etc.
                dto.ProgramName = "Program Name"; // Would fetch from program service
                dto.CreatedByName = "User Name"; // Would fetch from user service
                dto.ReviewerName = string.IsNullOrEmpty(dto.Reviewer) ? null : "Reviewer Name";
                dto.FileCount = 0; // Would be set from actual file count
                dto.IsCurrent = false; // Would be determined from program's current version
                await Task.CompletedTask; // Placeholder
            }
        }

        private async Task<VersionDiffDto> ComputeVersionDiffAsync(Version fromVersion, Version toVersion, CancellationToken cancellationToken)
        {
            var changes = new List<VersionFileChangeSummaryDto>();
            var stats = new VersionDiffStatsDto();

            // Create lookup for from version files
            var fromFiles = fromVersion.Files.ToDictionary(f => f.Path, f => f);
            var toFiles = toVersion.Files.ToDictionary(f => f.Path, f => f);

            // Find all unique file paths
            var allPaths = fromFiles.Keys.Union(toFiles.Keys).ToHashSet();

            foreach (var path in allPaths)
            {
                var fromFile = fromFiles.GetValueOrDefault(path);
                var toFile = toFiles.GetValueOrDefault(path);

                var change = new VersionFileChangeSummaryDto { Path = path };

                if (fromFile == null && toFile != null)
                {
                    // File added
                    change.Action = "added";
                    change.SizeAfter = toFile.Size;
                    stats.FilesAdded++;
                }
                else if (fromFile != null && toFile == null)
                {
                    // File deleted
                    change.Action = "deleted";
                    change.SizeBefore = fromFile.Size;
                    stats.FilesDeleted++;
                }
                else if (fromFile != null && toFile != null && fromFile.Hash != toFile.Hash)
                {
                    // File modified
                    change.Action = "modified";
                    change.SizeBefore = fromFile.Size;
                    change.SizeAfter = toFile.Size;
                    stats.FilesChanged++;
                }

                if (change.Action != null)
                {
                    changes.Add(change);
                }
            }

            return new VersionDiffDto
            {
                FromVersionId = fromVersion._ID.ToString(),
                ToVersionId = toVersion._ID.ToString(),
                FromVersionNumber = fromVersion.VersionNumber,
                ToVersionNumber = toVersion.VersionNumber,
                Changes = changes,
                Stats = stats
            };
        }

        private async Task<bool> CanDeleteVersionAsync(ObjectId versionId, CancellationToken cancellationToken)
        {
            // TODO: Check if version is deployed, is current version, has executions, etc.
            await Task.CompletedTask; // Placeholder
            return true;
        }

        private static string GenerateStorageKey(ObjectId versionId, string filePath)
        {
            return $"versions/{versionId}/{filePath}";
        }

        private static string ComputeFileHash(byte[] content)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(content);
            return Convert.ToBase64String(hash);
        }

        private static Dictionary<string, int> ComputeFileTypeCount(IEnumerable<Version> versions)
        {
            return versions
                .SelectMany(v => v.Files)
                .GroupBy(f => f.FileType)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        private static string DetermineFileType(string filePath, string? contentType)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".cs" or ".py" or ".js" or ".ts" or ".cpp" or ".h" or ".java" or ".rs" => "source",
                ".json" or ".xml" or ".yaml" or ".yml" or ".config" => "config",
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".ico" => "asset",
                ".css" or ".scss" or ".less" => "style",
                ".html" or ".htm" => "markup",
                ".md" or ".txt" or ".readme" => "documentation",
                _ => "source"
            };
        }

        #endregion
    }
}