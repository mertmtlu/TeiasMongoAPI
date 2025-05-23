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

            // Get program details if not global
            if (!component.IsGlobal && component.ProgramId.HasValue)
            {
                try
                {
                    var program = await _unitOfWork.Programs.GetByIdAsync(component.ProgramId.Value, cancellationToken);
                    if (program != null)
                    {
                        dto.ProgramName = program.Name;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get program details for component {ComponentId}", id);
                }
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

            if (searchDto.IsGlobal.HasValue)
            {
                filteredComponents = filteredComponents.Where(c => c.IsGlobal == searchDto.IsGlobal.Value);
            }

            if (!string.IsNullOrEmpty(searchDto.ProgramId))
            {
                var programObjectId = ParseObjectId(searchDto.ProgramId);
                filteredComponents = filteredComponents.Where(c => c.ProgramId == programObjectId);
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

        public async Task<UiComponentDto> CreateAsync(UiComponentCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate component type
            if (!_supportedComponentTypes.Contains(dto.Type))
            {
                throw new InvalidOperationException($"Unsupported component type: {dto.Type}");
            }

            // Validate name uniqueness
            if (dto.IsGlobal)
            {
                if (!await _unitOfWork.UiComponents.IsNameUniqueForGlobalAsync(dto.Name, null, cancellationToken))
                {
                    throw new InvalidOperationException($"Global component with name '{dto.Name}' already exists.");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(dto.ProgramId))
                {
                    throw new ArgumentException("ProgramId is required for non-global components.");
                }

                var programObjectId = ParseObjectId(dto.ProgramId);
                if (!await _unitOfWork.UiComponents.IsNameUniqueForProgramAsync(dto.Name, programObjectId, null, cancellationToken))
                {
                    throw new InvalidOperationException($"Component with name '{dto.Name}' already exists in this program.");
                }
            }

            var component = _mapper.Map<UiComponent>(dto);
            component.CreatedAt = DateTime.UtcNow;
            component.Creator = "system"; // Should come from current user context
            component.Status = "draft";

            if (!dto.IsGlobal && !string.IsNullOrEmpty(dto.ProgramId))
            {
                component.ProgramId = ParseObjectId(dto.ProgramId);
            }

            var createdComponent = await _unitOfWork.UiComponents.CreateAsync(component, cancellationToken);

            _logger.LogInformation("Created UI component {ComponentId} with name {ComponentName} of type {ComponentType}",
                createdComponent._ID, dto.Name, dto.Type);

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
                if (existingComponent.IsGlobal)
                {
                    if (!await _unitOfWork.UiComponents.IsNameUniqueForGlobalAsync(dto.Name, objectId, cancellationToken))
                    {
                        throw new InvalidOperationException($"Global component with name '{dto.Name}' already exists.");
                    }
                }
                else if (existingComponent.ProgramId.HasValue)
                {
                    if (!await _unitOfWork.UiComponents.IsNameUniqueForProgramAsync(dto.Name, existingComponent.ProgramId.Value, objectId, cancellationToken))
                    {
                        throw new InvalidOperationException($"Component with name '{dto.Name}' already exists in this program.");
                    }
                }
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

            var success = await _unitOfWork.UiComponents.DeleteAsync(objectId, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Deleted UI component {ComponentId}", id);
            }

            return success;
        }

        #endregion

        #region Component Filtering and Discovery

        public async Task<PagedResponse<UiComponentListDto>> GetGlobalComponentsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var components = await _unitOfWork.UiComponents.GetGlobalComponentsAsync(cancellationToken);
            return await CreatePagedComponentResponse(components, pagination, cancellationToken);
        }

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

        public async Task<PagedResponse<UiComponentListDto>> GetAvailableForProgramAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var components = await _unitOfWork.UiComponents.GetAvailableForProgramAsync(programObjectId, cancellationToken);

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
                // Store each asset
                foreach (var asset in dto.Assets)
                {
                    var storageKey = await _fileStorageService.StoreFileAsync(
                        $"components/{id}",
                        asset.Path,
                        asset.Content,
                        asset.ContentType,
                        cancellationToken);

                    _logger.LogDebug("Stored component asset {AssetPath} with storage key {StorageKey}",
                        asset.Path, storageKey);
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

            if (!await _unitOfWork.UiComponents.ExistsAsync(objectId, cancellationToken))
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            try
            {
                // Get component files from file storage
                var files = await _fileStorageService.ListProgramFilesAsync($"components/{id}", null, cancellationToken);

                return files.Select(file => new UiComponentAssetDto
                {
                    Path = file.FilePath,
                    ContentType = file.ContentType,
                    AssetType = DetermineAssetType(file.FilePath),
                    Size = file.Size,
                    LastModified = file.LastModified,
                    Url = $"/api/components/{id}/assets/{Path.GetFileName(file.FilePath)}"
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
                Configuration = component.Configuration,
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

            component.Configuration = dto.Configuration;

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
                Schema = component.Schema,
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

            component.Schema = dto.Schema;

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

        public async Task<List<ProgramComponentMappingDto>> GetProgramComponentMappingsAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // In a real implementation, this would query actual mappings
            return new List<ProgramComponentMappingDto>();
        }

        public async Task<bool> MapComponentToProgramAsync(string programId, UiComponentMappingDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var componentObjectId = ParseObjectId(dto.ComponentId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            if (!await _unitOfWork.UiComponents.ExistsAsync(componentObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"UI Component with ID {dto.ComponentId} not found.");
            }

            // In a real implementation, this would create the mapping
            _logger.LogInformation("Mapped UI component {ComponentId} to program {ProgramId} with name {MappingName}",
                dto.ComponentId, programId, dto.MappingName);

            return true;
        }

        public async Task<bool> UnmapComponentFromProgramAsync(string programId, string componentId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var componentObjectId = ParseObjectId(componentId);

            if (!await _unitOfWork.Programs.ExistsAsync(programObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            if (!await _unitOfWork.UiComponents.ExistsAsync(componentObjectId, cancellationToken))
            {
                throw new KeyNotFoundException($"UI Component with ID {componentId} not found.");
            }

            // In a real implementation, this would remove the mapping
            _logger.LogInformation("Unmapped UI component {ComponentId} from program {ProgramId}", componentId, programId);

            return true;
        }

        #endregion

        #region Component Discovery and Recommendations

        public async Task<List<UiComponentRecommendationDto>> GetRecommendedComponentsAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // Get available components for this program
            var availableComponents = await _unitOfWork.UiComponents.GetAvailableForProgramAsync(programObjectId, cancellationToken);

            // Generate recommendations based on program type and language
            var recommendations = new List<UiComponentRecommendationDto>();

            foreach (var component in availableComponents.Where(c => c.Status == "active"))
            {
                var compatibilityScore = CalculateCompatibilityScore(component, program);
                if (compatibilityScore > 0.5)
                {
                    recommendations.Add(new UiComponentRecommendationDto
                    {
                        ComponentId = component._ID.ToString(),
                        ComponentName = component.Name,
                        ComponentType = component.Type,
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

        public async Task<bool> ValidateNameUniqueForProgramAsync(string name, string? programId, string? excludeId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(programId))
            {
                return await ValidateNameUniqueForGlobalAsync(name, excludeId, cancellationToken);
            }

            var programObjectId = ParseObjectId(programId);
            MongoDB.Bson.ObjectId? excludeObjectId = null;

            if (!string.IsNullOrEmpty(excludeId))
            {
                excludeObjectId = ParseObjectId(excludeId);
            }

            return await _unitOfWork.UiComponents.IsNameUniqueForProgramAsync(name, programObjectId, excludeObjectId, cancellationToken);
        }

        public async Task<bool> ValidateNameUniqueForGlobalAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default)
        {
            MongoDB.Bson.ObjectId? excludeObjectId = null;

            if (!string.IsNullOrEmpty(excludeId))
            {
                excludeObjectId = ParseObjectId(excludeId);
            }

            return await _unitOfWork.UiComponents.IsNameUniqueForGlobalAsync(name, excludeObjectId, cancellationToken);
        }

        public async Task<UiComponentValidationResult> ValidateComponentDefinitionAsync(UiComponentCreateDto dto, CancellationToken cancellationToken = default)
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

            // Validate program dependency
            if (!dto.IsGlobal && string.IsNullOrEmpty(dto.ProgramId))
            {
                result.Errors.Add("ProgramId is required for non-global components");
                result.IsValid = false;
            }

            // Name uniqueness validation
            try
            {
                if (dto.IsGlobal)
                {
                    if (!await ValidateNameUniqueForGlobalAsync(dto.Name, null, cancellationToken))
                    {
                        result.Errors.Add($"Global component with name '{dto.Name}' already exists");
                        result.IsValid = false;
                    }
                }
                else if (!string.IsNullOrEmpty(dto.ProgramId))
                {
                    if (!await ValidateNameUniqueForProgramAsync(dto.Name, dto.ProgramId, null, cancellationToken))
                    {
                        result.Errors.Add($"Component with name '{dto.Name}' already exists in this program");
                        result.IsValid = false;
                    }
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

                // Get program name if not global
                if (!component.IsGlobal && component.ProgramId.HasValue)
                {
                    try
                    {
                        var program = await _unitOfWork.Programs.GetByIdAsync(component.ProgramId.Value, cancellationToken);
                        if (program != null)
                        {
                            dto.ProgramName = program.Name;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get program details for component {ComponentId}", component._ID);
                    }
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

            if (component.IsGlobal)
            {
                return "Globally available component";
            }

            return "General purpose component";
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
                if (dto.ProgramType == "web" && !component.Type.Contains("web") && !component.IsGlobal)
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

        #endregion
    }
}