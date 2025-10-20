using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IRemoteAppRepository : IGenericRepository<RemoteApp>
    {
        Task<IEnumerable<RemoteApp>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default);
        Task<IEnumerable<RemoteApp>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
        Task<IEnumerable<RemoteApp>> GetPublicAppsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<RemoteApp>> GetUserAccessibleAppsAsync(ObjectId userId, CancellationToken cancellationToken = default);
        Task<bool> IsNameUniqueAsync(string name, ObjectId? excludeId = null, CancellationToken cancellationToken = default);
    }
}