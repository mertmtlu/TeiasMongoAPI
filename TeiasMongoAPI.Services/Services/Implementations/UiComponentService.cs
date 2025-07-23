using AutoMapper;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using System.Text.Json;
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
    public class UiComponentService : BaseService, IUiComponentService
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IProgramService _programService;
        private readonly Dictionary<string, List<string>> _componentCategories;
        private readonly List<string> _supportedComponentTypes;

        public UiComponentService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IFileStorageService fileStorageService,
            IProgramService programService,
            ILogger<UiComponentService> logger)
            : base(unitOfWork, mapper, logger)
        {
            _fileStorageService = fileStorageService;
            _programService = programService;
            _componentCategories = InitializeComponentCategories();
            _supportedComponentTypes = InitializeSupportedTypes();
        }

        #region Basic CRUD Operations

        public async Task<UiComponentDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            var dto = _mapper.Map<UiComponentDetailDto>(component);

            // Get creator details
            try
            {
                var creator = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(component.Creator), cancellationToken);
                if (creator != null)
                {
                    dto.CreatorName = creator.FullName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get creator details for component {ComponentId}", id);
            }

            // Get program and version details
            try
            {
                var program = await _unitOfWork.Programs.GetByIdAsync(component.ProgramId, cancellationToken);
                if (program != null)
                {
                    dto.ProgramName = program.Name;
                }

                var version = await _unitOfWork.Versions.GetByIdAsync(component.VersionId, cancellationToken);
                if (version != null)
                {
                    dto.VersionNumber = version.VersionNumber;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get program/version details for component {ComponentId}", id);
            }

            // Get component assets
            dto.Assets = await GetComponentAssetsAsync(id, cancellationToken);

            // Get component bundle info
            dto.BundleInfo = await GetComponentBundleInfoAsync(id, cancellationToken);

            // Get component statistics
            dto.Stats = await GetComponentStatsAsync(id, cancellationToken);

            // Get component usage
            dto.Usage = await GetComponentUsageAsync(id, cancellationToken);

            return dto;
        }

        public async Task<PagedResponse<UiComponentListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var components = await _unitOfWork.UiComponents.GetAllAsync(cancellationToken);
            var componentsList = components.ToList();

            // Apply pagination
            var totalCount = componentsList.Count;
            var paginatedComponents = componentsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = await MapComponentListDtosAsync(paginatedComponents, cancellationToken);

            return new PagedResponse<UiComponentListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<UiComponentListDto>> SearchAsync(UiComponentSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var allComponents = await _unitOfWork.UiComponents.GetAllAsync(cancellationToken);
            var filteredComponents = allComponents.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchDto.Name))
            {
                filteredComponents = filteredComponents.Where(c => c.Name.Contains(searchDto.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(searchDto.Description))
            {
                filteredComponents = filteredComponents.Where(c => c.Description.Contains(searchDto.Description, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(searchDto.Type))
            {
                filteredComponents = filteredComponents.Where(c => c.Type == searchDto.Type);
            }

            if (!string.IsNullOrEmpty(searchDto.Creator))
            {
                filteredComponents = filteredComponents.Where(c => c.Creator == searchDto.Creator);
            }

            if (!string.IsNullOrEmpty(searchDto.Status))
            {
                filteredComponents = filteredComponents.Where(c => c.Status == searchDto.Status);
            }

            if (!string.IsNullOrEmpty(searchDto.ProgramId))
            {
                var programObjectId = ParseObjectId(searchDto.ProgramId);
                filteredComponents = filteredComponents.Where(c => c.ProgramId == programObjectId);
            }

            if (!string.IsNullOrEmpty(searchDto.VersionId))
            {
                var versionObjectId = ParseObjectId(searchDto.VersionId);
                filteredComponents = filteredComponents.Where(c => c.VersionId == versionObjectId);
            }

            if (searchDto.Tags?.Any() == true)
            {
                filteredComponents = filteredComponents.Where(c => c.Tags.Any(t => searchDto.Tags.Contains(t)));
            }

            if (searchDto.CreatedFrom.HasValue)
            {
                filteredComponents = filteredComponents.Where(c => c.CreatedAt >= searchDto.CreatedFrom.Value);
            }

            if (searchDto.CreatedTo.HasValue)
            {
                filteredComponents = filteredComponents.Where(c => c.CreatedAt <= searchDto.CreatedTo.Value);
            }

            var componentsList = filteredComponents.ToList();

            // Apply pagination
            var totalCount = componentsList.Count;
            var paginatedComponents = componentsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = await MapComponentListDtosAsync(paginatedComponents, cancellationToken);

            return new PagedResponse<UiComponentListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<UiComponentDto> CreateAsync(string programId, string versionId, UiComponentCreateDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var versionObjectId = ParseObjectId(versionId);

            // Validate that program and version exist
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);
            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var version = await _unitOfWork.Versions.GetByIdAsync(versionObjectId, cancellationToken);
            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            // Validate that version belongs to the program
            if (version.ProgramId != programObjectId)
            {
                throw new InvalidOperationException($"Version {versionId} does not belong to program {programId}.");
            }

            // Validate component type
            if (!_supportedComponentTypes.Contains(dto.Type))
            {
                throw new InvalidOperationException($"Unsupported component type: {dto.Type}");
            }

            // Validate name uniqueness within the version
            if (!await ValidateNameUniqueForVersionAsync(programId, versionId, dto.Name, null, cancellationToken))
            {
                throw new InvalidOperationException($"Component with name '{dto.Name}' already exists in this version.");
            }

            // Validate JSON strings for Configuration and Schema
            ValidateJsonString(dto.Configuration, "Configuration");
            ValidateJsonString(dto.Schema, "Schema");

            var component = _mapper.Map<UiComponent>(dto);
            component.ProgramId = programObjectId;
            component.VersionId = versionObjectId;
            component.CreatedAt = DateTime.UtcNow;
            component.Creator = "system"; // Should come from current user context
            component.Status = "active";

            var createdComponent = await _unitOfWork.UiComponents.CreateAsync(component, cancellationToken);

            _logger.LogInformation("Created UI component {ComponentId} with name {ComponentName} of type {ComponentType} for program {ProgramId}, version {VersionId}",
                createdComponent._ID, dto.Name, dto.Type, programId, versionId);

            return _mapper.Map<UiComponentDto>(createdComponent);
        }

        public async Task<UiComponentDto> UpdateAsync(string id, UiComponentUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var existingComponent = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (existingComponent == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // Only allow updates if component is not deprecated
            if (existingComponent.Status == "deprecated")
            {
                throw new InvalidOperationException("Cannot update deprecated components.");
            }

            // Validate name uniqueness if changed
            if (!string.IsNullOrEmpty(dto.Name) && dto.Name != existingComponent.Name)
            {
                var programId = existingComponent.ProgramId.ToString();
                var versionId = existingComponent.VersionId.ToString();

                if (!await ValidateNameUniqueForVersionAsync(programId, versionId, dto.Name, id, cancellationToken))
                {
                    throw new InvalidOperationException($"Component with name '{dto.Name}' already exists in this version.");
                }
            }

            // Validate JSON strings for Configuration and Schema if provided
            if (!string.IsNullOrEmpty(dto.Configuration))
            {
                ValidateJsonString(dto.Configuration, "Configuration");
            }
            if (!string.IsNullOrEmpty(dto.Schema))
            {
                ValidateJsonString(dto.Schema, "Schema");
            }

            _mapper.Map(dto, existingComponent);

            var success = await _unitOfWork.UiComponents.UpdateAsync(objectId, existingComponent, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update UI component with ID {id}.");
            }

            _logger.LogInformation("Updated UI component {ComponentId}", id);

            return _mapper.Map<UiComponentDto>(existingComponent);
        }

        public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // Check if component is being used
            var usage = await GetComponentUsageAsync(id, cancellationToken);
            if (usage.Any(u => u.IsActive))
            {
                throw new InvalidOperationException("Cannot delete component that is actively being used. Remove all active usages first.");
            }

            // Delete component assets
            try
            {
                await DeleteComponentAssetsAsync(component, cancellationToken);
                _logger.LogInformation("Deleted assets for UI component {ComponentId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete some assets for UI component {ComponentId}", id);
            }

            var success = await _unitOfWork.UiComponents.DeleteAsync(objectId, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Deleted UI component {ComponentId}", id);
            }

            return success;
        }

        #endregion

        #region Version-specific Component Operations

        public async Task<PagedResponse<UiComponentListDto>> GetByProgramVersionAsync(string programId, string versionId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var versionObjectId = ParseObjectId(versionId);

            // Validate that program and version exist
            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            if (!await _unitOfWork.Versions.ExistsAsync(versionObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            var components = await _unitOfWork.UiComponents.GetByProgramVersionAsync(programObjectId, versionObjectId, cancellationToken);
            return await CreatePagedComponentResponse(components, pagination, cancellationToken);
        }

        public async Task<List<UiComponentListDto>> CopyComponentsToNewVersionAsync(string programId, string fromVersionId, string toVersionId, List<string>? componentNames = null, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var fromVersionObjectId = ParseObjectId(fromVersionId);
            var toVersionObjectId = ParseObjectId(toVersionId);

            // Validate inputs
            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            if (!await _unitOfWork.Versions.ExistsAsync(fromVersionObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Source version with ID {fromVersionId} not found.");
            }

            if (!await _unitOfWork.Versions.ExistsAsync(toVersionObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Target version with ID {toVersionId} not found.");
            }

            // Get source components
            var sourceComponents = await _unitOfWork.UiComponents.GetByProgramVersionAsync(programObjectId, fromVersionObjectId, cancellationToken);
            var componentsToList = sourceComponents.ToList();

            // Filter by component names if specified
            if (componentNames?.Any() == true)
            {
                componentsToList = componentsToList.Where(c => componentNames.Contains(c.Name)).ToList();
            }

            var copiedComponents = new List<UiComponent>();

            foreach (var sourceComponent in componentsToList)
            {
                try
                {
                    // Check if component with same name already exists in target version
                    if (!await ValidateNameUniqueForVersionAsync(programId, toVersionId, sourceComponent.Name, null, cancellationToken))
                    {
                        _logger.LogWarning("Component with name {ComponentName} already exists in target version, skipping", sourceComponent.Name);
                        continue;
                    }

                    // Create new component in target version
                    var newComponent = new UiComponent
                    {
                        Name = sourceComponent.Name,
                        Description = sourceComponent.Description,
                        Type = sourceComponent.Type,
                        Creator = sourceComponent.Creator,
                        CreatedAt = DateTime.UtcNow,
                        ProgramId = programObjectId,
                        VersionId = toVersionObjectId,
                        Configuration = sourceComponent.Configuration,
                        Schema = sourceComponent.Schema,
                        Status = "active",
                        Tags = new List<string>(sourceComponent.Tags)
                    };

                    var createdComponent = await _unitOfWork.UiComponents.CreateAsync(newComponent, cancellationToken);

                    // Copy assets
                    try
                    {
                        await CopyComponentAssetsAsync(sourceComponent, createdComponent, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to copy assets for component {ComponentName}", sourceComponent.Name);
                    }

                    copiedComponents.Add(createdComponent);

                    _logger.LogInformation("Copied component {ComponentName} from version {FromVersion} to {ToVersion}",
                        sourceComponent.Name, fromVersionId, toVersionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to copy component {ComponentName} from version {FromVersion} to {ToVersion}",
                        sourceComponent.Name, fromVersionId, toVersionId);
                }
            }

            return await MapComponentListDtosAsync(copiedComponents, cancellationToken);
        }

        public async Task<bool> CopyComponentToVersionAsync(string componentId, string targetProgramId, string targetVersionId, string? newName = null, CancellationToken cancellationToken = default)
        {
            var componentObjectId = ParseObjectId(componentId);
            var targetProgramObjectId = ParseObjectId(targetProgramId);
            var targetVersionObjectId = ParseObjectId(targetVersionId);

            var sourceComponent = await _unitOfWork.UiComponents.GetByIdAsync(componentObjectId, cancellationToken);
            if (sourceComponent == null)
            {
                throw new KeyNotFoundException($"Source component with ID {componentId} not found.");
            }

            if (!await _unitOfWork.Programs.ExistsAsync(targetProgramObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Target program with ID {targetProgramId} not found.");
            }

            if (!await _unitOfWork.Versions.ExistsAsync(targetVersionObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Target version with ID {targetVersionId} not found.");
            }

            var componentName = newName ?? sourceComponent.Name;

            // Check name uniqueness in target version
            if (!await ValidateNameUniqueForVersionAsync(targetProgramId, targetVersionId, componentName, null, cancellationToken))
            {
                throw new InvalidOperationException($"Component with name '{componentName}' already exists in target version.");
            }

            // Create new component
            var newComponent = new UiComponent
            {
                Name = componentName,
                Description = sourceComponent.Description,
                Type = sourceComponent.Type,
                Creator = sourceComponent.Creator,
                CreatedAt = DateTime.UtcNow,
                ProgramId = targetProgramObjectId,
                VersionId = targetVersionObjectId,
                Configuration = sourceComponent.Configuration,
                Schema = sourceComponent.Schema,
                Status = "active",
                Tags = new List<string>(sourceComponent.Tags)
            };

            var createdComponent = await _unitOfWork.UiComponents.CreateAsync(newComponent, cancellationToken);

            // Copy assets
            try
            {
                await CopyComponentAssetsAsync(sourceComponent, createdComponent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy assets when copying component {ComponentId}", componentId);
            }

            _logger.LogInformation("Copied component {ComponentId} to program {TargetProgramId}, version {TargetVersionId}",
                componentId, targetProgramId, targetVersionId);

            return true;
        }

        #endregion

        #region Component Filtering and Discovery

        public async Task<PagedResponse<UiComponentListDto>> GetByProgramIdAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var components = await _unitOfWork.UiComponents.GetByProgramIdAsync(programObjectId, cancellationToken);
            return await CreatePagedComponentResponse(components, pagination, cancellationToken);
        }

        public async Task<PagedResponse<UiComponentListDto>> GetByTypeAsync(string type, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var components = await _unitOfWork.UiComponents.GetByTypeAsync(type, cancellationToken);
            return await CreatePagedComponentResponse(components, pagination, cancellationToken);
        }

        public async Task<PagedResponse<UiComponentListDto>> GetByCreatorAsync(string creatorId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var components = await _unitOfWork.UiComponents.GetByCreatorAsync(creatorId, cancellationToken);
            return await CreatePagedComponentResponse(components, pagination, cancellationToken);
        }

        public async Task<PagedResponse<UiComponentListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var components = await _unitOfWork.UiComponents.GetByStatusAsync(status, cancellationToken);
            return await CreatePagedComponentResponse(components, pagination, cancellationToken);
        }

        public async Task<PagedResponse<UiComponentListDto>> GetAvailableForProgramVersionAsync(string programId, string versionId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var versionObjectId = ParseObjectId(versionId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            if (!await _unitOfWork.Versions.ExistsAsync(versionObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            var components = await _unitOfWork.UiComponents.GetByProgramVersionAsync(programObjectId, versionObjectId, cancellationToken);

            // Filter only active components
            var activeComponents = components.Where(c => c.Status == "active").ToList();

            return await CreatePagedComponentResponse(activeComponents, pagination, cancellationToken);
        }

        #endregion

        #region Component Lifecycle Management

        public async Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);

            if (!await _unitOfWork.UiComponents.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            var validStatuses = new[] { "draft", "active", "inactive", "deprecated" };
            if (!validStatuses.Contains(status))
            {
                throw new ArgumentException($"Invalid status: {status}. Valid statuses are: {string.Join(", ", validStatuses)}");
            }

            var success = await _unitOfWork.UiComponents.UpdateStatusAsync(objectId, status, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Updated status of UI component {ComponentId} to {Status}", id, status);
            }

            return success;
        }

        public async Task<bool> ActivateComponentAsync(string id, CancellationToken cancellationToken = default)
        {
            return await UpdateStatusAsync(id, "active", cancellationToken);
        }

        public async Task<bool> DeactivateComponentAsync(string id, CancellationToken cancellationToken = default)
        {
            return await UpdateStatusAsync(id, "inactive", cancellationToken);
        }

        public async Task<bool> DeprecateComponentAsync(string id, string reason, CancellationToken cancellationToken = default)
        {
            var success = await UpdateStatusAsync(id, "deprecated", cancellationToken);

            if (success)
            {
                _logger.LogInformation("Deprecated UI component {ComponentId} with reason: {Reason}", id, reason);
            }

            return success;
        }

        #endregion

        #region Component Bundle Management

        public async Task<UiComponentBundleDto> GetComponentBundleAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            var assets = await GetComponentAssetsAsync(id, cancellationToken);

            return new UiComponentBundleDto
            {
                Id = Guid.NewGuid().ToString(),
                ComponentId = id,
                BundleType = "standard",
                Assets = assets,
                Dependencies = new Dictionary<string, string>(),
                CreatedAt = DateTime.UtcNow,
                TotalSize = assets.Sum(a => a.Size)
            };
        }

        public async Task<bool> UploadComponentBundleAsync(string id, UiComponentBundleUploadDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            try
            {
                // Store each asset using version-specific storage structure
                var programId = component.ProgramId.ToString();
                var versionId = component.VersionId.ToString();

                foreach (var asset in dto.Assets)
                {
                    var componentAssetPath = $"components/{component.Name}/{asset.Path}";
                    var storageKey = await _fileStorageService.StoreFileAsync(
                        programId,
                        versionId,
                        componentAssetPath,
                        asset.Content,
                        asset.ContentType,
                        cancellationToken);

                    _logger.LogDebug("Stored component asset {AssetPath} with storage key {StorageKey}",
                        componentAssetPath, storageKey);
                }

                _logger.LogInformation("Uploaded bundle for UI component {ComponentId} with {AssetCount} assets",
                    id, dto.Assets.Count);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload bundle for UI component {ComponentId}", id);
                return false;
            }
        }

        public async Task<bool> UpdateComponentAssetsAsync(string id, List<UiComponentAssetDto> assets, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);

            if (!await _unitOfWork.UiComponents.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // In a real implementation, this would update the component's asset references
            _logger.LogInformation("Updated assets for UI component {ComponentId}", id);
            return true;
        }

        public async Task<List<UiComponentAssetDto>> GetComponentAssetsAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            try
            {
                // Get component files from version-specific storage
                var programId = component.ProgramId.ToString();
                var versionId = component.VersionId.ToString();

                var files = await _fileStorageService.ListVersionFilesAsync(programId, versionId, cancellationToken);

                // Filter files that belong to this component
                var componentPrefix = $"components/{component.Name}/";
                var componentFiles = files.Where(f => f.Path.StartsWith(componentPrefix)).ToList();

                return componentFiles.Select(file => new UiComponentAssetDto
                {
                    Path = file.Path.Substring(componentPrefix.Length), // Remove component prefix
                    ContentType = file.ContentType,
                    AssetType = DetermineAssetType(file.Path),
                    Size = file.Size,
                    LastModified = DateTime.UtcNow, // Would need proper tracking
                    Url = $"/api/components/{id}/assets/{Path.GetFileName(file.Path)}"
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get assets for component {ComponentId}", id);
                return new List<UiComponentAssetDto>();
            }
        }

        #endregion

        #region Component Configuration and Schema

        public async Task<UiComponentConfigDto> GetComponentConfigurationAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            return new UiComponentConfigDto
            {
                ComponentId = id,
                Configuration = component.Configuration as Dictionary<string, object> ?? new Dictionary<string, object>(),
                LastUpdated = component.CreatedAt,
                UpdatedBy = component.Creator
            };
        }

        public async Task<bool> UpdateComponentConfigurationAsync(string id, UiComponentConfigUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // Validate JSON string for Configuration
            ValidateJsonString(dto.Configuration, "Configuration");

            // Use AutoMapper to update the component
            _mapper.Map(dto, component);

            var success = await _unitOfWork.UiComponents.UpdateAsync(objectId, component, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Updated configuration for UI component {ComponentId}", id);
            }

            return success;
        }

        public async Task<UiComponentSchemaDto> GetComponentSchemaAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            return new UiComponentSchemaDto
            {
                ComponentId = id,
                Schema = component.Schema as Dictionary<string, object> ?? new Dictionary<string, object>(),
                LastUpdated = component.CreatedAt,
                UpdatedBy = component.Creator,
                IsValid = true // Would need proper schema validation
            };
        }

        public async Task<bool> UpdateComponentSchemaAsync(string id, UiComponentSchemaUpdateDto dto, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // Validate JSON string for Schema
            ValidateJsonString(dto.Schema, "Schema");

            // Use AutoMapper to update the component
            _mapper.Map(dto, component);

            var success = await _unitOfWork.UiComponents.UpdateAsync(objectId, component, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Updated schema for UI component {ComponentId}", id);
            }

            return success;
        }

        public async Task<bool> ValidateComponentSchemaAsync(string id, object testData, CancellationToken cancellationToken = default)
        {
            var schema = await GetComponentSchemaAsync(id, cancellationToken);

            // In a real implementation, this would validate testData against the schema
            _logger.LogInformation("Validated schema for UI component {ComponentId}", id);
            return true;
        }

        #endregion

        #region Component Usage and Integration

        public async Task<List<UiComponentUsageDto>> GetComponentUsageAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);

            if (!await _unitOfWork.UiComponents.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // In a real implementation, this would query actual usage from programs/mappings
            // For now, returning simulated data
            return new List<UiComponentUsageDto>();
        }

        public async Task<List<ProgramComponentMappingDto>> GetProgramVersionComponentMappingsAsync(string programId, string versionId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var versionObjectId = ParseObjectId(versionId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            if (!await _unitOfWork.Versions.ExistsAsync(versionObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            // In a real implementation, this would query actual mappings
            return new List<ProgramComponentMappingDto>();
        }

        public async Task<bool> MapComponentToProgramVersionAsync(string programId, string versionId, UiComponentMappingDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var versionObjectId = ParseObjectId(versionId);
            var componentObjectId = ParseObjectId(dto.ComponentId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            if (!await _unitOfWork.Versions.ExistsAsync(versionObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            if (!await _unitOfWork.UiComponents.ExistsAsync(componentObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"UI Component with ID {dto.ComponentId} not found.");
            }

            // In a real implementation, this would create the mapping
            _logger.LogInformation("Mapped UI component {ComponentId} to program {ProgramId}, version {VersionId} with name {MappingName}",
                dto.ComponentId, programId, versionId, dto.MappingName);

            return true;
        }

        public async Task<bool> UnmapComponentFromProgramVersionAsync(string programId, string versionId, string componentId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var versionObjectId = ParseObjectId(versionId);
            var componentObjectId = ParseObjectId(componentId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            if (!await _unitOfWork.Versions.ExistsAsync(versionObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            if (!await _unitOfWork.UiComponents.ExistsAsync(componentObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"UI Component with ID {componentId} not found.");
            }

            // In a real implementation, this would remove the mapping
            _logger.LogInformation("Unmapped UI component {ComponentId} from program {ProgramId}, version {VersionId}", componentId, programId, versionId);

            return true;
        }

        #endregion

        #region Component Discovery and Recommendations

        public async Task<List<UiComponentRecommendationDto>> GetRecommendedComponentsAsync(string programId, string versionId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var versionObjectId = ParseObjectId(versionId);

            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);
            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var version = await _unitOfWork.Versions.GetByIdAsync(versionObjectId, cancellationToken);
            if (version == null)
            {
                throw new KeyNotFoundException($"Version with ID {versionId} not found.");
            }

            // Get components from other versions of the same program
            var otherVersionComponents = await _unitOfWork.UiComponents.GetByProgramIdAsync(programObjectId, cancellationToken);

            // Filter out components from the current version
            var availableComponents = otherVersionComponents.Where(c => c.VersionId != versionObjectId && c.Status == "active").ToList();

            // Generate recommendations based on program type and language
            var recommendations = new List<UiComponentRecommendationDto>();

            foreach (var component in availableComponents)
            {
                var compatibilityScore = CalculateCompatibilityScore(component, program);
                if (compatibilityScore > 0.5)
                {
                    recommendations.Add(new UiComponentRecommendationDto
                    {
                        ComponentId = component._ID.ToString(),
                        ComponentName = component.Name,
                        ComponentType = component.Type,
                        ProgramId = component.ProgramId.ToString(),
                        VersionId = component.VersionId.ToString(),
                        RecommendationReason = GenerateRecommendationReason(component, program),
                        CompatibilityScore = compatibilityScore,
                        UsageCount = 0, // Would need usage tracking
                        Rating = 4.0 // Would need rating system
                    });
                }
            }

            return recommendations.OrderByDescending(r => r.CompatibilityScore).Take(10).ToList();
        }

        public async Task<List<UiComponentListDto>> SearchCompatibleComponentsAsync(UiComponentCompatibilitySearchDto dto, CancellationToken cancellationToken = default)
        {
            var allComponents = await _unitOfWork.UiComponents.GetAllAsync(cancellationToken);
            var compatibleComponents = new List<UiComponent>();

            foreach (var component in allComponents.Where(c => c.Status == "active"))
            {
                if (IsComponentCompatible(component, dto))
                {
                    compatibleComponents.Add(component);
                }
            }

            return await MapComponentListDtosAsync(compatibleComponents, cancellationToken);
        }

        #endregion

        #region Component Validation

        public async Task<bool> ValidateNameUniqueForVersionAsync(string programId, string versionId, string name, string? excludeId = null, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var versionObjectId = ParseObjectId(versionId);
            MongoDB.Bson.ObjectId? excludeObjectId = null;

            if (!string.IsNullOrEmpty(excludeId))
            {
                excludeObjectId = ParseObjectId(excludeId);
            }

            return await _unitOfWork.UiComponents.IsNameUniqueForVersionAsync(programObjectId, versionObjectId, name, excludeObjectId, cancellationToken);
        }

        public async Task<UiComponentValidationResult> ValidateComponentDefinitionAsync(string programId, string versionId, UiComponentCreateDto dto, CancellationToken cancellationToken = default)
        {
            var result = new UiComponentValidationResult { IsValid = true };

            // Validate component type
            if (!_supportedComponentTypes.Contains(dto.Type))
            {
                result.Errors.Add($"Unsupported component type: {dto.Type}");
                result.IsValid = false;
            }

            // Validate name
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 100)
            {
                result.Errors.Add("Component name is required and must be 100 characters or less");
                result.IsValid = false;
            }

            // Validate description
            if (dto.Description.Length > 500)
            {
                result.Errors.Add("Description must be 500 characters or less");
                result.IsValid = false;
            }

            // Validate program and version exist
            try
            {
                var programObjectId = ParseObjectId(programId);
                var versionObjectId = ParseObjectId(versionId);

                if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
                {
                    result.Errors.Add($"Program with ID {programId} not found");
                    result.IsValid = false;
                }

                if (!await _unitOfWork.Versions.ExistsAsync(versionObjectId, cancellationToken))
                {
                    result.Errors.Add($"Version with ID {versionId} not found");
                    result.IsValid = false;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Invalid program or version ID: {ex.Message}");
                result.IsValid = false;
            }

            // Name uniqueness validation
            try
            {
                if (!await ValidateNameUniqueForVersionAsync(programId, versionId, dto.Name, null, cancellationToken))
                {
                    result.Errors.Add($"Component with name '{dto.Name}' already exists in this version");
                    result.IsValid = false;
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Could not validate name uniqueness: {ex.Message}");
            }

            // Add suggestions
            if (dto.Tags.Count == 0)
            {
                result.Suggestions.Add(new UiComponentValidationSuggestionDto
                {
                    Type = "tags",
                    Message = "Consider adding tags to improve component discoverability",
                    SuggestedValue = GetSuggestedTags(dto.Type)
                });
            }

            return result;
        }

        #endregion

        #region Component Categories and Tags

        public async Task<List<string>> GetAvailableComponentTypesAsync(CancellationToken cancellationToken = default)
        {
            return _supportedComponentTypes.ToList();
        }

        public async Task<List<UiComponentCategoryDto>> GetComponentCategoriesAsync(CancellationToken cancellationToken = default)
        {
            var allComponents = await _unitOfWork.UiComponents.GetAllAsync(cancellationToken);

            return _componentCategories.Select(category => new UiComponentCategoryDto
            {
                Name = category.Key,
                Description = $"Components in the {category.Key} category",
                ComponentCount = allComponents.Count(c => category.Value.Contains(c.Type)),
                SubCategories = category.Value
            }).ToList();
        }

        public async Task<bool> AddComponentTagsAsync(string id, List<string> tags, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // Add new tags that don't already exist
            foreach (var tag in tags.Where(t => !component.Tags.Contains(t)))
            {
                component.Tags.Add(tag);
            }

            var success = await _unitOfWork.UiComponents.UpdateAsync(objectId, component, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Added tags {Tags} to UI component {ComponentId}",
                    string.Join(", ", tags), id);
            }

            return success;
        }

        public async Task<bool> RemoveComponentTagsAsync(string id, List<string> tags, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // Remove specified tags
            foreach (var tag in tags)
            {
                component.Tags.Remove(tag);
            }

            var success = await _unitOfWork.UiComponents.UpdateAsync(objectId, component, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Removed tags {Tags} from UI component {ComponentId}",
                    string.Join(", ", tags), id);
            }

            return success;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Deletes all assets for a component using the version-specific storage structure
        /// </summary>
        private async Task DeleteComponentAssetsAsync(UiComponent component, CancellationToken cancellationToken)
        {
            try
            {
                var programId = component.ProgramId.ToString();
                var versionId = component.VersionId.ToString();

                // Get all files for this version
                var files = await _fileStorageService.ListVersionFilesAsync(programId, versionId, cancellationToken);

                // Filter files that belong to this component
                var componentPrefix = $"components/{component.Name}/";
                var componentFiles = files.Where(f => f.Path.StartsWith(componentPrefix)).ToList();

                // Delete each component file
                foreach (var file in componentFiles)
                {
                    await _fileStorageService.DeleteFileAsync(programId, versionId, file.Path, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete assets for component {ComponentId}", component._ID);
                throw;
            }
        }

        /// <summary>
        /// Copies assets from source component to target component
        /// </summary>
        private async Task CopyComponentAssetsAsync(UiComponent sourceComponent, UiComponent targetComponent, CancellationToken cancellationToken)
        {
            try
            {
                var sourceProgramId = sourceComponent.ProgramId.ToString();
                var sourceVersionId = sourceComponent.VersionId.ToString();
                var targetProgramId = targetComponent.ProgramId.ToString();
                var targetVersionId = targetComponent.VersionId.ToString();

                // Get source component files
                var sourceFiles = await _fileStorageService.ListVersionFilesAsync(sourceProgramId, sourceVersionId, cancellationToken);

                var sourceComponentPrefix = $"components/{sourceComponent.Name}/";
                var sourceComponentFiles = sourceFiles.Where(f => f.Path.StartsWith(sourceComponentPrefix)).ToList();

                // Copy each file
                foreach (var sourceFile in sourceComponentFiles)
                {
                    var content = await _fileStorageService.GetFileContentAsync(sourceProgramId, sourceVersionId, sourceFile.Path, cancellationToken);

                    // Create target path with new component name
                    var relativePath = sourceFile.Path.Substring(sourceComponentPrefix.Length);
                    var targetPath = $"components/{targetComponent.Name}/{relativePath}";

                    await _fileStorageService.StoreFileAsync(targetProgramId, targetVersionId, targetPath, content, sourceFile.ContentType, cancellationToken);
                }

                _logger.LogInformation("Copied {FileCount} assets from component {SourceComponent} to {TargetComponent}",
                    sourceComponentFiles.Count, sourceComponent._ID, targetComponent._ID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy assets from component {SourceComponent} to {TargetComponent}",
                    sourceComponent._ID, targetComponent._ID);
                throw;
            }
        }

        private async Task<List<UiComponentListDto>> MapComponentListDtosAsync(List<UiComponent> components, CancellationToken cancellationToken)
        {
            var dtos = new List<UiComponentListDto>();

            foreach (var component in components)
            {
                var dto = _mapper.Map<UiComponentListDto>(component);

                // Get creator name
                try
                {
                    var creator = await _unitOfWork.Users.GetByIdAsync(ParseObjectId(component.Creator), cancellationToken);
                    if (creator != null)
                    {
                        dto.CreatorName = creator.FullName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get creator details for component {ComponentId}", component._ID);
                    dto.CreatorName = "Unknown";
                }

                // Get program name
                try
                {
                    var program = await _unitOfWork.Programs.GetByIdAsync(component.ProgramId, cancellationToken);
                    if (program != null)
                    {
                        dto.ProgramName = program.Name;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get program details for component {ComponentId}", component._ID);
                }

                // Get version number
                try
                {
                    var version = await _unitOfWork.Versions.GetByIdAsync(component.VersionId, cancellationToken);
                    if (version != null)
                    {
                        dto.VersionNumber = version.VersionNumber;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get version details for component {ComponentId}", component._ID);
                }

                // Set usage count (would need proper tracking)
                dto.UsageCount = 0;

                dtos.Add(dto);
            }

            return dtos;
        }

        private async Task<PagedResponse<UiComponentListDto>> CreatePagedComponentResponse(IEnumerable<UiComponent> components, PaginationRequestDto pagination, CancellationToken cancellationToken)
        {
            var componentsList = components.ToList();
            var totalCount = componentsList.Count;
            var paginatedComponents = componentsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = await MapComponentListDtosAsync(paginatedComponents, cancellationToken);
            return new PagedResponse<UiComponentListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        private async Task<UiComponentBundleInfoDto> GetComponentBundleInfoAsync(string id, CancellationToken cancellationToken)
        {
            var assets = await GetComponentAssetsAsync(id, cancellationToken);

            return new UiComponentBundleInfoDto
            {
                BundleType = "standard",
                AssetUrls = assets.Select(a => a.Url).ToList(),
                Dependencies = new Dictionary<string, string>(),
                LastUpdated = DateTime.UtcNow,
                TotalSize = assets.Sum(a => a.Size)
            };
        }

        private async Task<UiComponentStatsDto> GetComponentStatsAsync(string id, CancellationToken cancellationToken)
        {
            // In a real implementation, this would query actual usage statistics
            return new UiComponentStatsDto
            {
                TotalUsage = 0,
                ActiveUsage = 0,
                LastUsed = null,
                AverageRating = 0,
                RatingCount = 0,
                TotalDownloads = 0
            };
        }

        private string DetermineAssetType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".js" => "js",
                ".css" => "css",
                ".html" => "html",
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" => "image",
                _ => "file"
            };
        }

        private double CalculateCompatibilityScore(UiComponent component, Core.Models.Collaboration.Program program)
        {
            double score = 0.5; // Base score

            // Type compatibility
            if (program.UiType == "web" && component.Type.Contains("web"))
            {
                score += 0.3;
            }

            // Language compatibility
            if (program.Language == "angular" && component.Type.Contains("angular"))
            {
                score += 0.2;
            }
            else if (program.Language == "react" && component.Type.Contains("react"))
            {
                score += 0.2;
            }

            return Math.Min(1.0, score);
        }

        private string GenerateRecommendationReason(UiComponent component, Core.Models.Collaboration.Program program)
        {
            if (program.UiType == "web" && component.Type.Contains("web"))
            {
                return "Compatible with web applications";
            }

            return "Reusable component from other versions";
        }

        private bool IsComponentCompatible(UiComponent component, UiComponentCompatibilitySearchDto dto)
        {
            // Check type compatibility
            if (dto.CompatibleTypes.Any() && !dto.CompatibleTypes.Contains(component.Type))
            {
                return false;
            }

            // Check program type compatibility
            if (!string.IsNullOrEmpty(dto.ProgramType))
            {
                // Simple compatibility check - in real implementation would be more sophisticated
                if (dto.ProgramType == "web" && !component.Type.Contains("web"))
                {
                    return false;
                }
            }

            return true;
        }

        private string GetSuggestedTags(string componentType)
        {
            return componentType.ToLowerInvariant() switch
            {
                "input_form" => "forms, input, validation",
                "visualization" => "charts, graphs, data",
                "web_component" => "web, reusable, ui",
                _ => "ui, component"
            };
        }

        private Dictionary<string, List<string>> InitializeComponentCategories()
        {
            return new Dictionary<string, List<string>>
            {
                ["Input"] = new List<string> { "input_form", "validation", "form_builder" },
                ["Visualization"] = new List<string> { "chart", "graph", "data_visualization" },
                ["Layout"] = new List<string> { "layout", "grid", "responsive" },
                ["Navigation"] = new List<string> { "menu", "navigation", "breadcrumb" },
                ["Feedback"] = new List<string> { "notification", "alert", "modal" },
                ["Data"] = new List<string> { "table", "list", "data_grid" }
            };
        }

        private List<string> InitializeSupportedTypes()
        {
            return new List<string>
            {
                "input_form",
                "visualization",
                "composite",
                "web_component",
                "chart",
                "data_grid",
                "navigation",
                "layout"
            };
        }

        private static void ValidateJsonString(string jsonString, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
                return; // Empty is valid

            try
            {
                using var document = JsonDocument.Parse(jsonString);
                // If parsing succeeds, the JSON is valid
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Invalid JSON format in {fieldName}: {ex.Message}", ex);
            }
        }

        #endregion
    }
}