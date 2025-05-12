using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IClientRepository : IGenericRepository<Client>
    {
        Task<Client> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    }
}