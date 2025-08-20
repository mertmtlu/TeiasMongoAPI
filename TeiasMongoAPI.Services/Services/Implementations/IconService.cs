using AutoMapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Services.DTOs.Request.Icon;
using TeiasMongoAPI.Services.DTOs.Response.Icon;
using TeiasMongoAPI.Services.Interfaces;

namespace TeiasMongoAPI.Services.Services.Implementations
{
    public class IconService : IIconService
    {
        private readonly IIconRepository _iconRepository;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;
        private readonly ILogger<IconService> _logger;

        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);
        private const string CacheKeyPrefix = "icon_";
        private const string EntityCacheKeyPrefix = "icon_entity_";

        public IconService(
            IIconRepository iconRepository,
            IMapper mapper,
            IMemoryCache cache,
            ILogger<IconService> logger)
        {
            _iconRepository = iconRepository;
            _mapper = mapper;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IconResponseDto> CreateIconAsync(IconCreateDto createDto, string creator, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Creating icon for entity {EntityType}:{EntityId}", createDto.EntityType, createDto.EntityId);

            if (!ObjectId.TryParse(createDto.EntityId, out var entityObjectId))
            {
                throw new ArgumentException($"Invalid EntityId format: {createDto.EntityId}");
            }

            if (createDto.EntityType == IconEntityType.Program)
            {
                var hasExisting = await _iconRepository.EntityHasIconAsync(createDto.EntityType, entityObjectId, cancellationToken);
                if (hasExisting)
                {
                    throw new InvalidOperationException("Program can only have one icon. Delete existing icon first.");
                }
            }

            var icon = _mapper.Map<Icon>(createDto);
            icon.Creator = creator;

            var createdIcon = await _iconRepository.CreateAsync(icon, cancellationToken);
            
            var responseDto = _mapper.Map<IconResponseDto>(createdIcon);
            
            CacheIcon(responseDto);
            InvalidateEntityCache(createDto.EntityType, entityObjectId);

            _logger.LogInformation("Icon created successfully with ID: {IconId}", createdIcon._ID);
            return responseDto;
        }

        public async Task<IconResponseDto?> GetIconByIdAsync(ObjectId iconId, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CacheKeyPrefix}{iconId}";
            
            if (_cache.TryGetValue(cacheKey, out IconResponseDto? cachedIcon))
            {
                _logger.LogDebug("Icon {IconId} retrieved from cache", iconId);
                return cachedIcon;
            }

            var icon = await _iconRepository.GetByIdAsync(iconId, cancellationToken);
            if (icon == null || !icon.IsActive)
            {
                return null;
            }

            var responseDto = _mapper.Map<IconResponseDto>(icon);
            CacheIcon(responseDto);

            _logger.LogDebug("Icon {IconId} retrieved from database", iconId);
            return responseDto;
        }

        public async Task<IconResponseDto?> GetIconByEntityAsync(IconEntityType entityType, ObjectId entityId, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{EntityCacheKeyPrefix}{entityType}_{entityId}";
            
            if (_cache.TryGetValue(cacheKey, out IconResponseDto? cachedIcon))
            {
                _logger.LogDebug("Icon for entity {EntityType}:{EntityId} retrieved from cache", entityType, entityId);
                return cachedIcon;
            }

            var icon = await _iconRepository.GetByEntityAsync(entityType, entityId, cancellationToken);
            if (icon == null)
            {
                return null;
            }

            var responseDto = _mapper.Map<IconResponseDto>(icon);
            CacheIcon(responseDto);
            CacheEntityIcon(entityType, entityId, responseDto);

            _logger.LogDebug("Icon for entity {EntityType}:{EntityId} retrieved from database", entityType, entityId);
            return responseDto;
        }

        public async Task<IEnumerable<IconResponseDto>> GetIconsByEntityTypeAsync(IconEntityType entityType, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Retrieving icons for entity type: {EntityType}", entityType);
            
            var icons = await _iconRepository.GetByEntityTypeAsync(entityType, cancellationToken);
            var responseDtos = _mapper.Map<IEnumerable<IconResponseDto>>(icons);

            foreach (var dto in responseDtos)
            {
                CacheIcon(dto);
            }

            return responseDtos;
        }

        public async Task<IEnumerable<IconResponseDto>> GetIconsByEntityIdsAsync(IconEntityType entityType, IEnumerable<ObjectId> entityIds, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Retrieving icons for {Count} entities of type {EntityType}", entityIds.Count(), entityType);
            
            var icons = await _iconRepository.GetByEntityIdsAsync(entityType, entityIds, cancellationToken);
            var responseDtos = _mapper.Map<IEnumerable<IconResponseDto>>(icons);

            foreach (var dto in responseDtos)
            {
                CacheIcon(dto);
                if (ObjectId.TryParse(dto.EntityId, out var entityObjectId))
                {
                    CacheEntityIcon(entityType, entityObjectId, dto);
                }
            }

            return responseDtos;
        }

