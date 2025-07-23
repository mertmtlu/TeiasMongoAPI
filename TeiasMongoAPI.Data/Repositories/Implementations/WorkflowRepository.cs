using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class WorkflowRepository : GenericRepository<Workflow>, IWorkflowRepository
    {
        public WorkflowRepository(MongoDbContext context) : base(context.Database)
        {
        }

        public async Task<IEnumerable<Workflow>> GetWorkflowsByUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Workflow>.Filter.Or(
                Builders<Workflow>.Filter.Eq(w => w.Creator, userId),
                Builders<Workflow>.Filter.ElemMatch(w => w.Permissions.AllowedUsers, userId),
                Builders<Workflow>.Filter.ElemMatch(w => w.Permissions.Permissions, p => p.UserId == userId)
            );

            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Workflow>> GetActiveWorkflowsAsync(CancellationToken cancellationToken = default)
        {
            var filter = Builders<Workflow>.Filter.Eq(w => w.Status, WorkflowStatus.Active);
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Workflow>> GetWorkflowsByStatusAsync(WorkflowStatus status, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Workflow>.Filter.Eq(w => w.Status, status);
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Workflow>> GetWorkflowsByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Workflow>.Filter.AnyEq(w => w.Tags, tag);
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Workflow>> GetWorkflowsWithPaginationAsync(int skip, int take, Expression<Func<Workflow, bool>>? filter = null, CancellationToken cancellationToken = default)
        {
            var mongoFilter = filter != null ? Builders<Workflow>.Filter.Where(filter) : Builders<Workflow>.Filter.Empty;
            
            return await _collection.Find(mongoFilter)
                .Skip(skip)
                .Limit(take)
                .SortByDescending(w => w.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Workflow>> SearchWorkflowsAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Workflow>.Filter.Or(
                Builders<Workflow>.Filter.Regex(w => w.Name, new BsonRegularExpression(searchTerm, "i")),
                Builders<Workflow>.Filter.Regex(w => w.Description, new BsonRegularExpression(searchTerm, "i")),
                Builders<Workflow>.Filter.AnyEq(w => w.Tags, searchTerm)
            );

            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<bool> ValidateWorkflowStructureAsync(ObjectId workflowId, CancellationToken cancellationToken = default)
        {
            var workflow = await GetByIdAsync(workflowId, cancellationToken);
            if (workflow == null) return false;

            // Check for cycles using DFS
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var node in workflow.Nodes)
            {
                if (!visited.Contains(node.Id))
                {
                    if (HasCycle(node.Id, workflow, visited, recursionStack))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool HasCycle(string nodeId, Workflow workflow, HashSet<string> visited, HashSet<string> recursionStack)
        {
            visited.Add(nodeId);
            recursionStack.Add(nodeId);

            var outgoingEdges = workflow.Edges.Where(e => e.SourceNodeId == nodeId);
            foreach (var edge in outgoingEdges)
            {
                if (!visited.Contains(edge.TargetNodeId))
                {
                    if (HasCycle(edge.TargetNodeId, workflow, visited, recursionStack))
                    {
                        return true;
                    }
                }
                else if (recursionStack.Contains(edge.TargetNodeId))
                {
                    return true;
                }
            }

            recursionStack.Remove(nodeId);
            return false;
        }

        public async Task<IEnumerable<Workflow>> GetWorkflowTemplatesAsync(CancellationToken cancellationToken = default)
        {
            var filter = Builders<Workflow>.Filter.Eq(w => w.IsTemplate, true);
            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<Workflow> CloneWorkflowAsync(ObjectId workflowId, string newName, string userId, CancellationToken cancellationToken = default)
        {
            var originalWorkflow = await GetByIdAsync(workflowId, cancellationToken);
            if (originalWorkflow == null) throw new InvalidOperationException("Workflow not found");

            var clonedWorkflow = new Workflow
            {
                Name = newName,
                Description = $"Cloned from {originalWorkflow.Name}",
                Creator = userId,
                CreatedAt = DateTime.UtcNow,
                Status = WorkflowStatus.Draft,
                Version = 1,
                Nodes = originalWorkflow.Nodes.Select(n => new WorkflowNode
                {
                    Id = n.Id,
                    Name = n.Name,
                    Description = n.Description,
                    ProgramId = n.ProgramId,
                    VersionId = n.VersionId,
                    NodeType = n.NodeType,
                    Position = n.Position,
                    InputConfiguration = n.InputConfiguration,
                    OutputConfiguration = n.OutputConfiguration,
                    ExecutionSettings = n.ExecutionSettings,
                    ConditionalExecution = n.ConditionalExecution,
                    UIConfiguration = n.UIConfiguration,
                    Metadata = n.Metadata,
                    CreatedAt = DateTime.UtcNow
                }).ToList(),
                Edges = originalWorkflow.Edges.Select(e => new WorkflowEdge
                {
                    Id = e.Id,
                    SourceNodeId = e.SourceNodeId,
                    TargetNodeId = e.TargetNodeId,
                    SourceOutputName = e.SourceOutputName,
                    TargetInputName = e.TargetInputName,
                    EdgeType = e.EdgeType,
                    Condition = e.Condition,
                    Transformation = e.Transformation,
                    UIConfiguration = e.UIConfiguration,
                    Metadata = e.Metadata,
                    CreatedAt = DateTime.UtcNow
                }).ToList(),
                Settings = originalWorkflow.Settings,
                Tags = originalWorkflow.Tags.ToList(),
                Metadata = originalWorkflow.Metadata,
                TemplateId = originalWorkflow.IsTemplate ? workflowId : originalWorkflow.TemplateId
            };

            return await CreateAsync(clonedWorkflow, cancellationToken);
        }

        public async Task<bool> UpdateWorkflowStatusAsync(ObjectId workflowId, WorkflowStatus status, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Workflow>.Filter.Eq(w => w._ID, workflowId);
            var update = Builders<Workflow>.Update
                .Set(w => w.Status, status)
                .Set(w => w.UpdatedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> IncrementExecutionCountAsync(ObjectId workflowId, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Workflow>.Filter.Eq(w => w._ID, workflowId);
            var update = Builders<Workflow>.Update
                .Inc(w => w.ExecutionCount, 1)
                .Set(w => w.UpdatedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdateAverageExecutionTimeAsync(ObjectId workflowId, TimeSpan averageTime, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Workflow>.Filter.Eq(w => w._ID, workflowId);
            var update = Builders<Workflow>.Update
                .Set(w => w.AverageExecutionTime, averageTime)
                .Set(w => w.UpdatedAt, DateTime.UtcNow);

            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
            return result.ModifiedCount > 0;
        }

        public async Task<IEnumerable<Workflow>> GetWorkflowsByPermissionAsync(string userId, WorkflowPermissionType permission, CancellationToken cancellationToken = default)
        {
            var filter = Builders<Workflow>.Filter.Or(
                Builders<Workflow>.Filter.Eq(w => w.Creator, userId),
                Builders<Workflow>.Filter.ElemMatch(w => w.Permissions.Permissions, 
                    p => p.UserId == userId && p.Permissions.Contains(permission))
            );

            return await _collection.Find(filter).ToListAsync(cancellationToken);
        }

        public async Task<bool> HasPermissionAsync(ObjectId workflowId, string userId, WorkflowPermissionType permission, CancellationToken cancellationToken = default)
        {
            var workflow = await GetByIdAsync(workflowId, cancellationToken);
            if (workflow == null) return false;

            // Creator has all permissions
            if (workflow.Creator == userId) return true;

            // Check if user has specific permission
            var userPermission = workflow.Permissions.Permissions.FirstOrDefault(p => p.UserId == userId);
            if (userPermission != null && userPermission.Permissions.Contains(permission))
            {
                return true;
            }

            // Check if workflow is public or permission is View
            if (workflow.Permissions.IsPublic || permission == WorkflowPermissionType.View)
            {
                return true;
            }

            return false;
        }

        public async Task<bool> ArchiveWorkflowAsync(ObjectId workflowId, CancellationToken cancellationToken = default)
        {
            return await UpdateWorkflowStatusAsync(workflowId, WorkflowStatus.Archived, cancellationToken);
        }

        public async Task<bool> RestoreWorkflowAsync(ObjectId workflowId, CancellationToken cancellationToken = default)
        {
            return await UpdateWorkflowStatusAsync(workflowId, WorkflowStatus.Active, cancellationToken);
        }
    }
}