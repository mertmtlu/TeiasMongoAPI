using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IExecutionRepository : IGenericRepository<Execution>
    {
        Task<IEnumerable<Execution>> GetByProgramIdAsync(ObjectId programId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Execution>> GetByVersionIdAsync(ObjectId versionId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Execution>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Execution>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
        Task<IEnumerable<Execution>> GetRunningExecutionsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Execution>> GetCompletedExecutionsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Execution>> GetFailedExecutionsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Execution>> GetRecentExecutionsAsync(int count, CancellationToken cancellationToken = default);
        Task<bool> UpdateStatusAsync(ObjectId id, string status, CancellationToken cancellationToken = default);
        Task<bool> CompleteExecutionAsync(ObjectId id, int exitCode, string output, List<string> outputFiles, string? error = null, CancellationToken cancellationToken = default);
        Task<bool> UpdateResourceUsageAsync(ObjectId id, double cpuTime, long memoryUsed, long diskUsed, CancellationToken cancellationToken = default);
        Task<int> CleanupOldExecutionsAsync(int daysToKeep, CancellationToken cancellationToken = default);
    }
}