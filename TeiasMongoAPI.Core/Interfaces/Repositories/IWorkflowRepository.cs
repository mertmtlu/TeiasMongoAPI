using MongoDB.Bson;
using System.Linq.Expressions;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IWorkflowRepository : IGenericRepository<Workflow>
    {
        Task<IEnumerable<Workflow>> GetWorkflowsByUserAsync(string userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Workflow>> GetActiveWorkflowsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Workflow>> GetWorkflowsByStatusAsync(WorkflowStatus status, CancellationToken cancellationToken = default);
        Task<IEnumerable<Workflow>> GetWorkflowsByTagAsync(string tag, CancellationToken cancellationToken = default);
        Task<IEnumerable<Workflow>> GetWorkflowsWithPaginationAsync(int skip, int take, Expression<Func<Workflow, bool>>? filter = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<Workflow>> SearchWorkflowsAsync(string searchTerm, CancellationToken cancellationToken = default);
        Task<bool> ValidateWorkflowStructureAsync(ObjectId workflowId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Workflow>> GetWorkflowTemplatesAsync(CancellationToken cancellationToken = default);
        Task<Workflow> CloneWorkflowAsync(ObjectId workflowId, string newName, string userId, CancellationToken cancellationToken = default);
        Task<bool> UpdateWorkflowStatusAsync(ObjectId workflowId, WorkflowStatus status, CancellationToken cancellationToken = default);
        Task<bool> IncrementExecutionCountAsync(ObjectId workflowId, CancellationToken cancellationToken = default);
        Task<bool> UpdateAverageExecutionTimeAsync(ObjectId workflowId, TimeSpan averageTime, CancellationToken cancellationToken = default);
        Task<IEnumerable<Workflow>> GetWorkflowsByPermissionAsync(string userId, WorkflowPermissionType permission, CancellationToken cancellationToken = default);
        Task<bool> HasPermissionAsync(ObjectId workflowId, string userId, WorkflowPermissionType permission, CancellationToken cancellationToken = default);
        Task<bool> ArchiveWorkflowAsync(ObjectId workflowId, CancellationToken cancellationToken = default);
        Task<bool> RestoreWorkflowAsync(ObjectId workflowId, CancellationToken cancellationToken = default);
    }
}