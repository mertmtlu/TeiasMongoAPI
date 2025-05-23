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

            // Enhance with additional data
            dto.Usage = await GetComponentUsageInternalAsync(id, cancellationToken);
            dto.Stats = await GetComponentStatsInternalAsync(component, cancellationToken);

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

            // Enhance with usage count and creator names
            foreach (var dto in dtos)
            {
                dto.UsageCount = await GetUsageCountAsync(dto.Id, cancellationToken);
                if (!string.IsNullOrEmpty(dto.Creator))
                {
                    dto.CreatorName = await GetUserNameAsync(dto.Creator, cancellationToken);
                }
                if (!string.IsNullOrEmpty(dto.ProgramId))
                {
                    dto.ProgramName = await GetProgramNameAsync(dto.ProgramId, cancellationToken);
                }
            }

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

            // Enhance with additional data
            foreach (var dto in dtos)
            {
                dto.UsageCount = await GetUsageCountAsync(dto.Id, cancellationToken);
                if (!string.IsNullOrEmpty(dto.Creator))
                {
                    dto.CreatorName = await GetUserNameAsync(dto.Creator, cancellationToken);
                }
                if (!string.IsNullOrEmpty(dto.ProgramId))
                {
                    dto.ProgramName = await GetProgramNameAsync(dto.ProgramId, cancellationToken);
                }
            }

            return new PagedResponse<UiComponentListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        public async Task<UiComponentDto> CreateAsync(UiComponentCreateDto dto, CancellationToken cancellationToken = default)
        {
            // Validate name uniqueness
            if (dto.IsGlobal)
            {
                if (!await ValidateNameUniqueForGlobalAsync(dto.Name, null, cancellationToken))
                {
                    throw new InvalidOperationException($"A global component with name '{dto.Name}' already exists.");
                }
            }
            else if (!string.IsNullOrEmpty(dto.ProgramId))
            {
                if (!await ValidateNameUniqueForProgramAsync(dto.Name, dto.ProgramId, null, cancellationToken))
                {
                    throw new InvalidOperationException($"A component with name '{dto.Name}' already exists for this program.");
                }
            }

            var component = _mapper.Map<UiComponent>(dto);

            var createdComponent = await _unitOfWork.UiComponents.CreateAsync(component, cancellationToken);

            _logger.LogInformation("Created UI component {ComponentId} with name {ComponentName}",
                createdComponent._ID, createdComponent.Name);

            return _mapper.Map<UiComponentDto>(createdComponent);
        }

        public async Task<UiComponentDto> CreateAsync(UiComponentCreateDto dto, string creatorId, CancellationToken cancellationToken = default)
        {
            // Validate name uniqueness
            if (dto.IsGlobal)
            {
                if (!await ValidateNameUniqueForGlobalAsync(dto.Name, null, cancellationToken))
                {
                    throw new InvalidOperationException($"A global component with name '{dto.Name}' already exists.");
                }
            }
            else if (!string.IsNullOrEmpty(dto.ProgramId))
            {
                if (!await ValidateNameUniqueForProgramAsync(dto.Name, dto.ProgramId, null, cancellationToken))
                {
                    throw new InvalidOperationException($"A component with name '{dto.Name}' already exists for this program.");
                }
            }

            var component = _mapper.Map<UiComponent>(dto);
            component.Creator = creatorId;

            var createdComponent = await _unitOfWork.UiComponents.CreateAsync(component, cancellationToken);

            _logger.LogInformation("Created UI component {ComponentId} with name {ComponentName} by user {CreatorId}",
                createdComponent._ID, createdComponent.Name, creatorId);

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

            // Validate name uniqueness if name is being changed
            if (!string.IsNullOrEmpty(dto.Name) && dto.Name != existingComponent.Name)
            {
                if (existingComponent.IsGlobal)
                {
                    if (!await ValidateNameUniqueForGlobalAsync(dto.Name, id, cancellationToken))
                    {
                        throw new InvalidOperationException($"A global component with name '{dto.Name}' already exists.");
                    }
                }
                else if (existingComponent.ProgramId.HasValue)
                {
                    if (!await ValidateNameUniqueForProgramAsync(dto.Name, existingComponent.ProgramId.Value.ToString(), id, cancellationToken))
                    {
                        throw new InvalidOperationException($"A component with name '{dto.Name}' already exists for this program.");
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

            // Check if component is in use
            var usage = await GetComponentUsageInternalAsync(id, cancellationToken);
            if (usage.Any(u => u.IsActive))
            {
                throw new InvalidOperationException("Cannot delete a UI component that is currently in use by active programs.");
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
            var componentsList = components.ToList();

            // Apply pagination
            var totalCount = componentsList.Count;
            var paginatedComponents = componentsList
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToList();

            var dtos = _mapper.Map<List<UiComponentListDto>>(paginatedComponents);

            // Enhance with additional data
            foreach (var dto in dtos)
            {
                dto.UsageCount = await GetUsageCountAsync(dto.Id, cancellationToken);
                if (!string.IsNullOrEmpty(dto.Creator))
                {
                    dto.CreatorName = await GetUserNameAsync(dto.Creator, cancellationToken);
                }
            }

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

            // Enhance with additional data
            foreach (var dto in dtos)
            {
                dto.UsageCount = await GetUsageCountAsync(dto.Id, cancellationToken);
                if (!string.IsNullOrEmpty(dto.Creator))
                {
                    dto.CreatorName = await GetUserNameAsync(dto.Creator, cancellationToken);
                }
                dto.ProgramName = await GetProgramNameAsync(programId, cancellationToken);
            }

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

            // Enhance with additional data
            foreach (var dto in dtos)
            {
                dto.UsageCount = await GetUsageCountAsync(dto.Id, cancellationToken);
                if (!string.IsNullOrEmpty(dto.Creator))
                {
                    dto.CreatorName = await GetUserNameAsync(dto.Creator, cancellationToken);
                }
                if (!string.IsNullOrEmpty(dto.ProgramId))
                {
                    dto.ProgramName = await GetProgramNameAsync(dto.ProgramId, cancellationToken);
                }
            }

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

            // Enhance with additional data
            var creatorName = await GetUserNameAsync(creatorId, cancellationToken);
            foreach (var dto in dtos)
            {
                dto.UsageCount = await GetUsageCountAsync(dto.Id, cancellationToken);
                dto.CreatorName = creatorName;
                if (!string.IsNullOrEmpty(dto.ProgramId))
                {
                    dto.ProgramName = await GetProgramNameAsync(dto.ProgramId, cancellationToken);
                }
            }

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

            // Enhance with additional data
            foreach (var dto in dtos)
            {
                dto.UsageCount = await GetUsageCountAsync(dto.Id, cancellationToken);
                if (!string.IsNullOrEmpty(dto.Creator))
                {
                    dto.CreatorName = await GetUserNameAsync(dto.Creator, cancellationToken);
                }
                if (!string.IsNullOrEmpty(dto.ProgramId))
                {
                    dto.ProgramName = await GetProgramNameAsync(dto.ProgramId, cancellationToken);
                }
            }

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

            // Enhance with additional data
            var programName = await GetProgramNameAsync(programId, cancellationToken);
            foreach (var dto in dtos)
            {
                dto.UsageCount = await GetUsageCountAsync(dto.Id, cancellationToken);
                if (!string.IsNullOrEmpty(dto.Creator))
                {
                    dto.CreatorName = await GetUserNameAsync(dto.Creator, cancellationToken);
                }
                if (!string.IsNullOrEmpty(dto.ProgramId))
                {
                    dto.ProgramName = await GetProgramNameAsync(dto.ProgramId, cancellationToken);
                }
                else if (dto.IsGlobal)
                {
                    dto.ProgramName = "Global";
                }
            }

            return new PagedResponse<UiComponentListDto>(dtos, pagination.PageNumber, pagination.PageSize, totalCount);
        }

        #endregion

        #region Component Lifecycle Management

        public async Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken = default)
        {
            var objectId = ParseObjectId(id);
            var success = await _unitOfWork.UiComponents.UpdateStatusAsync(objectId, status, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Updated status for UI component {ComponentId} to {Status}", id, status);
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
                // TODO: Store deprecation reason in component metadata
            }

            return success;
        }

        #endregion

        #region Validation Methods

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

            // Validate name
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                result.Errors.Add("Component name is required");
                result.IsValid = false;
            }
            else if (dto.Name.Length > 100)
            {
                result.Errors.Add("Component name cannot exceed 100 characters");
                result.IsValid = false;
            }

            // Validate type
            if (string.IsNullOrWhiteSpace(dto.Type))
            {
                result.Errors.Add("Component type is required");
                result.IsValid = false;
            }

            // Validate program assignment
            if (!dto.IsGlobal && string.IsNullOrEmpty(dto.ProgramId))
            {
                result.Errors.Add("Non-global components must be assigned to a program");
                result.IsValid = false;
            }

            if (dto.IsGlobal && !string.IsNullOrEmpty(dto.ProgramId))
            {
                result.Warnings.Add("Global components should not be assigned to a specific program");
            }

            // Validate name uniqueness
            if (!string.IsNullOrEmpty(dto.Name))
            {
                if (dto.IsGlobal)
                {
                    if (!await ValidateNameUniqueForGlobalAsync(dto.Name, null, cancellationToken))
                    {
                        result.Errors.Add($"A global component with name '{dto.Name}' already exists");
                        result.IsValid = false;
                    }
                }
                else if (!string.IsNullOrEmpty(dto.ProgramId))
                {
                    if (!await ValidateNameUniqueForProgramAsync(dto.Name, dto.ProgramId, null, cancellationToken))
                    {
                        result.Errors.Add($"A component with name '{dto.Name}' already exists for this program");
                        result.IsValid = false;
                    }
                }
            }

            return result;
        }

        #endregion

        #region Component Categories and Tags

        public async Task<List<string>> GetAvailableComponentTypesAsync(CancellationToken cancellationToken = default)
        {
            var components = await _unitOfWork.UiComponents.GetAllAsync(cancellationToken);
            return components.Select(c => c.Type).Distinct().Where(t => !string.IsNullOrEmpty(t)).ToList();
        }

        public async Task<List<UiComponentCategoryDto>> GetComponentCategoriesAsync(CancellationToken cancellationToken = default)
        {
            var components = await _unitOfWork.UiComponents.GetAllAsync(cancellationToken);
            var typeGroups = components.GroupBy(c => c.Type).ToList();

            var categories = new List<UiComponentCategoryDto>();

            foreach (var group in typeGroups)
            {
                if (string.IsNullOrEmpty(group.Key)) continue;

                categories.Add(new UiComponentCategoryDto
                {
                    Name = group.Key,
                    Description = GetTypeDescription(group.Key),
                    ComponentCount = group.Count(),
                    SubCategories = new List<string>() // TODO: Implement subcategories if needed
                });
            }

            return categories.OrderBy(c => c.Name).ToList();
        }

        public async Task<bool> AddComponentTagsAsync(string id, List<string> tags, CancellationToken cancellationToken = default)
        {
            // TODO: Implement tag management
            // This would require extending the UiComponent model to include tags
            _logger.LogInformation("Adding tags {Tags} to component {ComponentId}", string.Join(", ", tags), id);
            return await Task.FromResult(true);
        }

        public async Task<bool> RemoveComponentTagsAsync(string id, List<string> tags, CancellationToken cancellationToken = default)
        {
            // TODO: Implement tag management
            _logger.LogInformation("Removing tags {Tags} from component {ComponentId}", string.Join(", ", tags), id);
            return await Task.FromResult(true);
        }

        #endregion

        #region Methods requiring additional implementation

        // These methods require additional infrastructure or dependencies that aren't available in the current context

        public async Task<UiComponentBundleDto> GetComponentBundleAsync(string id, CancellationToken cancellationToken = default)
        {
            // TODO: Requires file storage service integration
            throw new NotImplementedException("Component bundle management requires file storage service integration");
        }

        public async Task<bool> UploadComponentBundleAsync(string id, UiComponentBundleUploadDto dto, CancellationToken cancellationToken = default)
        {
            // TODO: Requires file storage service integration
            throw new NotImplementedException("Component bundle management requires file storage service integration");
        }

        public async Task<bool> UpdateComponentAssetsAsync(string id, List<UiComponentAssetDto> assets, CancellationToken cancellationToken = default)
        {
            // TODO: Requires file storage service integration
            throw new NotImplementedException("Component asset management requires file storage service integration");
        }

        public async Task<List<UiComponentAssetDto>> GetComponentAssetsAsync(string id, CancellationToken cancellationToken = default)
        {
            // TODO: Requires file storage service integration
            return new List<UiComponentAssetDto>();
        }

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
                IsValid = true // TODO: Add schema validation
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
            // TODO: Implement schema validation logic
            return await Task.FromResult(true);
        }

        public async Task<List<UiComponentUsageDto>> GetComponentUsageAsync(string id, CancellationToken cancellationToken = default)
        {
            return await GetComponentUsageInternalAsync(id, cancellationToken);
        }

        public async Task<List<ProgramComponentMappingDto>> GetProgramComponentMappingsAsync(string programId, CancellationToken cancellationToken = default)
        {
            // TODO: Requires program-component mapping storage
            return new List<ProgramComponentMappingDto>();
        }

        public async Task<bool> MapComponentToProgramAsync(string programId, UiComponentMappingDto dto, CancellationToken cancellationToken = default)
        {
            // TODO: Requires program-component mapping storage
            _logger.LogInformation("Mapping component {ComponentId} to program {ProgramId}", dto.ComponentId, programId);
            return await Task.FromResult(true);
        }

        public async Task<bool> UnmapComponentFromProgramAsync(string programId, string componentId, CancellationToken cancellationToken = default)
        {
            // TODO: Requires program-component mapping storage
            _logger.LogInformation("Unmapping component {ComponentId} from program {ProgramId}", componentId, programId);
            return await Task.FromResult(true);
        }

        public async Task<List<UiComponentRecommendationDto>> GetRecommendedComponentsAsync(string programId, CancellationToken cancellationToken = default)
        {
            // TODO: Implement recommendation algorithm
            return new List<UiComponentRecommendationDto>();
        }

        public async Task<List<UiComponentListDto>> SearchCompatibleComponentsAsync(UiComponentCompatibilitySearchDto dto, CancellationToken cancellationToken = default)
        {
            // TODO: Implement compatibility search logic
            return new List<UiComponentListDto>();
        }

        #endregion

        #region Private Helper Methods

        private async Task<List<UiComponentUsageDto>> GetComponentUsageInternalAsync(string componentId, CancellationToken cancellationToken)
        {
            // TODO: This would need to query program-component mappings or relationships
            // For now, return empty list
            return new List<UiComponentUsageDto>();
        }

        private async Task<UiComponentStatsDto> GetComponentStatsInternalAsync(UiComponent component, CancellationToken cancellationToken)
        {
            var usage = await GetComponentUsageInternalAsync(component._ID.ToString(), cancellationToken);

            return new UiComponentStatsDto
            {
                TotalUsage = usage.Count,
                ActiveUsage = usage.Count(u => u.IsActive),
                LastUsed = usage.Any() ? usage.Max(u => u.UsedSince) : null,
                AverageRating = 0, // TODO: Implement rating system
                RatingCount = 0,
                TotalDownloads = 0 // TODO: Track downloads
            };
        }

        private async Task<int> GetUsageCountAsync(string componentId, CancellationToken cancellationToken)
        {
            var usage = await GetComponentUsageInternalAsync(componentId, cancellationToken);
            return usage.Count(u => u.IsActive);
        }

        private async Task<string> GetUserNameAsync(string userId, CancellationToken cancellationToken)
        {
            try
            {
                if (!ObjectId.TryParse(userId, out var userObjectId))
                {
                    return "Unknown User";
                }

                var user = await _unitOfWork.Users.GetByIdAsync(userObjectId, cancellationToken);
                return user?.FullName ?? "Unknown User";
            }
            catch
            {
                return "Unknown User";
            }
        }

        private async Task<string> GetProgramNameAsync(string programId, CancellationToken cancellationToken)
        {
            try
            {
                if (!ObjectId.TryParse(programId, out var programObjectId))
                {
                    return "Unknown Program";
                }

                var program = await _unitOfWork.Programs.GetByIdAsync(programObjectId, cancellationToken);
                return program?.Name ?? "Unknown Program";
            }
            catch
            {
                return "Unknown Program";
            }
        }

        private string GetTypeDescription(string type)
        {
            return type.ToLowerInvariant() switch
            {
                "input_form" => "Form components for user input",
                "visualization" => "Data visualization and chart components",
                "composite" => "Composite components combining multiple elements",
                "web_component" => "Web components for modern browser integration",
                "navigation" => "Navigation and menu components",
                "layout" => "Layout and structural components",
                _ => $"Components of type {type}"
            };
        }

        #endregion
    }
}