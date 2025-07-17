using MongoDB.Bson;
using System.Linq.Expressions;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IWorkflowExecutionRepository : IGenericRepository<WorkflowExecution>
    {
        Task<IEnumerable<WorkflowExecution>> GetExecutionsByWorkflowIdAsync(ObjectId workflowId, CancellationToken cancellationToken = default);
        Task<IEnumerable<WorkflowExecution>> GetExecutionsByUserIdAsync(string userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<WorkflowExecution>> GetExecutionsByStatusAsync(WorkflowExecutionStatus status, CancellationToken cancellationToken = default);
        Task<IEnumerable<WorkflowExecution>> GetRunningExecutionsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<WorkflowExecution>> GetExecutionsWithPaginationAsync(int skip, int take, Expression<Func<WorkflowExecution, bool>>? filter = null, CancellationToken cancellationToken = default);
        Task<WorkflowExecution> GetLatestExecutionAsync(ObjectId workflowId, CancellationToken cancellationToken = default);
        Task<IEnumerable<WorkflowExecution>> GetExecutionHistoryAsync(ObjectId workflowId, int limit = 10, CancellationToken cancellationToken = default);
        Task<bool> UpdateExecutionStatusAsync(ObjectId executionId, WorkflowExecutionStatus status, CancellationToken cancellationToken = default);
        Task<bool> UpdateExecutionProgressAsync(ObjectId executionId, WorkflowExecutionProgress progress, CancellationToken cancellationToken = default);
        Task<bool> AddNodeExecutionAsync(ObjectId executionId, NodeExecution nodeExecution, CancellationToken cancellationToken = default);
        Task<bool> UpdateNodeExecutionAsync(ObjectId executionId, string nodeId, NodeExecution nodeExecution, CancellationToken cancellationToken = default);
        Task<bool> AddExecutionLogAsync(ObjectId executionId, WorkflowExecutionLog log, CancellationToken cancellationToken = default);
        Task<bool> SetExecutionResultsAsync(ObjectId executionId, WorkflowExecutionResults results, CancellationToken cancellationToken = default);
        Task<bool> SetExecutionErrorAsync(ObjectId executionId, WorkflowExecutionError error, CancellationToken cancellationToken = default);
        Task<bool> UpdateResourceUsageAsync(ObjectId executionId, WorkflowResourceUsage resourceUsage, CancellationToken cancellationToken = default);
        Task<IEnumerable<WorkflowExecution>> GetExecutionsByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<WorkflowExecution> GetExecutionWithDetailsAsync(ObjectId executionId, CancellationToken cancellationToken = default);
        Task<bool> CancelExecutionAsync(ObjectId executionId, CancellationToken cancellationToken = default);
        Task<bool> PauseExecutionAsync(ObjectId executionId, CancellationToken cancellationToken = default);
        Task<bool> ResumeExecutionAsync(ObjectId executionId, CancellationToken cancellationToken = default);
        Task<IEnumerable<WorkflowExecution>> GetFailedExecutionsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<WorkflowExecution>> GetExecutionsForRetryAsync(CancellationToken cancellationToken = default);
        Task<bool> MarkExecutionForRetryAsync(ObjectId executionId, CancellationToken cancellationToken = default);
        Task<Dictionary<string, int>> GetExecutionStatisticsByStatusAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<WorkflowExecution>> GetLongRunningExecutionsAsync(TimeSpan threshold, CancellationToken cancellationToken = default);
        Task<bool> CleanupOldExecutionsAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
    }
}