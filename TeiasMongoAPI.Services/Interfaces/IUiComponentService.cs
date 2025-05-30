﻿using TeiasMongoAPI.Services.DTOs.Request.Collaboration;
using TeiasMongoAPI.Services.DTOs.Request.Pagination;
using TeiasMongoAPI.Services.DTOs.Response.Collaboration;
using TeiasMongoAPI.Services.DTOs.Response.Common;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IUiComponentService
    {
        // Basic CRUD Operations
        Task<UiComponentDetailDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
        Task<PagedResponse<UiComponentListDto>> GetAllAsync(PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<UiComponentListDto>> SearchAsync(UiComponentSearchDto searchDto, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<UiComponentDto> CreateAsync(string programId, string versionId, UiComponentCreateDto dto, CancellationToken cancellationToken = default);
        Task<UiComponentDto> UpdateAsync(string id, UiComponentUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

        // Version-specific Component Operations
        Task<PagedResponse<UiComponentListDto>> GetByProgramVersionAsync(string programId, string versionId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<List<UiComponentListDto>> CopyComponentsToNewVersionAsync(string programId, string fromVersionId, string toVersionId, List<string>? componentNames = null, CancellationToken cancellationToken = default);
        Task<bool> CopyComponentToVersionAsync(string componentId, string targetProgramId, string targetVersionId, string? newName = null, CancellationToken cancellationToken = default);

        // Component Filtering and Discovery
        Task<PagedResponse<UiComponentListDto>> GetByProgramIdAsync(string programId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<UiComponentListDto>> GetByTypeAsync(string type, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<UiComponentListDto>> GetByCreatorAsync(string creatorId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<UiComponentListDto>> GetByStatusAsync(string status, PaginationRequestDto pagination, CancellationToken cancellationToken = default);
        Task<PagedResponse<UiComponentListDto>> GetAvailableForProgramVersionAsync(string programId, string versionId, PaginationRequestDto pagination, CancellationToken cancellationToken = default);

        // Component Lifecycle Management
        Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken = default);
        Task<bool> ActivateComponentAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> DeactivateComponentAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> DeprecateComponentAsync(string id, string reason, CancellationToken cancellationToken = default);

        // Component Bundle Management
        Task<UiComponentBundleDto> GetComponentBundleAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> UploadComponentBundleAsync(string id, UiComponentBundleUploadDto dto, CancellationToken cancellationToken = default);
        Task<bool> UpdateComponentAssetsAsync(string id, List<UiComponentAssetDto> assets, CancellationToken cancellationToken = default);
        Task<List<UiComponentAssetDto>> GetComponentAssetsAsync(string id, CancellationToken cancellationToken = default);

        // Component Configuration and Schema
        Task<UiComponentConfigDto> GetComponentConfigurationAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> UpdateComponentConfigurationAsync(string id, UiComponentConfigUpdateDto dto, CancellationToken cancellationToken = default);
        Task<UiComponentSchemaDto> GetComponentSchemaAsync(string id, CancellationToken cancellationToken = default);
        Task<bool> UpdateComponentSchemaAsync(string id, UiComponentSchemaUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> ValidateComponentSchemaAsync(string id, object testData, CancellationToken cancellationToken = default);

        // Component Usage and Integration
        Task<List<UiComponentUsageDto>> GetComponentUsageAsync(string id, CancellationToken cancellationToken = default);
        Task<List<ProgramComponentMappingDto>> GetProgramVersionComponentMappingsAsync(string programId, string versionId, CancellationToken cancellationToken = default);
        Task<bool> MapComponentToProgramVersionAsync(string programId, string versionId, UiComponentMappingDto dto, CancellationToken cancellationToken = default);
        Task<bool> UnmapComponentFromProgramVersionAsync(string programId, string versionId, string componentId, CancellationToken cancellationToken = default);

        // Component Discovery and Recommendations
        Task<List<UiComponentRecommendationDto>> GetRecommendedComponentsAsync(string programId, string versionId, CancellationToken cancellationToken = default);
        Task<List<UiComponentListDto>> SearchCompatibleComponentsAsync(UiComponentCompatibilitySearchDto dto, CancellationToken cancellationToken = default);

        // Component Validation
        Task<bool> ValidateNameUniqueForVersionAsync(string programId, string versionId, string name, string? excludeId = null, CancellationToken cancellationToken = default);
        Task<UiComponentValidationResult> ValidateComponentDefinitionAsync(string programId, string versionId, UiComponentCreateDto dto, CancellationToken cancellationToken = default);

        // Component Categories and Tags
        Task<List<string>> GetAvailableComponentTypesAsync(CancellationToken cancellationToken = default);
        Task<List<UiComponentCategoryDto>> GetComponentCategoriesAsync(CancellationToken cancellationToken = default);
        Task<bool> AddComponentTagsAsync(string id, List<string> tags, CancellationToken cancellationToken = default);
        Task<bool> RemoveComponentTagsAsync(string id, List<string> tags, CancellationToken cancellationToken = default);
    }
}