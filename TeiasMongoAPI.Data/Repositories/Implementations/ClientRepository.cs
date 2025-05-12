using MongoDB.Driver;
using TeiasMongoAPI.Core.Interfaces.Repositories;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Data.Context;
using TeiasMongoAPI.Data.Repositories.Base;

namespace TeiasMongoAPI.Data.Repositories.Implementations
{
    public class ClientRepository : GenericRepository<Client>, IClientRepository
    {
        private readonly MongoDbContext _context;

        public ClientRepository(MongoDbContext context) : base(context.Database)
        {
            _context = context;
        }

        public async Task<Client> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return await _context.Clients
                .Find(c => c.Name == name)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}