        public async Task<IEnumerable<IconResponseDto>> GetIconsByIdsAsync(IEnumerable<ObjectId> iconIds, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Retrieving {Count} icons by IDs", iconIds.Count());
            
            var icons = await _iconRepository.GetByIconIdsAsync(iconIds, cancellationToken);
            var responseDtos = _mapper.Map<IEnumerable<IconResponseDto>>(icons);

            foreach (var dto in responseDtos)
            {
                CacheIcon(dto);
            }

            return responseDtos;
        }

        public async Task<IconResponseDto?> UpdateIconAsync(ObjectId iconId, IconUpdateDto updateDto, string userId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Updating icon {IconId}", iconId);

            var existingIcon = await _iconRepository.GetByIdAsync(iconId, cancellationToken);
            if (existingIcon == null || !existingIcon.IsActive)
            {
                return null;
            }

            if (existingIcon.Creator != userId)
            {
                throw new UnauthorizedAccessException("Only the creator can update the icon");
            }

            _mapper.Map(updateDto, existingIcon);
            existingIcon.ModifiedAt = DateTime.UtcNow;

            var updateResult = await _iconRepository.UpdateAsync(iconId, existingIcon, cancellationToken);
            if (!updateResult)
            {
                return null;
            }

            var responseDto = _mapper.Map<IconResponseDto>(existingIcon);
            
            InvalidateIconCache(iconId);
            InvalidateEntityCache(existingIcon.EntityType, existingIcon.EntityId);
            CacheIcon(responseDto);

            _logger.LogInformation("Icon {IconId} updated successfully", iconId);
            return responseDto;
        }

        public async Task<bool> DeleteIconAsync(ObjectId iconId, string userId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Deleting icon {IconId}", iconId);

            var existingIcon = await _iconRepository.GetByIdAsync(iconId, cancellationToken);
            if (existingIcon == null || !existingIcon.IsActive)
            {
                return false;
            }

            if (existingIcon.Creator != userId)
            {
                throw new UnauthorizedAccessException("Only the creator can delete the icon");
            }

            var deleteResult = await _iconRepository.DeleteAsync(iconId, cancellationToken);
            
            if (deleteResult)
            {
                InvalidateIconCache(iconId);
                InvalidateEntityCache(existingIcon.EntityType, existingIcon.EntityId);
                _logger.LogInformation("Icon {IconId} deleted successfully", iconId);
            }

            return deleteResult;
        }

        public async Task<bool> DeleteIconByEntityAsync(IconEntityType entityType, ObjectId entityId, string userId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Deleting icon for entity {EntityType}:{EntityId}", entityType, entityId);

            var existingIcon = await _iconRepository.GetByEntityAsync(entityType, entityId, cancellationToken);
            if (existingIcon == null)
            {
                return false;
            }

            if (existingIcon.Creator != userId)
            {
                throw new UnauthorizedAccessException("Only the creator can delete the icon");
            }

            var deleteResult = await _iconRepository.DeleteByEntityAsync(entityType, entityId, cancellationToken);
            
            if (deleteResult)
            {
                InvalidateIconCache(existingIcon._ID);
                InvalidateEntityCache(entityType, entityId);
                _logger.LogInformation("Icon for entity {EntityType}:{EntityId} deleted successfully", entityType, entityId);
            }

            return deleteResult;
        }

        public async Task<IEnumerable<IconResponseDto>> GetUserIconsAsync(string creator, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Retrieving icons for creator: {Creator}", creator);
            
            var icons = await _iconRepository.GetByCreatorAsync(creator, cancellationToken);
            return _mapper.Map<IEnumerable<IconResponseDto>>(icons);
        }

        public async Task<long> GetIconCountByTypeAsync(IconEntityType entityType, CancellationToken cancellationToken = default)
        {
            return await _iconRepository.GetCountByEntityTypeAsync(entityType, cancellationToken);
        }

        public async Task<bool> ValidateIconConstraintsAsync(IconEntityType entityType, ObjectId entityId, ObjectId? excludeIconId = null, CancellationToken cancellationToken = default)
        {
            if (entityType == IconEntityType.Program)
            {
                var existingIcon = await _iconRepository.GetByEntityAsync(entityType, entityId, cancellationToken);
                if (existingIcon != null && (!excludeIconId.HasValue || existingIcon._ID != excludeIconId.Value))
                {
                    return false;
                }
            }
            return true;
        }

        private void CacheIcon(IconResponseDto icon)
        {
            var cacheKey = $"{CacheKeyPrefix}{icon.Id}";
            _cache.Set(cacheKey, icon, _cacheExpiration);
        }

        private void CacheEntityIcon(IconEntityType entityType, ObjectId entityId, IconResponseDto icon)
        {
            var cacheKey = $"{EntityCacheKeyPrefix}{entityType}_{entityId}";
            _cache.Set(cacheKey, icon, _cacheExpiration);
        }

        private void InvalidateIconCache(ObjectId iconId)
        {
            var cacheKey = $"{CacheKeyPrefix}{iconId}";
            _cache.Remove(cacheKey);
        }

        private void InvalidateEntityCache(IconEntityType entityType, ObjectId entityId)
        {
            var cacheKey = $"{EntityCacheKeyPrefix}{entityType}_{entityId}";
            _cache.Remove(cacheKey);
        }
    }
}