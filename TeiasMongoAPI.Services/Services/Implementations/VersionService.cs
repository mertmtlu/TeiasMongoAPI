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
        private readonly IFileStorageService _fileStorageService;
        private readonly IProgramService _programService;

        public VersionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IFileStorageService fileStorageService,
            IProgramService programService,
            ILogger<VersionService> logger)
            : base(unitOfWork, mapper, logger)
        {
            _fileStorageService = fileStorageService;
            _programService = programService;
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

            // Get program details
            var program = await _unitOfWork.Programs.GetByIdAsync(version.ProgramId, cancellationToken);
            if (program != null)
            {
                dto.ProgramName = program.Name;

                // Check if this is the current version
                dto.Stats.IsCurrentVersion = program.CurrentVersion == id;
            }

            // Get creator name
            try
            {
                var creator = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(version.CreatedBy), cancellationToken);
                if (creator != null)
                {
                    dto.CreatedByName = creator.FullName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get creator details for version {VersionId}", id);
            }

            // Get reviewer name
            if (!string.IsNullOrEmpty(version.Reviewer))
            {
                try
                {
                    // Handle special case where reviewer is "system" (not a valid ObjectId)
                    if (string.Equals(version.Reviewer, "system", StringComparison.OrdinalIgnoreCase))
                    {
                        dto.ReviewerName = "System";
                    }
                    else
                    {
                        var reviewer = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(version.Reviewer), cancellationToken);
                        if (reviewer != null)
                        {
                            dto.ReviewerName = reviewer.FullName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get reviewer details for version {VersionId}", id);
                }
            }

            // Get version files using IFileStorageService
            dto.Files = await GetVersionFilesAsync(id, version.ProgramId.ToString(), cancellationToken);

            // Get version statistics
            await PopulateVersionStatsAsync(dto, version, cancellationToken);

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

            var dtos = await MapVersionListDtosAsync(paginatedVersions, cancellationToken);

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

            var dtos = await MapVersionListDtosAsync(paginatedVersions, cancellationToken);

            return new PagedResponse<VersionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<VersionDto> CreateAsync(VersionCreateDto dto, ObjectId? objectId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(dto.ProgramId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {dto.ProgramId} not found.");
            }

            // Get next version number
            var nextVersionNumber = await _unitOfWork.Versions.GetNextVersionNumberAsync(programObjectId, cancellationToken);

            string createdBy = "system"; // Should come from current user context BaseController holds CurrentUserId property

            if (objectId is ObjectId userId)
            {
                createdBy = userId.ToString();
            }

            var version = new Version
            {
                ProgramId = programObjectId,
                VersionNumber = nextVersionNumber,
                CommitMessage = dto.CommitMessage,
                CreatedBy = createdBy, // This should come from current user context
                CreatedAt = DateTime.UtcNow,
                Status = "pending",
                Files = new List<VersionFile>()
            };

            var currentVersion = await _unitOfWork.Versions.GetLatestVersionForProgramAsync(programObjectId, cancellationToken);
            var createdVersion = await _unitOfWork.Versions.CreateAsync(version, cancellationToken);

            if (currentVersion is not null)
                await _fileStorageService.CopyVersionFilesAsync(dto.ProgramId, currentVersion._ID.ToString(), createdVersion._ID.ToString(), cancellationToken);

            // Store files if provided using IFileStorageService
            if (dto.Files.Any())
            {
                await StoreVersionFilesAsync(createdVersion._ID.ToString(), dto.ProgramId, dto.Files, cancellationToken);
            }

            _logger.LogInformation("Created version {VersionNumber} for program {ProgramId} with ID {VersionId}",
                nextVersionNumber, dto.ProgramId, createdVersion._ID);

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

            // Only allow updating if version is still pending
            if (existingVersion.Status != "pending")
            {
                throw new InvalidOperationException("Cannot update version that has been reviewed.");
            }

            _mapper.Map(dto, existingVersion);

            var success = await _unitOfWork.Versions.UpdateAsync(objectId, existingVersion, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update version with ID {id}.");
            }

            _logger.LogInformation("Updated version {VersionId}", id);

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

            // Only allow deleting if version is pending and not the current version
            if (version.Status == "approved")
            {
                throw new InvalidOperationException("Cannot delete version that has been approved.");
            }

            var program = await _unitOfWork.Programs.GetByIdAsync(version.ProgramId, cancellationToken);
            if (program?.CurrentVersion == id)
            {
                throw new InvalidOperationException("Cannot delete the current version of a program.");
            }

            // Delete associated files using IFileStorageService
            try
            {
                await _fileStorageService.DeleteVersionFilesAsync(version.ProgramId.ToString(), id, cancellationToken);
                _logger.LogInformation("Deleted files for version {VersionId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete some files for version {VersionId}", id);
            }

            var success = await _unitOfWork.Versions.DeleteAsync(objectId, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Deleted version {VersionId}", id);
            }

            return success;
        }

        #endregion

        #region Program-specific Version Operations

        public async Task<PagedResponse<VersionListDto>> GetByProgramIdAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var versions = await _unitOfWork.Versions.GetByProgramIdAsync(programObjectId, cancellationToken);
            var versionsList = versions.ToList();

            // Apply pagination
            var totalCount = versionsList.Count;
            var paginatedVersions = versionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = await MapVersionListDtosAsync(paginatedVersions, cancellationToken);

            return new PagedResponse<VersionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<VersionDto> GetLatestVersionForProgramAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

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

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

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

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            return await _unitOfWork.Versions.GetNextVersionNumberAsync(programObjectId, cancellationToken);
        }

        #endregion

        #region User-specific Operations

        public async Task<PagedResponse<VersionListDto>> GetByCreatorAsync(string creatorId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var versions = await _unitOfWork.Versions.GetByCreatorAsync(creatorId, cancellationToken);
            return await CreatePagedVersionResponse(versions, pagination, cancellationToken);
        }

        public async Task<PagedResponse<VersionListDto>> GetByReviewerAsync(string reviewerId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var versions = await _unitOfWork.Versions.GetByReviewerAsync(reviewerId, cancellationToken);
            return await CreatePagedVersionResponse(versions, pagination, cancellationToken);
        }

        #endregion

        #region Status and Review Management

        public async Task<PagedResponse<VersionListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var versions = await _unitOfWork.Versions.GetByStatusAsync(status, cancellationToken);
            return await CreatePagedVersionResponse(versions, pagination, cancellationToken);
        }

        public async Task<PagedResponse<VersionListDto>> GetPendingReviewsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var versions = await _unitOfWork.Versions.GetPendingReviewsAsync(cancellationToken);
            return await CreatePagedVersionResponse(versions, pagination, cancellationToken);
        }

        public async Task<bool> UpdateStatusAsync(string id, VersionStatusUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var version = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {id} not found.");
            }

            var success = await _unitOfWork.Versions.UpdateStatusAsync(objectId,
                dto.Status,
                "system", // Should come from current user context BaseController holds CurrentUserId property
                dto.Comments,
                cancellationToken);

            if (success)
            {
                _logger.LogInformation("Updated status of version {VersionId} to {Status}", id, dto.Status);
            }

            return success;
        }

        public async Task<VersionReviewDto> SubmitReviewAsync(string id, VersionReviewSubmissionDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var version = await _unitOfWork.Versions.GetByIdAsync(objectId, cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {id} not found.");
            }

            if (version.Status != "pending")
            {
                throw new InvalidOperationException("Version is not pending review.");
            }

            var success = await _unitOfWork.Versions.UpdateStatusAsync(objectId,
                dto.Status, 
                "system", // Should come from current user context BaseController holds CurrentUserId property
                dto.Comments, 
                cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to submit review for version {id}.");
            }

            // If approved, optionally set as current version
            if (dto.Status == "approved")
            {
                try
                {
                    await _programService.UpdateCurrentVersionAsync(version.ProgramId.ToString(), id, cancellationToken);
                    _logger.LogInformation("Set version {VersionId} as current version for program {ProgramId}", id, version.ProgramId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set version {VersionId} as current version", id);
                }
            }

            _logger.LogInformation("Submitted review for version {VersionId} with status {Status}", id, dto.Status);

            return new VersionReviewDto
            {
                Id = Guid.NewGuid().ToString(),
                VersionId = id,
                Status = dto.Status,
                Comments = dto.Comments,
                ReviewedBy = "system",// Should come from current user context BaseController holds CurrentUserId property
                ReviewedByName = "System",
                ReviewedAt = DateTime.UtcNow
            };
        }

        #endregion

        #region Version Comparison and Diff

        public async Task<VersionDiffDto> GetDiffBetweenVersionsAsync(string fromVersionId, string toVersionId, CancellationToken cancellationToken = default)
        {
            var fromVersion = await _unitOfWork.Versions.GetByIdAsync(ParseObjectId(fromVersionId), cancellationToken);
            var toVersion = await _unitOfWork.Versions.GetByIdAsync(ParseObjectId(toVersionId), cancellationToken);

            if (fromVersion == null)
            {
                throw new KeyNotFoundException($"Version with ID {fromVersionId} not found.");
            }

            if (toVersion == null)
            {
                throw new KeyNotFoundException($"Version with ID {toVersionId} not found.");
            }

            if (fromVersion.ProgramId != toVersion.ProgramId)
            {
                throw new InvalidOperationException("Cannot compare versions from different programs.");
            }

            // Get files for both versions using IFileStorageService
            var programId = fromVersion.ProgramId.ToString();
            var fromFiles = await _fileStorageService.ListVersionFilesAsync(programId, fromVersionId, cancellationToken);
            var toFiles = await _fileStorageService.ListVersionFilesAsync(programId, toVersionId, cancellationToken);

            var changes = CalculateFileChanges(fromFiles, toFiles);

            return new VersionDiffDto
            {
                FromVersionId = fromVersionId,
                ToVersionId = toVersionId,
                FromVersionNumber = fromVersion.VersionNumber,
                ToVersionNumber = toVersion.VersionNumber,
                Changes = changes,
                Stats = new VersionDiffStatsDto
                {
                    FilesChanged = changes.Count(c => c.Action == "modified"),
                    FilesAdded = changes.Count(c => c.Action == "added"),
                    FilesDeleted = changes.Count(c => c.Action == "deleted"),
                    TotalLinesAdded = changes.Sum(c => c.LinesAdded),
                    TotalLinesRemoved = changes.Sum(c => c.LinesRemoved)
                }
            };
        }

        public async Task<VersionDiffDto> GetDiffFromPreviousAsync(string versionId, CancellationToken cancellationToken = default)
        {
            var version = await _unitOfWork.Versions.GetByIdAsync(ParseObjectId(versionId), cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            if (version.VersionNumber <= 1)
            {
                throw new InvalidOperationException("Cannot get diff for the first version.");
            }

            var previousVersion = await _unitOfWork.Versions.GetByProgramIdAndVersionNumberAsync(
                version.ProgramId, version.VersionNumber - 1, cancellationToken);

            if (previousVersion == null)
            {
                throw new KeyNotFoundException($"Previous version not found for version {versionId}.");
            }

            return await GetDiffBetweenVersionsAsync(previousVersion._ID.ToString(), versionId, cancellationToken);
        }

        public async Task<List<VersionChangeDto>> GetChangeSummaryAsync(string versionId, CancellationToken cancellationToken = default)
        {
            try
            {
                var diff = await GetDiffFromPreviousAsync(versionId, cancellationToken);

                return diff.Changes.Select(change => new VersionChangeDto
                {
                    Path = change.Path,
                    Action = change.Action,
                    Description = GenerateChangeDescription(change),
                    ImpactLevel = CalculateImpactLevel(change)
                }).ToList();
            }
            catch (InvalidOperationException)
            {
                // First version, no changes
                return new List<VersionChangeDto>();
            }
        }

        #endregion

        #region Version Deployment Operations

        public async Task<VersionDeploymentDto> DeployVersionAsync(string versionId, VersionDeploymentRequestDto dto, CancellationToken cancellationToken = default)
        {
            var version = await _unitOfWork.Versions.GetByIdAsync(ParseObjectId(versionId), cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            if (version.Status != "approved")
            {
                throw new InvalidOperationException("Can only deploy approved versions.");
            }

            // Set as current version if requested
            if (dto.SetAsCurrent)
            {
                await _programService.UpdateCurrentVersionAsync(version.ProgramId.ToString(), versionId, cancellationToken);
            }

            _logger.LogInformation("Deployed version {VersionId} for program {ProgramId}", versionId, version.ProgramId);

            return new VersionDeploymentDto
            {
                Id = Guid.NewGuid().ToString(),
                VersionId = versionId,
                Status = "deployed",
                DeployedAt = DateTime.UtcNow,
                DeployedBy = "system",// Should come from current user context BaseController holds CurrentUserId property
                TargetEnvironments = dto.TargetEnvironments,
                Configuration = dto.DeploymentConfiguration
            };
        }

        public async Task<bool> RevertToPreviousVersionAsync(string programId, string versionId, CancellationToken cancellationToken = default)
        {
            var version = await _unitOfWork.Versions.GetByIdAsync(ParseObjectId(versionId), cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            if (version.VersionNumber <= 1)
            {
                throw new InvalidOperationException("Cannot revert from the first version.");
            }

            var previousVersion = await _unitOfWork.Versions.GetByProgramIdAndVersionNumberAsync(
                version.ProgramId, version.VersionNumber - 1, cancellationToken);

            if (previousVersion == null)
            {
                throw new KeyNotFoundException("Previous version not found.");
            }

            var success = await _programService.UpdateCurrentVersionAsync(programId, previousVersion._ID.ToString(), cancellationToken);

            if (success)
            {
                _logger.LogInformation("Reverted program {ProgramId} from version {CurrentVersion} to {PreviousVersion}",
                    programId, version.VersionNumber, previousVersion.VersionNumber);
            }

            return success;
        }

        public async Task<bool> SetAsCurrentVersionAsync(string programId, string versionId, CancellationToken cancellationToken = default)
        {
            var version = await _unitOfWork.Versions.GetByIdAsync(ParseObjectId(versionId), cancellationToken);

            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            if (version.Status != "approved")
            {
                throw new InvalidOperationException("Can only set approved versions as current.");
            }

            return await _programService.UpdateCurrentVersionAsync(programId, versionId, cancellationToken);
        }

        #endregion

        #region Version Statistics

        public async Task<VersionStatsDto> GetVersionStatsAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var versions = await _unitOfWork.Versions.GetByProgramIdAsync(programObjectId, cancellationToken);
            var versionsList = versions.ToList();

            var executions = await _unitOfWork.Executions.GetByProgramIdAsync(programObjectId, cancellationToken);
            var executionsList = executions.ToList();

            // Aggregate file statistics across all versions using IFileStorageService
            var totalFiles = 0;
            var totalSize = 0L;
            var fileTypes = new Dictionary<string, int>();

            foreach (var version in versionsList)
            {
                try
                {
                    var files = await _fileStorageService.ListVersionFilesAsync(programId, version._ID.ToString(), cancellationToken);
                    totalFiles += files.Count;
                    totalSize += files.Sum(f => f.Size);

                    foreach (var file in files)
                    {
                        var extension = Path.GetExtension(file.Path).ToLowerInvariant();
                        if (string.IsNullOrEmpty(extension)) extension = "no-extension";
                        fileTypes[extension] = fileTypes.GetValueOrDefault(extension, 0) + 1;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get files for version {VersionId}", version._ID);
                }
            }

            return new VersionStatsDto
            {
                TotalFiles = totalFiles,
                TotalSize = totalSize,
                FileTypeCount = fileTypes,
                ExecutionCount = executionsList.Count,
                IsCurrentVersion = false // Would need program context to determine this
            };
        }

        public async Task<List<VersionActivityDto>> GetVersionActivityAsync(string programId, int days = 30, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var versions = await _unitOfWork.Versions.GetByProgramIdAsync(programObjectId, cancellationToken);

            var activities = versions
                .Where(v => v.CreatedAt >= cutoffDate)
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => new VersionActivityDto
                {
                    Date = v.CreatedAt,
                    Activity = "Version Created",
                    UserId = v.CreatedBy,
                    UserName = "Unknown", // Would need to resolve user names
                    Description = $"Version {v.VersionNumber}: {v.CommitMessage}"
                })
                .ToList();

            return activities;
        }

        #endregion

        #region Commit Operations

        public async Task<VersionDto> CommitChangesAsync(string programId, ObjectId? objectId, VersionCommitDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Create new version
            var nextVersionNumber = await _unitOfWork.Versions.GetNextVersionNumberAsync(programObjectId, cancellationToken);

            string createdBy = "system";// Should come from current user context BaseController holds CurrentUserId property

            if (objectId is ObjectId userId)
            {
                createdBy = userId.ToString();
            }

            var version = new Version
            {
                ProgramId = programObjectId,
                VersionNumber = nextVersionNumber,
                CommitMessage = dto.CommitMessage,
                CreatedBy = createdBy, // Should come from current user context
                CreatedAt = DateTime.UtcNow,
                Status = "pending",
                Files = new List<VersionFile>()
            };

            var currentVersion = await _unitOfWork.Versions.GetLatestVersionForProgramAsync(programObjectId, cancellationToken);
            var createdVersion = await _unitOfWork.Versions.CreateAsync(version, cancellationToken);
            var versionId = createdVersion._ID.ToString();

            if (currentVersion is not null)
                await _fileStorageService.CopyVersionFilesAsync(programId, currentVersion._ID.ToString(), versionId);

            // Process file changes using IFileStorageService
            var filesToStore = new List<VersionFileCreateDto>();

            foreach (var change in dto.Changes)
            {
                switch (change.Action.ToLowerInvariant())
                {
                    case "add":
                    case "modify":
                        if (change.Content != null)
                        {
                            filesToStore.Add(new VersionFileCreateDto
                            {
                                Path = change.Path,
                                Content = change.Content,
                                ContentType = change.ContentType ?? GetContentTypeFromPath(change.Path),
                                FileType = DetermineFileType(change.Path)
                            });
                        }
                        break;
                    case "delete":
                        // Deleted files are handled by not including them in the new version
                        // They still exist in previous versions
                        break;
                }
            }

            // Store all files for this version
            if (filesToStore.Any())
            {
                var results = await _fileStorageService.StoreFilesAsync(programId, versionId, filesToStore, cancellationToken);
                var failedFiles = results.Where(r => !r.Success).ToList();
                if (failedFiles.Any())
                {
                    var failedPaths = string.Join(", ", failedFiles.Select(f => f.FilePath));
                    _logger.LogWarning("Failed to store some files during commit: {FailedFiles}", failedPaths);
                }
            }

            _logger.LogInformation("Committed changes for program {ProgramId} as version {VersionNumber}",
                programId, nextVersionNumber);

            return _mapper.Map<VersionDto>(createdVersion);
        }

        public async Task<bool> ValidateCommitAsync(string programId, VersionCommitValidationDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Validate all file changes
            foreach (var change in dto.Changes)
            {
                if (change.Action.ToLowerInvariant() == "delete")
                    continue;

                if (change.Content == null)
                {
                    throw new InvalidOperationException($"Content is required for {change.Action} operation on {change.Path}");
                }

                // Validate file using storage service
                var validation = await _fileStorageService.ValidateFileAsync(
                    change.Path,
                    change.Content,
                    change.ContentType ?? GetContentTypeFromPath(change.Path),
                    cancellationToken);

                if (!validation.IsValid)
                {
                    throw new InvalidOperationException($"File {change.Path} is invalid: {string.Join(", ", validation.Errors)}");
                }
            }

            return true;
        }

        #endregion

        #region Private Helper Methods

        private async Task<List<VersionFileDto>> GetVersionFilesAsync(string versionId, string programId, CancellationToken cancellationToken)
        {
            try
            {
                return await _fileStorageService.ListVersionFilesAsync(programId, versionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get files for version {VersionId}", versionId);
                return new List<VersionFileDto>();
            }
        }

        private async Task<List<VersionListDto>> MapVersionListDtosAsync(List<Version> versions, CancellationToken cancellationToken)
        {
            var dtos = new List<VersionListDto>();

            foreach (var version in versions)
            {
                var dto = _mapper.Map<VersionListDto>(version);

                // Get program name
                try
                {
                    var program = await _unitOfWork.Programs.GetByIdAsync(version.ProgramId, cancellationToken);
                    if (program != null)
                    {
                        dto.ProgramName = program.Name;
                        dto.IsCurrent = program.CurrentVersion == version._ID.ToString();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get program details for version {VersionId}", version._ID);
                }

                // Get creator name
                try
                {
                    var creator = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(version.CreatedBy), cancellationToken);
                    if (creator != null)
                    {
                        dto.CreatedByName = creator.FullName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get creator details for version {VersionId}", version._ID);
                }

                // Get reviewer name
                if (!string.IsNullOrEmpty(version.Reviewer))
                {
                    try
                    {
                        // Handle special case where reviewer is "system" (not a valid ObjectId)
                        if (string.Equals(version.Reviewer, "system", StringComparison.OrdinalIgnoreCase))
                        {
                            dto.ReviewerName = "System";
                        }
                        else
                        {
                            var reviewer = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(version.Reviewer), cancellationToken);
                            if (reviewer != null)
                            {
                                dto.ReviewerName = reviewer.FullName;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get reviewer details for version {VersionId}", version._ID);
                    }
                }

                // Get file count using IFileStorageService
                try
                {
                    var files = await _fileStorageService.ListVersionFilesAsync(version.ProgramId.ToString(), version._ID.ToString(), cancellationToken);
                    dto.FileCount = files.Count;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get file count for version {VersionId}", version._ID);
                    dto.FileCount = 0;
                }

                dtos.Add(dto);
            }

            return dtos;
        }

        private async Task<PagedResponse<VersionListDto>> CreatePagedVersionResponse(IEnumerable<Version> versions, PaginationRequestDto pagination, CancellationToken cancellationToken)
        {
            var versionsList = versions.ToList();
            var totalCount = versionsList.Count;
            var paginatedVersions = versionsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = await MapVersionListDtosAsync(paginatedVersions, cancellationToken);
            return new PagedResponse<VersionListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        private async Task StoreVersionFilesAsync(string versionId, string programId, List<VersionFileCreateDto> files, CancellationToken cancellationToken)
        {
            var results = await _fileStorageService.StoreFilesAsync(programId, versionId, files, cancellationToken);
            var failedFiles = results.Where(r => !r.Success).ToList();

            if (failedFiles.Any())
            {
                var failedPaths = string.Join(", ", failedFiles.Select(f => f.FilePath));
                _logger.LogWarning("Failed to store some files for version {VersionId}: {FailedFiles}", versionId, failedPaths);
            }
        }

        private async Task PopulateVersionStatsAsync(VersionDetailDto dto, Version version, CancellationToken cancellationToken)
        {
            try
            {
                var executions = await _unitOfWork.Executions.GetByVersionIdAsync(version._ID, cancellationToken);
                var files = await _fileStorageService.ListVersionFilesAsync(version.ProgramId.ToString(), version._ID.ToString(), cancellationToken);

                dto.Stats = new VersionStatsDto
                {
                    TotalFiles = files.Count,
                    TotalSize = files.Sum(f => f.Size),
                    FileTypeCount = files
                        .GroupBy(f => Path.GetExtension(f.Path).ToLowerInvariant())
                        .ToDictionary(g => g.Key, g => g.Count()),
                    ExecutionCount = executions.Count(),
                    IsCurrentVersion = false // Set in GetByIdAsync
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to populate version stats for version {VersionId}", version._ID);
                dto.Stats = new VersionStatsDto();
            }
        }

        private List<VersionFileChangeSummaryDto> CalculateFileChanges(IEnumerable<VersionFileDto> fromFiles, IEnumerable<VersionFileDto> toFiles)
        {
            var changes = new List<VersionFileChangeSummaryDto>();
            var fromFileDict = fromFiles.ToDictionary(f => f.Path, f => f);
            var toFileDict = toFiles.ToDictionary(f => f.Path, f => f);

            // Find added files
            foreach (var toFile in toFiles.Where(f => !fromFileDict.ContainsKey(f.Path)))
            {
                changes.Add(new VersionFileChangeSummaryDto
                {
                    Path = toFile.Path,
                    Action = "added",
                    LinesAdded = EstimateLineCount(toFile.Size),
                    LinesRemoved = 0,
                    SizeBefore = 0,
                    SizeAfter = toFile.Size
                });
            }

            // Find deleted files
            foreach (var fromFile in fromFiles.Where(f => !toFileDict.ContainsKey(f.Path)))
            {
                changes.Add(new VersionFileChangeSummaryDto
                {
                    Path = fromFile.Path,
                    Action = "deleted",
                    LinesAdded = 0,
                    LinesRemoved = EstimateLineCount(fromFile.Size),
                    SizeBefore = fromFile.Size,
                    SizeAfter = 0
                });
            }

            // Find modified files
            foreach (var path in fromFileDict.Keys.Intersect(toFileDict.Keys))
            {
                var fromFile = fromFileDict[path];
                var toFile = toFileDict[path];

                if (fromFile.Hash != toFile.Hash)
                {
                    changes.Add(new VersionFileChangeSummaryDto
                    {
                        Path = path,
                        Action = "modified",
                        LinesAdded = Math.Max(0, EstimateLineCount(toFile.Size) - EstimateLineCount(fromFile.Size)),
                        LinesRemoved = Math.Max(0, EstimateLineCount(fromFile.Size) - EstimateLineCount(toFile.Size)),
                        SizeBefore = fromFile.Size,
                        SizeAfter = toFile.Size
                    });
                }
            }

            return changes;
        }

        private int EstimateLineCount(long fileSize)
        {
            // Rough estimate: average 50 characters per line
            return (int)(fileSize / 50);
        }

        private string GenerateChangeDescription(VersionFileChangeSummaryDto change)
        {
            return change.Action.ToLowerInvariant() switch
            {
                "added" => $"Added {change.Path} ({change.SizeAfter} bytes)",
                "deleted" => $"Deleted {change.Path} ({change.SizeBefore} bytes)",
                "modified" => $"Modified {change.Path} ({change.SizeBefore} → {change.SizeAfter} bytes)",
                _ => $"Changed {change.Path}"
            };
        }

        private int CalculateImpactLevel(VersionFileChangeSummaryDto change)
        {
            // Simple impact calculation based on file size and type
            var sizeImpact = Math.Max(change.SizeAfter, change.SizeBefore) switch
            {
                < 1000 => 1,
                < 10000 => 2,
                < 100000 => 3,
                < 1000000 => 4,
                _ => 5
            };

            var typeImpact = Path.GetExtension(change.Path).ToLowerInvariant() switch
            {
                ".cs" or ".py" or ".js" or ".ts" => 3, // Source code
                ".json" or ".xml" or ".config" => 4, // Configuration
                ".md" or ".txt" => 1, // Documentation
                _ => 2 // Other files
            };

            return Math.Min(5, Math.Max(1, (sizeImpact + typeImpact) / 2));
        }

        private string DetermineFileType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".cs" or ".py" or ".js" or ".ts" or ".java" or ".cpp" or ".c" or ".rs" => "source",
                ".json" or ".xml" or ".yaml" or ".yml" or ".config" => "config",
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" => "asset",
                ".exe" or ".dll" or ".so" or ".dylib" => "build_artifact",
                _ => "source"
            };
        }

        private string GetContentTypeFromPath(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".py" => "text/x-python",
                ".cs" => "text/x-csharp",
                ".cpp" => "text/x-c++src",
                ".c" => "text/x-csrc",
                ".java" => "text/x-java-source",
                ".rs" => "text/x-rust",
                ".md" => "text/markdown",
                _ => "application/octet-stream"
            };
        }

        #endregion
    }
}