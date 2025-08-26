using MongoDB.Bson;
using TeiasMongoAPI.Core.Models.Collaboration;
using Version = TeiasMongoAPI.Core.Models.Collaboration.Version;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IVersionRepository : IGenericRepository<Version>
    {
        Task<IEnumerable<Version>> GetByProgramIdAsync(ObjectId programId, CancellationToken cancellationToken = default);
        Task<Version> GetLatestVersionForProgramAsync(ObjectId programId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Version>> GetByCreatorAsync(string creatorId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Version>> GetByReviewerAsync(string reviewerId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Version>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
        Task<IEnumerable<Version>> GetPendingReviewsAsync(CancellationToken cancellationToken = default);
        Task<Version> GetByProgramIdAndVersionNumberAsync(ObjectId programId, int versionNumber, CancellationToken cancellationToken = default);
        Task<int> GetNextVersionNumberAsync(ObjectId programId, CancellationToken cancellationToken = default);
        Task<bool> UpdateStatusAsync(ObjectId id, string status, string reviewerId, string? comments = null, CancellationToken cancellationToken = default);
        Task<bool> AddFileAsync(ObjectId id, VersionFile file, CancellationToken cancellationToken = default);
        Task<bool> UpdateFileAsync(ObjectId id, string filePath, VersionFile file, CancellationToken cancellationToken = default);
        Task<bool> RemoveFileAsync(ObjectId id, string filePath, CancellationToken cancellationToken = default);
        Task<IEnumerable<VersionFile>> GetFilesByVersionIdAsync(ObjectId versionId, CancellationToken cancellationToken = default);
        Task<VersionFile?> GetFileByPathAsync(ObjectId versionId, string filePath, CancellationToken cancellationToken = default);
        
        Task<Dictionary<string, int>> GetVersionCountsByProgramIdsAsync(IEnumerable<ObjectId> programIds, CancellationToken cancellationToken = default);
        Task<List<Version>> GetVersionsByIdsAsync(IEnumerable<ObjectId> versionIds, CancellationToken cancellationToken = default);
    }
}