using MongoDB.Bson;
using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.Collaboration;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class RequestRepository : GenericRepository<Request>, IRequestRepository
    {
        private readonly MongoDbContext _context;

        public RequestRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<IEnumerable<Request>> GetByTypeAsync(string type, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Request>("requests")
                .Find(r => r.Type == type)
                .SortByDescending(r => r.RequestedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Request>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Request>("requests")
                .Find(r => r.Status == status)
                .SortByDescending(r => r.RequestedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Request>> GetByPriorityAsync(string priority, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Request>("requests")
                .Find(r => r.Priority == priority)
                .SortByDescending(r => r.RequestedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Request>> GetByRequestedByAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Request>("requests")
                .Find(r => r.RequestedBy == userId)
                .SortByDescending(r => r.RequestedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Request>> GetByAssignedToAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Request>("requests")
                .Find(r => r.AssignedTo == userId)
                .SortByDescending(r => r.RequestedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Request>> GetByProgramIdAsync(ObjectId programId, CancellationToken cancellationToken = default)
        {
            return await _context.Database.GetCollection<Request>("requests")
                .Find(r => r.ProgramId == programId)
                .SortByDescending(r => r.RequestedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Request>> GetUnassignedRequestsAsync(CancellationToken cancellationToken = default)
        {
            var filter = Builders<Request>.Filter.Or(
                Builders<Request>.Filter.Eq(r => r.AssignedTo, null),
                Builders<Request>.Filter.Eq(r => r.AssignedTo, "")
            );

            return await _context.Database.GetCollection<Request>("requests")
                .Find(filter)
                .SortBy(r => r.Priority)
                .ThenBy(r => r.RequestedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> UpdateStatusAsync(ObjectId id, string status, CancellationToken cancellationToken = default)
        {
            var update = Builders<Request>.Update.Set(r => r.Status, status);
            var result = await _context.Database.GetCollection<Request>("requests")
                .UpdateOneAsync(r => r._ID == id, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> AssignRequestAsync(ObjectId id, string assignedTo, CancellationToken cancellationToken = default)
        {
            var update = Builders<Request>.Update
                .Set(r => r.AssignedTo, assignedTo)
                .Set(r => r.Status, "in_progress");

            var result = await _context.Database.GetCollection<Request>("requests")
                .UpdateOneAsync(r => r._ID == id, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> UpdatePriorityAsync(ObjectId id, string priority, CancellationToken cancellationToken = default)
        {
            var update = Builders<Request>.Update.Set(r => r.Priority, priority);
            var result = await _context.Database.GetCollection<Request>("requests")
                .UpdateOneAsync(r => r._ID == id, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> AddResponseAsync(ObjectId id, RequestResponse response, CancellationToken cancellationToken = default)
        {
            var update = Builders<Request>.Update.Push(r => r.Responses, response);
            var result = await _context.Database.GetCollection<Request>("requests")
                .UpdateOneAsync(r => r._ID == id, update, cancellationToken: cancellationToken);

            return result.ModifiedCount > 0;
        }

        public async Task<IEnumerable<RequestResponse>> GetResponsesAsync(ObjectId id, CancellationToken cancellationToken = default)
        {
            var request = await _context.Database.GetCollection<Request>("requests")
                .Find(r => r._ID == id)
                .FirstOrDefaultAsync(cancellationToken);

            return request?.Responses ?? new List<RequestResponse>();
        }
    }
}