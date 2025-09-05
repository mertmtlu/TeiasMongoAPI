using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IProgramRepository : IGenericRepository<Program>
    {
        Task<IEnumerable<Program>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Program>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
        Task<IEnumerable<Program>> GetByTypeAsync(string type, CancellationToken cancellationToken = default);
        Task<IEnumerable<Program>> GetByLanguageAsync(string language, CancellationToken cancellationToken = default);
        Task<IEnumerable<Program>> GetPublicProgramsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Program>> GetUserAccessibleProgramsAsync(string userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Program>> GetGroupAccessibleProgramsAsync(string groupId, CancellationToken cancellationToken = default);
        Task<bool> UpdateStatusAsync(ObjectId id, string status, CancellationToken cancellationToken = default);
        Task<bool> UpdateCurrentVersionAsync(ObjectId id, string versionId, CancellationToken cancellationToken = default);
        Task<bool> IsNameUniqueAsync(string name, ObjectId? excludeId = null, CancellationToken cancellationToken = default);
        Task<bool> AddUserPermissionAsync(ObjectId programId, string userId, string accessLevel, CancellationToken cancellationToken = default);
        Task<bool> RemoveUserPermissionAsync(ObjectId programId, string userId, CancellationToken cancellationToken = default);
        Task<bool> UpdateUserPermissionAsync(ObjectId programId, string userId, string accessLevel, CancellationToken cancellationToken = default);
        Task<bool> AddGroupPermissionAsync(ObjectId programId, string groupId, string accessLevel, CancellationToken cancellationToken = default);
        Task<bool> RemoveGroupPermissionAsync(ObjectId programId, string groupId, CancellationToken cancellationToken = default);
        Task<bool> UpdateGroupPermissionAsync(ObjectId programId, string groupId, string accessLevel, CancellationToken cancellationToken = default);
    }
}