using MongoDB.Bson;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IRequestRepository : IGenericRepository<Request>
    {
        Task<IEnumerable<Request>> GetByTypeAsync(string type, CancellationToken cancellationToken = default);
        Task<IEnumerable<Request>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
        Task<IEnumerable<Request>> GetByPriorityAsync(string priority, CancellationToken cancellationToken = default);
        Task<IEnumerable<Request>> GetByRequestedByAsync(string userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Request>> GetByAssignedToAsync(string userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Request>> GetByProgramIdAsync(ObjectId programId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Request>> GetUnassignedRequestsAsync(CancellationToken cancellationToken = default);
        Task<bool> UpdateStatusAsync(ObjectId id, string status, CancellationToken cancellationToken = default);
        Task<bool> AssignRequestAsync(ObjectId id, string assignedTo, CancellationToken cancellationToken = default);
        Task<bool> UpdatePriorityAsync(ObjectId id, string priority, CancellationToken cancellationToken = default);
        Task<bool> AddResponseAsync(ObjectId id, RequestResponse response, CancellationToken cancellationToken = default);
        Task<IEnumerable<RequestResponse>> GetResponsesAsync(ObjectId id, CancellationToken cancellationToken = default);
    }
}