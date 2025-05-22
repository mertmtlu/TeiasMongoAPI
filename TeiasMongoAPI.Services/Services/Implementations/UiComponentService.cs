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
    public class UiComponentService : BaseService, IUiComponentService
    {
        public UiComponentService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<UiComponentService> logger)
            : base(unitOfWork, mapper, logger)
        {
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

            // Enrich with additional data
            await EnrichUiComponentDetailAsync(dto, component, cancellationToken);

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

            var dtos = _mapper.Map<List<UiComponentListDto>>(paginatedComponents);

            // Enrich list items
            await EnrichUiComponentListAsync(dtos, cancellationToken);

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

            var dtos = _mapper.Map<List<UiComponentListDto>>(paginatedComponents);

            // Enrich list items
            await EnrichUiComponentListAsync(dtos, cancellationToken);

            return new PagedResponse<UiComponentListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<UiComponentDto> CreateAsync(UiComponentCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate name uniqueness
            var isNameUnique = dto.IsGlobal
                ? await ValidateNameUniqueForGlobalAsync(dto.Name, null, cancellationToken)
                : await ValidateNameUniqueForProgramAsync(dto.Name, dto.ProgramId, null, cancellationToken);

            if (!isNameUnique)
            {
                throw new InvalidOperationException($"UI Component with name '{dto.Name}' already exists.");
            }

            // Validate program exists if not global
            if (!dto.IsGlobal && !string.IsNullOrEmpty(dto.ProgramId))
            {
                var programObjectId = ParseObjectId(dto.ProgramId);
                var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);
                if (program == null)
                {
                    throw new KeyNotFoundException($"Program with ID {dto.ProgramId} not found.");
                }
            }

            var component = _mapper.Map<UiComponent>(dto);
            component.CreatedAt = DateTime.UtcNow;
            component.Status = "draft";

            if (!dto.IsGlobal && !string.IsNullOrEmpty(dto.ProgramId))
            {
                component.ProgramId = ParseObjectId(dto.ProgramId);
            }

            // Set creator from current context (would come from authentication context)
            // component.Creator = currentUserId; // This would be injected via service context

            var createdComponent = await _unitOfWork.UiComponents.CreateAsync(component, cancellationToken);

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

            // If updating name, check uniqueness
            if (!string.IsNullOrEmpty(dto.Name) && dto.Name != existingComponent.Name)
            {
                var isNameUnique = existingComponent.IsGlobal
                    ? await ValidateNameUniqueForGlobalAsync(dto.Name, id, cancellationToken)
                    : await ValidateNameUniqueForProgramAsync(dto.Name, existingComponent.ProgramId?.ToString(), id, cancellationToken);

                if (!isNameUnique)
                {
                    throw new InvalidOperationException($"UI Component with name '{dto.Name}' already exists.");
                }
            }

            _mapper.Map(dto, existingComponent);

            var success = await _unitOfWork.UiComponents.UpdateAsync(objectId, existingComponent, cancellationToken);

            if (!success)
            {
                throw new InvalidOperationException($"Failed to update UI component with ID {id}.");
            }

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

            // Check if component can be deleted (not being used by any programs)
            var canDelete = await CanDeleteComponentAsync(objectId, cancellationToken);
            if (!canDelete)
            {
                throw new InvalidOperationException("UI Component cannot be deleted. It is being used by one or more programs.");
            }

            return await _unitOfWork.UiComponents.DeleteAsync(objectId, cancellationToken);
        }

        #endregion

        #region Component Filtering and Discovery

        public async Task<PagedResponse<UiComponentListDto>> GetGlobalComponentsAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var components = await _unitOfWork.UiComponents.GetGlobalComponentsAsync(cancellationToken);
            var componentsList = components.ToList();

            // Apply pagination
            var totalCount = componentsList.Count;
            var paginatedComponents = componentsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<UiComponentListDto>>(paginatedComponents);
            await EnrichUiComponentListAsync(dtos, cancellationToken);

            return new PagedResponse<UiComponentListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<UiComponentListDto>> GetByProgramIdAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var components = await _unitOfWork.UiComponents.GetByProgramIdAsync(programObjectId, cancellationToken);
            var componentsList = components.ToList();

            // Apply pagination
            var totalCount = componentsList.Count;
            var paginatedComponents = componentsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<UiComponentListDto>>(paginatedComponents);
            await EnrichUiComponentListAsync(dtos, cancellationToken);

            return new PagedResponse<UiComponentListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<UiComponentListDto>> GetByTypeAsync(string type, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var components = await _unitOfWork.UiComponents.GetByTypeAsync(type, cancellationToken);
            var componentsList = components.ToList();

            // Apply pagination
            var totalCount = componentsList.Count;
            var paginatedComponents = componentsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<UiComponentListDto>>(paginatedComponents);
            await EnrichUiComponentListAsync(dtos, cancellationToken);

            return new PagedResponse<UiComponentListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<UiComponentListDto>> GetByCreatorAsync(string creatorId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var components = await _unitOfWork.UiComponents.GetByCreatorAsync(creatorId, cancellationToken);
            var componentsList = components.ToList();

            // Apply pagination
            var totalCount = componentsList.Count;
            var paginatedComponents = componentsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<UiComponentListDto>>(paginatedComponents);
            await EnrichUiComponentListAsync(dtos, cancellationToken);

            return new PagedResponse<UiComponentListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<UiComponentListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var components = await _unitOfWork.UiComponents.GetByStatusAsync(status, cancellationToken);
            var componentsList = components.ToList();

            // Apply pagination
            var totalCount = componentsList.Count;
            var paginatedComponents = componentsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<UiComponentListDto>>(paginatedComponents);
            await EnrichUiComponentListAsync(dtos, cancellationToken);

            return new PagedResponse<UiComponentListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<PagedResponse<UiComponentListDto>> GetAvailableForProgramAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var components = await _unitOfWork.UiComponents.GetAvailableForProgramAsync(programObjectId, cancellationToken);
            var componentsList = components.ToList();

            // Apply pagination
            var totalCount = componentsList.Count;
            var paginatedComponents = componentsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<UiComponentListDto>>(paginatedComponents);
            await EnrichUiComponentListAsync(dtos, cancellationToken);

            return new PagedResponse<UiComponentListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        #endregion

        #region Component Lifecycle Management

        public async Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            return await _unitOfWork.UiComponents.UpdateStatusAsync(objectId, status, cancellationToken);
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
                // TODO: Log deprecation reason
                _logger.LogInformation("UI Component {ComponentId} deprecated: {Reason}", id, reason);
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

            // TODO: Load bundle information from storage
            return new UiComponentBundleDto
            {
                Id = Guid.NewGuid().ToString(),
                ComponentId = id,
                BundleType = "angular_element", // Would come from component data
                Assets = new List<UiComponentAssetDto>(),
                Dependencies = new Dictionary<string, string>(),
                CreatedAt = DateTime.UtcNow,
                TotalSize = 0
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

            // TODO: Store bundle files and update component metadata
            _logger.LogInformation("Uploading bundle with {AssetCount} assets for component {ComponentId}",
                dto.Assets.Count, id);

            return true;
        }

        public async Task<bool> UpdateComponentAssetsAsync(string id, List<UiComponentAssetDto> assets, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // TODO: Update component asset references
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

            // TODO: Load asset information from storage
            return new List<UiComponentAssetDto>();
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

            return await _unitOfWork.UiComponents.UpdateAsync(objectId, component, cancellationToken);
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
                IsValid = ValidateSchema(component.Schema)
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

            return await _unitOfWork.UiComponents.UpdateAsync(objectId, component, cancellationToken);
        }

        public async Task<bool> ValidateComponentSchemaAsync(string id, object testData, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // TODO: Implement schema validation against test data
            return ValidateDataAgainstSchema(component.Schema, testData);
        }

        #endregion

        #region Component Usage and Integration

        public async Task<List<UiComponentUsageDto>> GetComponentUsageAsync(string id, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // TODO: Find programs that use this component
            var usage = new List<UiComponentUsageDto>();

            return usage;
        }

        public async Task<List<ProgramComponentMappingDto>> GetProgramComponentMappingsAsync(string programId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Load component mappings from program configuration
            var mappings = new List<ProgramComponentMappingDto>();

            return mappings;
        }

        public async Task<bool> MapComponentToProgramAsync(string programId, UiComponentMappingDto dto, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            var componentObjectId = ParseObjectId(dto.ComponentId);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(componentObjectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {dto.ComponentId} not found.");
            }

            // TODO: Store component mapping in program configuration
            return true;
        }

        public async Task<bool> UnmapComponentFromProgramAsync(string programId, string componentId, CancellationToken cancellationToken = default)
        {
            var programObjectId = ParseObjectId(programId);
            var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);

            if (program == null)
            {
                throw new KeyNotFoundException($"Program with ID {programId} not found.");
            }

            // TODO: Remove component mapping from program configuration
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

            // TODO: Implement recommendation algorithm based on program type, language, existing components
            var recommendations = new List<UiComponentRecommendationDto>();

            // Example recommendations based on program language
            var availableComponents = await _unitOfWork.UiComponents.GetAvailableForProgramAsync(programObjectId, cancellationToken);

            foreach (var component in availableComponents.Where(c => c.Status == "active"))
            {
                var compatibility = CalculateCompatibilityScore(program, component);

                if (compatibility > 0.5)
                {
                    recommendations.Add(new UiComponentRecommendationDto
                    {
                        ComponentId = component._ID.ToString(),
                        ComponentName = component.Name,
                        ComponentType = component.Type,
                        RecommendationReason = GetRecommendationReason(program, component),
                        CompatibilityScore = compatibility,
                        UsageCount = 0, // Would be computed from actual usage
                        Rating = 4.0 // Would be computed from user ratings
                    });
                }
            }

            return recommendations.OrderByDescending(r => r.CompatibilityScore).ToList();
        }

        public async Task<List<UiComponentListDto>> SearchCompatibleComponentsAsync(UiComponentCompatibilitySearchDto dto, CancellationToken cancellationToken = default)
        {
            var allComponents = await _unitOfWork.UiComponents.GetAllAsync(cancellationToken);
            var compatibleComponents = allComponents.Where(c => c.Status == "active");

            // Filter by type compatibility
            if (dto.CompatibleTypes?.Any() == true)
            {
                compatibleComponents = compatibleComponents.Where(c => dto.CompatibleTypes.Contains(c.Type));
            }

            // TODO: Add more sophisticated compatibility filtering based on features, language, etc.

            var componentsList = compatibleComponents.ToList();
            var dtos = _mapper.Map<List<UiComponentListDto>>(componentsList);
            await EnrichUiComponentListAsync(dtos, cancellationToken);

            return dtos;
        }

        #endregion

        #region Component Validation

        public async Task<bool> ValidateNameUniqueForProgramAsync(string name, string? programId, string? excludeId = null, CancellationToken cancellationToken = default)
        {
            ObjectId? programObjectId = null;
            if (!string.IsNullOrEmpty(programId))
            {
                programObjectId = ParseObjectId(programId);
            }

            ObjectId? excludeObjectId = null;
            if (!string.IsNullOrEmpty(excludeId))
            {
                excludeObjectId = ParseObjectId(excludeId);
            }

            return await _unitOfWork.UiComponents.IsNameUniqueForProgramAsync(name, programObjectId, excludeObjectId, cancellationToken);
        }

        public async Task<bool> ValidateNameUniqueForGlobalAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default)
        {
            ObjectId? excludeObjectId = null;
            if (!string.IsNullOrEmpty(excludeId))
            {
                excludeObjectId = ParseObjectId(excludeId);
            }

            return await _unitOfWork.UiComponents.IsNameUniqueForGlobalAsync(name, excludeObjectId, cancellationToken);
        }

        public async Task<UiComponentValidationResult> ValidateComponentDefinitionAsync(UiComponentCreateDto dto, CancellationToken cancellationToken = default)
        {
            var result = new UiComponentValidationResult { IsValid = true };

            // Validate required fields
            if (string.IsNullOrEmpty(dto.Name))
            {
                result.Errors.Add("Name is required");
                result.IsValid = false;
            }

            if (string.IsNullOrEmpty(dto.Type))
            {
                result.Errors.Add("Type is required");
                result.IsValid = false;
            }

            // Validate type
            var validTypes = await GetAvailableComponentTypesAsync(cancellationToken);
            if (!validTypes.Contains(dto.Type))
            {
                result.Errors.Add($"Invalid component type: {dto.Type}");
                result.IsValid = false;
            }

            // Validate program reference for non-global components
            if (!dto.IsGlobal && string.IsNullOrEmpty(dto.ProgramId))
            {
                result.Errors.Add("Program ID is required for non-global components");
                result.IsValid = false;
            }

            // Validate schema if provided
            if (dto.Schema != null && !ValidateSchema(dto.Schema))
            {
                result.Warnings.Add("Component schema appears to be invalid");
            }

            return result;
        }

        #endregion

        #region Component Categories and Tags

        public async Task<List<string>> GetAvailableComponentTypesAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return new List<string>
            {
                "input_form",
                "visualization",
                "composite",
                "web_component",
                "data_display",
                "navigation",
                "utility"
            };
        }

        public async Task<List<UiComponentCategoryDto>> GetComponentCategoriesAsync(CancellationToken cancellationToken = default)
        {
            var allComponents = await _unitOfWork.UiComponents.GetAllAsync(cancellationToken);
            var componentsByType = allComponents.GroupBy(c => c.Type);

            var categories = new List<UiComponentCategoryDto>();

            foreach (var group in componentsByType)
            {
                categories.Add(new UiComponentCategoryDto
                {
                    Name = group.Key,
                    Description = GetTypeDescription(group.Key),
                    ComponentCount = group.Count(),
                    SubCategories = new List<string>() // Could be expanded with sub-categories
                });
            }

            return categories.OrderBy(c => c.Name).ToList();
        }

        public async Task<bool> AddComponentTagsAsync(string id, List<string> tags, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // TODO: Add tags to component (would require tags field in UiComponent model)
            return true;
        }

        public async Task<bool> RemoveComponentTagsAsync(string id, List<string> tags, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var component = await _unitOfWork.UiComponents.GetByIdAsync(objectId, cancellationToken);

            if (component == null)
            {
                throw new KeyNotFoundException($"UI Component with ID {id} not found.");
            }

            // TODO: Remove tags from component
            return true;
        }

        #endregion

        #region Private Helper Methods

        private async Task EnrichUiComponentDetailAsync(UiComponentDetailDto dto, UiComponent component, CancellationToken cancellationToken)
        {
            // Enrich with creator name
            dto.CreatorName = "User Name"; // Would fetch from user service

            // Enrich with program name
            if (component.ProgramId.HasValue)
            {
                var program = await _unitOfWork.Programs.GetByIdAsync(component.ProgramId.Value, cancellationToken);
                dto.ProgramName = program?.Name;
            }

            // Enrich with assets
            dto.Assets = await GetComponentAssetsAsync(dto.Id, cancellationToken);

            // Enrich with bundle info
            try
            {
                var bundle = await GetComponentBundleAsync(dto.Id, cancellationToken);
                dto.BundleInfo = new UiComponentBundleInfoDto
                {
                    BundleType = bundle.BundleType,
                    AssetUrls = bundle.Assets.Select(a => a.Url).ToList(),
                    Dependencies = bundle.Dependencies,
                    LastUpdated = bundle.CreatedAt,
                    TotalSize = bundle.TotalSize
                };
            }
            catch
            {
                // Bundle not found or error loading
                dto.BundleInfo = null;
            }

            // Enrich with stats
            dto.Stats = new UiComponentStatsDto
            {
                TotalUsage = 0, // Would be computed from actual usage
                ActiveUsage = 0,
                LastUsed = null,
                AverageRating = 4.0,
                RatingCount = 0,
                TotalDownloads = 0
            };

            // Enrich with usage
            dto.Usage = await GetComponentUsageAsync(dto.Id, cancellationToken);
        }

        private async Task EnrichUiComponentListAsync(List<UiComponentListDto> dtos, CancellationToken cancellationToken)
        {
            foreach (var dto in dtos)
            {
                // TODO: Add user name resolution, program name, usage count, etc.
                dto.CreatorName = "User Name"; // Would fetch from user service
                dto.ProgramName = !string.IsNullOrEmpty(dto.ProgramId) ? "Program Name" : null;
                dto.UsageCount = 0; // Would be computed from actual usage
                await Task.CompletedTask; // Placeholder
            }
        }

        private async Task<bool> CanDeleteComponentAsync(ObjectId componentId, CancellationToken cancellationToken)
        {
            // TODO: Check if component is being used by any programs
            await Task.CompletedTask; // Placeholder
            return true;
        }

        private static bool ValidateSchema(object schema)
        {
            // TODO: Implement JSON schema validation
            return schema != null;
        }

        private static bool ValidateDataAgainstSchema(object schema, object data)
        {
            // TODO: Implement schema validation against data
            return true;
        }

        private static double CalculateCompatibilityScore(Program program, UiComponent component)
        {
            double score = 0.0;

            // Basic compatibility based on component status
            if (component.Status == "active")
                score += 0.3;

            // Language compatibility
            if (IsLanguageCompatible(program.Language, component.Type))
                score += 0.4;

            // Type compatibility
            if (IsTypeCompatible(program.Type, component.Type))
                score += 0.3;

            return Math.Min(score, 1.0);
        }

        private static bool IsLanguageCompatible(string programLanguage, string componentType)
        {
            // TODO: Implement language compatibility logic
            return true; // Simplified for now
        }

        private static bool IsTypeCompatible(string programType, string componentType)
        {
            // TODO: Implement type compatibility logic
            return true; // Simplified for now
        }

        private static string GetRecommendationReason(Program program, UiComponent component)
        {
            return $"Compatible with {program.Language} programs and commonly used for {program.Type} applications";
        }

        private static string GetTypeDescription(string type)
        {
            return type switch
            {
                "input_form" => "Components for collecting user input",
                "visualization" => "Components for displaying data and charts",
                "composite" => "Complex components combining multiple elements",
                "web_component" => "Reusable web components",
                "data_display" => "Components for showing data in various formats",
                "navigation" => "Components for navigation and menus",
                "utility" => "Utility and helper components",
                _ => "Component category"
            };
        }

        #endregion
    }
}