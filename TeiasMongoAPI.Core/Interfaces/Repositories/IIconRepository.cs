using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Common;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IIconRepository : IGenericRepository<Icon>
    {
        Task<IEnumerable<Icon>> GetByEntityTypeAsync(IconEntityType entityType, CancellationToken cancellationToken = default);
        Task<Icon?> GetByEntityAsync(IconEntityType entityType, ObjectId entityId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Icon>> GetByEntityIdsAsync(IconEntityType entityType, IEnumerable<ObjectId> entityIds, CancellationToken cancellationToken = default);
        Task<IEnumerable<Icon>> GetByIconIdsAsync(IEnumerable<ObjectId> iconIds, CancellationToken cancellationToken = default);
        Task<bool> EntityHasIconAsync(IconEntityType entityType, ObjectId entityId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Icon>> GetByCreatorAsync(string creator, CancellationToken cancellationToken = default);
        Task<bool> DeleteByEntityAsync(IconEntityType entityType, ObjectId entityId, CancellationToken cancellationToken = default);
        Task<long> GetCountByEntityTypeAsync(IconEntityType entityType, CancellationToken cancellationToken = default);
    }
}