using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IUiComponentRepository : IGenericRepository<UiComponent>
    {
        Task<IEnumerable<UiComponent>> GetGlobalComponentsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<UiComponent>> GetByProgramIdAsync(ObjectId programId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UiComponent>> GetByTypeAsync(string type, CancellationToken cancellationToken = default);
        Task<IEnumerable<UiComponent>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UiComponent>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
        Task<IEnumerable<UiComponent>> GetAvailableForProgramAsync(ObjectId programId, CancellationToken cancellationToken = default);
        Task<bool> UpdateStatusAsync(ObjectId id, string status, CancellationToken cancellationToken = default);
        Task<bool> IsNameUniqueForProgramAsync(string name, ObjectId? programId, ObjectId? excludeId = null, CancellationToken cancellationToken = default);
        Task<bool> IsNameUniqueForGlobalAsync(string name, ObjectId? excludeId = null, CancellationToken cancellationToken = default);
    }
}