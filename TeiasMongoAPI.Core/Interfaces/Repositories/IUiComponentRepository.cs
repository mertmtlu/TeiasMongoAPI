using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IUiComponentRepository : IGenericRepository<UiComponent>
    {
        // Version-specific Component Operations
        Task<IEnumerable<UiComponent>> GetByProgramVersionAsync(ObjectId programId, ObjectId versionId, CancellationToken cancellationToken = default);
        Task<bool> IsNameUniqueForVersionAsync(ObjectId programId, ObjectId versionId, string name, ObjectId? excludeId = null, CancellationToken cancellationToken = default);

        // Program-level Component Operations (across all versions)
        Task<IEnumerable<UiComponent>> GetByProgramIdAsync(ObjectId programId, CancellationToken cancellationToken = default);

        // Component Filtering and Discovery
        Task<IEnumerable<UiComponent>> GetByTypeAsync(string type, CancellationToken cancellationToken = default);
        Task<IEnumerable<UiComponent>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UiComponent>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);

        // Component Lifecycle Management
        Task<bool> UpdateStatusAsync(ObjectId id, string status, CancellationToken cancellationToken = default);

        // Component Search and Filtering
        Task<IEnumerable<UiComponent>> SearchComponentsAsync(
            string? name = null,
            string? type = null,
            string? creator = null,
            string? status = null,
            ObjectId? programId = null,
            ObjectId? versionId = null,
            List<string>? tags = null,
            DateTime? createdFrom = null,
            DateTime? createdTo = null,
            CancellationToken cancellationToken = default);

        // Component Statistics and Analytics
        Task<int> GetComponentCountForProgramAsync(ObjectId programId, CancellationToken cancellationToken = default);
        Task<int> GetComponentCountForVersionAsync(ObjectId programId, ObjectId versionId, CancellationToken cancellationToken = default);
        Task<int> GetComponentCountByTypeAsync(string type, CancellationToken cancellationToken = default);
        Task<int> GetActiveComponentCountAsync(CancellationToken cancellationToken = default);

        // Component Version Management
        Task<IEnumerable<UiComponent>> GetComponentsForCopyingAsync(ObjectId fromProgramId, ObjectId fromVersionId, List<string>? componentNames = null, CancellationToken cancellationToken = default);
        Task<bool> HasComponentsInVersionAsync(ObjectId programId, ObjectId versionId, CancellationToken cancellationToken = default);

        // Component Validation and Compatibility
        Task<IEnumerable<UiComponent>> GetCompatibleComponentsAsync(string programType, string? programLanguage = null, List<string>? requiredFeatures = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<UiComponent>> GetRecommendedComponentsAsync(ObjectId programId, ObjectId excludeVersionId, CancellationToken cancellationToken = default);

        // Component Tags and Categories
        Task<IEnumerable<string>> GetAllTagsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<UiComponent>> GetByTagsAsync(List<string> tags, CancellationToken cancellationToken = default);
        Task<Dictionary<string, int>> GetComponentCountByTypeAsync(CancellationToken cancellationToken = default);
    }
}