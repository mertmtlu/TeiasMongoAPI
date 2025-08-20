using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Common;
using TeiasMongoAPI.Services.DTOs.Request.Icon;
using TeiasMongoAPI.Services.DTOs.Response.Icon;

namespace TeiasMongoAPI.Services.Interfaces
{
    public interface IIconService
    {
        Task<IconResponseDto> CreateIconAsync(IconCreateDto createDto, string creator, CancellationToken cancellationToken = default);
        Task<IconResponseDto?> GetIconByIdAsync(ObjectId iconId, CancellationToken cancellationToken = default);
        Task<IconResponseDto?> GetIconByEntityAsync(IconEntityType entityType, ObjectId entityId, CancellationToken cancellationToken = default);
        Task<IEnumerable<IconResponseDto>> GetIconsByEntityTypeAsync(IconEntityType entityType, CancellationToken cancellationToken = default);
        Task<IEnumerable<IconResponseDto>> GetIconsByEntityIdsAsync(IconEntityType entityType, IEnumerable<ObjectId> entityIds, CancellationToken cancellationToken = default);
        Task<IEnumerable<IconResponseDto>> GetIconsByIdsAsync(IEnumerable<ObjectId> iconIds, CancellationToken cancellationToken = default);
        Task<IconResponseDto?> UpdateIconAsync(ObjectId iconId, IconUpdateDto updateDto, string userId, CancellationToken cancellationToken = default);
        Task<bool> DeleteIconAsync(ObjectId iconId, string userId, CancellationToken cancellationToken = default);
        Task<bool> DeleteIconByEntityAsync(IconEntityType entityType, ObjectId entityId, string userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<IconResponseDto>> GetUserIconsAsync(string creator, CancellationToken cancellationToken = default);
        Task<long> GetIconCountByTypeAsync(IconEntityType entityType, CancellationToken cancellationToken = default);
        Task<bool> ValidateIconConstraintsAsync(IconEntityType entityType, ObjectId entityId, ObjectId? excludeIconId = null, CancellationToken cancellationToken = default);
    }
}