using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IGroupRepository : IGenericRepository<Group>
    {
        Task<IEnumerable<Group>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Group>> GetActiveGroupsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Group>> GetUserGroupsAsync(ObjectId userId, CancellationToken cancellationToken = default);
        Task<bool> IsNameUniqueAsync(string name, ObjectId? excludeId = null, CancellationToken cancellationToken = default);
        Task<bool> AddMemberAsync(ObjectId groupId, ObjectId userId, CancellationToken cancellationToken = default);
        Task<bool> RemoveMemberAsync(ObjectId groupId, ObjectId userId, CancellationToken cancellationToken = default);
        Task<bool> IsMemberAsync(ObjectId groupId, ObjectId userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ObjectId>> GetGroupMembersAsync(ObjectId groupId, CancellationToken cancellationToken = default);
        Task<int> GetMemberCountAsync(ObjectId groupId, CancellationToken cancellationToken = default);
        Task<bool> UpdateStatusAsync(ObjectId groupId, bool isActive, CancellationToken cancellationToken = default);
    }
}