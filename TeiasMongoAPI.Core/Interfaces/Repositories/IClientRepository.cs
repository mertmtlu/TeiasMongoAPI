using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeiasMongoAPI.Core.Models.KeyModels;

namespace TeiasMongoAPI.Core.Interfaces.Repositories
{
    public interface IClientRepository : IGenericRepository<Client>
    {
        Task<Client> GetByNameAsync(string name);
    }
}
